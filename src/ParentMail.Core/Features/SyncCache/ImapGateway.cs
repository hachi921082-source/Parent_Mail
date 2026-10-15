using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using ParentMail.Core.Models;
using ParentMail.Core.Features.Compose;

namespace ParentMail.Core.Features.SyncCache;

/// <summary>
/// IMAP ゲートウェイの MailKit 実装（Gmail 専用）。アカウントごとに 1 インスタンス。
/// 最初の操作で imap.gmail.com:993 に接続してログインし、Dispose で切断する（IDLE は使わない）。
/// </summary>
public sealed class ImapGateway : IImapGateway
{
    public const string Host = "imap.gmail.com";
    public const int Port = 993;
    public const int TimeoutMilliseconds = 30_000;
    /// <summary>本文取得でまず全体を取りにいく上限。これ未満なら 1 往復で済む。</summary>
    public const int WholeMessageLimitBytes = 512 * 1024;

    private readonly Account _account;
    private readonly ICredentialStore _credentials;
    private ImapClient? _client;
    private IMailFolder? _open;
    private bool _openWritable;
    // 移動先などの解決済みフォルダ（パスごと）。接続し直したら捨てる
    private readonly Dictionary<string, IMailFolder> _folders = new(StringComparer.Ordinal);

    public ImapGateway(Account account, ICredentialStore credentials)
    {
        _account = account;
        _credentials = credentials;
    }

    // ---- 接続 ----

    private async Task<ImapClient> ConnectAsync(CancellationToken ct)
    {
        if (_client is { IsConnected: true, IsAuthenticated: true }) return _client;
        _client?.Dispose();
        _client = null;
        _open = null;
        _folders.Clear();

        var client = new ImapClient { Timeout = TimeoutMilliseconds };
        try
        {
            await client.ConnectAsync(Host, Port, SecureSocketOptions.SslOnConnect, ct).ConfigureAwait(false);
            var password = _credentials.Get(_account.Id);
            if (string.IsNullOrEmpty(password)) throw new AuthenticationException("no stored password");
            // Gmail のアプリパスワードは表示上 4 文字ずつ空白で区切られるため、空白を除いて送る
            await client.AuthenticateAsync(_account.Email, password.Replace(" ", ""), ct).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            throw;
        }
        _client = client;
        return client;
    }

    public async Task<LoginResult> TestLoginAsync(CancellationToken ct = default)
    {
        try
        {
            await ConnectAsync(ct).ConfigureAwait(false);
            return LoginResult.Ok;
        }
        catch (AuthenticationException)
        {
            return LoginResult.AuthFailed;
        }
        catch (Exception ex) when (SyncService.IsNetworkError(ex) && !(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            return LoginResult.NetworkError;
        }
    }

    // ---- フォルダ ----

    public async Task<IReadOnlyList<ImapFolderInfo>> ListFoldersAsync(CancellationToken ct = default)
    {
        var c = await ConnectAsync(ct).ConfigureAwait(false);
        var list = new List<ImapFolderInfo> { new(c.Inbox.FullName, FolderKind.Inbox) };
        var folders = await c.GetFoldersAsync(c.PersonalNamespaces[0], false, ct).ConfigureAwait(false);
        foreach (var f in folders)
        {
            // special-use 属性で判別する（Gmail の表示言語でフォルダ名が変わるため名前は使わない）
            FolderKind? kind = null;
            if (f.Attributes.HasFlag(FolderAttributes.Sent)) kind = FolderKind.Sent;
            else if (f.Attributes.HasFlag(FolderAttributes.Drafts)) kind = FolderKind.Drafts;
            else if (f.Attributes.HasFlag(FolderAttributes.Trash)) kind = FolderKind.Trash;
            else if (f.Attributes.HasFlag(FolderAttributes.Junk)) kind = FolderKind.Spam;
            else if (f.Attributes.HasFlag(FolderAttributes.All)) kind = FolderKind.AllMail;
            if (kind is not null) list.Add(new ImapFolderInfo(f.FullName, kind));
        }
        return list;
    }

    // フォルダ名の解決（LIST の往復）を同じ接続の間は 1 回で済ませる
    private async Task<IMailFolder> ResolveFolderAsync(string path, CancellationToken ct)
    {
        var c = await ConnectAsync(ct).ConfigureAwait(false);
        if (_folders.TryGetValue(path, out var cached)) return cached;
        var folder = string.Equals(path, "INBOX", StringComparison.OrdinalIgnoreCase)
            ? c.Inbox
            : await c.GetFolderAsync(path, ct).ConfigureAwait(false);
        _folders[path] = folder;
        return folder;
    }

    private async Task<IMailFolder> OpenAsync(string path, bool writable, CancellationToken ct)
    {
        await ConnectAsync(ct).ConfigureAwait(false);
        if (_open is { IsOpen: true } o && o.FullName == path && (!writable || _openWritable)) return o;
        var folder = await ResolveFolderAsync(path, ct).ConfigureAwait(false);
        await folder.OpenAsync(writable ? FolderAccess.ReadWrite : FolderAccess.ReadOnly, ct).ConfigureAwait(false);
        _open = folder;
        _openWritable = writable;
        return folder;
    }

    public async Task<ImapFolderStatus> GetFolderStatusAsync(string folderPath, CancellationToken ct = default)
    {
        var f = await OpenAsync(folderPath, false, ct).ConfigureAwait(false);
        return new ImapFolderStatus(folderPath, f.UidValidity, f.UidNext?.Id ?? 0,
            f.HighestModSeq > 0 ? f.HighestModSeq : null, f.Count);
    }

    public async Task<IReadOnlyList<uint>> SearchUidsAsync(string folderPath, DateTime? sinceUtc, CancellationToken ct = default)
    {
        var f = await OpenAsync(folderPath, false, ct).ConfigureAwait(false);
        var query = sinceUtc is { } d ? SearchQuery.DeliveredAfter(d.Date) : SearchQuery.All;
        var ids = await f.SearchAsync(query, ct).ConfigureAwait(false);
        return ids.Select(u => u.Id).ToList();
    }

    // ---- 取得 ----

    public async Task<IReadOnlyList<ImapHeader>> FetchHeadersAsync(string folderPath, IReadOnlyList<uint> uids, CancellationToken ct = default)
    {
        if (uids.Count == 0) return Array.Empty<ImapHeader>();
        var f = await OpenAsync(folderPath, false, ct).ConfigureAwait(false);
        var request = new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.GMailMessageId | MessageSummaryItems.Flags
                                       | MessageSummaryItems.InternalDate | MessageSummaryItems.Envelope
                                       | MessageSummaryItems.BodyStructure | MessageSummaryItems.References);
        var summaries = await f.FetchAsync(uids.Select(u => new UniqueId(u)).ToList(), request, ct).ConfigureAwait(false);

        var list = new List<ImapHeader>();
        foreach (var s in summaries)
        {
            if (s.GMailMessageId is not { } gmId) continue; // Gmail 以外の応答は対象外
            var env = s.Envelope;
            var from = env?.From.Mailboxes.FirstOrDefault();
            var date = env?.Date ?? s.InternalDate ?? DateTimeOffset.UnixEpoch;
            list.Add(new ImapHeader(
                s.UniqueId.Id, (long)gmId, WrapId(env?.MessageId) ?? "",
                s.References is { Count: > 0 } r ? string.Join(" ", r.Select(x => WrapId(x)!)) : null,
                env?.Subject ?? "", from?.Address ?? "", from?.Name ?? "",
                env?.To.Mailboxes.Select(ImapAddressText.Of).ToList() ?? new List<string>(),
                env?.Cc.Mailboxes.Select(ImapAddressText.Of).ToList() ?? new List<string>(),
                date.UtcDateTime,
                s.Flags?.HasFlag(MessageFlags.Seen) ?? false,
                s.Attachments.Any(),
                FormatReplyTo(env)));
        }
        return list;
    }

    // Reply-To が複数あるときは先頭の 1 件だけ使う。無い・読めないときは null（返信は From 宛て）
    private static string? FormatReplyTo(Envelope? env)
    {
        var m = env?.ReplyTo.Mailboxes.FirstOrDefault();
        return string.IsNullOrWhiteSpace(m?.Address) ? null : RecipientParser.Format(m.Name, m.Address);
    }

    private static string? WrapId(string? id)
        => string.IsNullOrEmpty(id) ? null : id.StartsWith('<') ? id : "<" + id + ">";

    public async Task<ImapBody?> FetchBodyAsync(string folderPath, uint uid, CancellationToken ct = default)
    {
        var f = await OpenAsync(folderPath, false, ct).ConfigureAwait(false);
        var id = new UniqueId(uid);

        // 小さいメールは 1 往復で全体を取って解析する（往復回数が同期時間をほぼ決めるため）。
        // 上限ちょうどまで届いたら途中で切れた可能性があるので、従来どおり構造から必要な部分だけ取る。
        using (var head = await f.GetStreamAsync(id, 0, WholeMessageLimitBytes, ct).ConfigureAwait(false))
        {
            if (head.Length > 0 && head.Length < WholeMessageLimitBytes)
            {
                var message = await MimeMessage.LoadAsync(head, ct).ConfigureAwait(false);
                var parsed = BodyParser.Parse(message);
                return new ImapBody(uid, parsed.Text, parsed.Html, parsed.Attachments);
            }
        }

        var s = await FetchStructureAsync(f, id, ct).ConfigureAwait(false);
        if (s?.Body is null) return null;

        // 添付は本体を取らず、BODYSTRUCTURE の情報だけを記録する（本体は必要になってから取る）
        string? text = null, html = null;
        if (s.TextBody is { } tb) text = (await f.GetBodyPartAsync(id, tb, ct).ConfigureAwait(false) as TextPart)?.Text;
        if (s.HtmlBody is { } hb) html = (await f.GetBodyPartAsync(id, hb, ct).ConfigureAwait(false) as TextPart)?.Text;

        text = BodyParser.NormalizeNewlines(text);
        var attachments = new List<AttachmentInfo>();
        foreach (var p in s.BodyParts)
        {
            if (p.PartSpecifier == s.TextBody?.PartSpecifier || p.PartSpecifier == s.HtmlBody?.PartSpecifier) continue;
            // 添付ではない余分な本文候補（text/plain, text/html）は捨てる
            if (!p.IsAttachment && (p.ContentType.IsMimeType("text", "plain") || p.ContentType.IsMimeType("text", "html"))) continue;
            attachments.Add(new AttachmentInfo
            {
                PartId = p.PartSpecifier,
                FileName = p.FileName ?? p.ContentType.Name ?? "",
                ContentType = p.ContentType.MimeType,
                Size = p.Octets,
                IsInline = !p.IsAttachment,
                ContentId = p.ContentId,
            });
        }
        return new ImapBody(uid, text, html, attachments);
    }

    private static async Task<IMessageSummary?> FetchStructureAsync(IMailFolder f, UniqueId id, CancellationToken ct)
    {
        var sums = await f.FetchAsync(new[] { id }, new FetchRequest(MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure), ct)
            .ConfigureAwait(false);
        return sums.FirstOrDefault();
    }

    public async Task<Stream?> FetchPartAsync(string folderPath, uint uid, string partId, CancellationToken ct = default)
    {
        var f = await OpenAsync(folderPath, false, ct).ConfigureAwait(false);
        var id = new UniqueId(uid);
        var s = await FetchStructureAsync(f, id, ct).ConfigureAwait(false);
        var part = s?.BodyParts.FirstOrDefault(p => p.PartSpecifier == partId);
        if (part is null) return null;

        var entity = await f.GetBodyPartAsync(id, part, ct).ConfigureAwait(false);
        var ms = new MemoryStream();
        switch (entity)
        {
            case MimePart mp when mp.Content is not null:
                await mp.Content.DecodeToAsync(ms, ct).ConfigureAwait(false);
                break;
            case MessagePart { Message: { } inner }:
                await inner.WriteToAsync(ms, ct).ConfigureAwait(false);
                break;
            default:
                ms.Dispose();
                return null;
        }
        ms.Position = 0;
        return ms;
    }

    // ---- 更新 ----

    public async Task SetSeenAsync(string folderPath, IReadOnlyList<uint> uids, bool seen, CancellationToken ct = default)
    {
        if (uids.Count == 0) return;
        var f = await OpenAsync(folderPath, true, ct).ConfigureAwait(false);
        var ids = uids.Select(u => new UniqueId(u)).ToList();
        if (seen) await f.AddFlagsAsync(ids, MessageFlags.Seen, true, ct).ConfigureAwait(false);
        else await f.RemoveFlagsAsync(ids, MessageFlags.Seen, true, ct).ConfigureAwait(false);
    }

    public async Task<uint?> MoveAsync(string fromFolderPath, uint uid, string toFolderPath, CancellationToken ct = default)
    {
        var f = await OpenAsync(fromFolderPath, true, ct).ConfigureAwait(false);
        var dest = await ResolveFolderAsync(toFolderPath, ct).ConfigureAwait(false);
        var moved = await f.MoveToAsync(new UniqueId(uid), dest, ct).ConfigureAwait(false);
        return moved?.Id;
    }

    public async Task<uint?> AppendAsync(string folderPath, byte[] rawMessage, bool seen, CancellationToken ct = default)
    {
        var c = await ConnectAsync(ct).ConfigureAwait(false);
        using var ms = new MemoryStream(rawMessage);
        var message = await MimeMessage.LoadAsync(ms, ct).ConfigureAwait(false);
        var folder = await c.GetFolderAsync(folderPath, ct).ConfigureAwait(false);
        var id = await folder.AppendAsync(message, seen ? MessageFlags.Seen : MessageFlags.None, ct).ConfigureAwait(false);
        return id?.Id;
    }

    public async Task DeleteAsync(string folderPath, IReadOnlyList<uint> uids, CancellationToken ct = default)
    {
        if (uids.Count == 0) return;
        var f = await OpenAsync(folderPath, true, ct).ConfigureAwait(false);
        var ids = uids.Select(u => new UniqueId(u)).ToList();
        await f.AddFlagsAsync(ids, MessageFlags.Deleted, true, ct).ConfigureAwait(false);
        await f.ExpungeAsync(ids, ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        var c = _client;
        _client = null;
        _open = null;
        _folders.Clear();
        if (c is null) return;
        try
        {
            if (c.IsConnected) await c.DisconnectAsync(true).ConfigureAwait(false);
        }
        catch
        {
            // 切断の失敗は無視する（接続が既に切れている場合など）
        }
        c.Dispose();
    }
}

/// <summary>アカウントごとの MailKit ゲートウェイを作る工場。</summary>
public sealed class ImapGatewayFactory : IImapGatewayFactory
{
    private readonly ICredentialStore _credentials;

    public ImapGatewayFactory(ICredentialStore credentials) => _credentials = credentials;

    public IImapGateway Create(Account account) => new ImapGateway(account, _credentials);
}
