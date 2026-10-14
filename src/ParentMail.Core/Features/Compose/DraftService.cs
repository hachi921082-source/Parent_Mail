using MimeKit;
using ParentMail.Core.Data;
using ParentMail.Core.Features.OutboxSend;
using ParentMail.Core.Models;

namespace ParentMail.Core.Features.Compose;

/// <summary>
/// 下書きサービス（compose PLAN-2）。ローカルの drafts 行と Gmail の Drafts フォルダを同じ内容に保つ。
/// 更新は「新しい版を APPEND してから古い UID を消す」の順（消す側が失敗しても内容は失われない）。
/// 下書きの Message-ID は行の ID から決まる固定の値にして、版が変わっても変えない。
/// ローカル行の更新は _gate（短時間だけ持つ）、Gmail への反映は _uploadGate（IMAP を待つ間も持つ）で直列に流す。
/// 反映の最中でもローカル保存は待たされない。閉じるときの保存は「ローカル保存 + QueueUpload」で、窓は IMAP を待たない。
/// 持つ順は必ず _uploadGate -> _gate（逆順にしない）。
/// </summary>
public sealed class DraftService : IDraftService
{
    /// <summary>下書きの Message-ID に使うドメイン部分。</summary>
    public const string MessageIdDomain = "parent-mail.local";

    private readonly IDraftRepository _drafts;
    private readonly IMailRepository _mail;
    private readonly IAccountRepository _accounts;
    private readonly IImapGatewayFactory _gateways;
    private readonly IBodyFetcher _bodyFetcher;
    private readonly IAttachmentService _attachmentService;
    private readonly CachePaths _paths;
    private readonly IAppLog _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _uploadGate = new(1, 1);
    // 行ごとの保存回数（_gate の下でだけ触る）。反映中に内容が書き換わったかの判定に使う
    private readonly Dictionary<long, long> _revisions = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Task, byte> _background = new();

    public DraftService(IDraftRepository drafts, IMailRepository mail, IAccountRepository accounts, IImapGatewayFactory gateways,
        IBodyFetcher bodyFetcher, IAttachmentService attachmentService, CachePaths paths, IAppLog log)
    {
        _drafts = drafts;
        _mail = mail;
        _accounts = accounts;
        _gateways = gateways;
        _bodyFetcher = bodyFetcher;
        _attachmentService = attachmentService;
        _paths = paths;
        _log = log;
    }

    /// <summary>この下書きの Message-ID（山括弧なし）。行の ID だけで決まるので版が変わっても同じ。</summary>
    public static string MessageIdOf(long localId) => $"draft.{localId}@{MessageIdDomain}";

    /// <summary>窓を閉じたあとのバックグラウンド反映（または削除）が終わったとき。一覧の更新を促すために使う。</summary>
    public event EventHandler<AccountEventArgs>? BackgroundCompleted;

    private long RevisionOf(long id) => _revisions.GetValueOrDefault(id);

    // ---- 保存 ----

    public async Task<DraftItem> SaveAsync(DraftSnapshot snapshot, bool uploadNow, CancellationToken ct = default)
    {
        DraftItem row;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var existing = snapshot.DraftId is { } id ? FindDraft(snapshot.AccountId, id) : null;
            row = new DraftItem
            {
                LocalId = existing?.LocalId ?? snapshot.DraftId ?? 0, AccountId = snapshot.AccountId,
                To = snapshot.To, Cc = snapshot.Cc, Bcc = snapshot.Bcc, Subject = snapshot.Subject, Body = snapshot.Body,
                Attachments = existing?.Attachments ?? Array.Empty<AttachmentInfo>(),
                ReplyHeaders = snapshot.ReplyHeaders, GmailUid = existing?.GmailUid, Dirty = true,
            };
            // 先にローカルへ保存して ID を決め、添付をその ID のフォルダへ写す
            row = _drafts.Save(row);
            row = row with { Attachments = CopyAttachments(row.AccountId, snapshot.Attachments, row.LocalId) };
            row = _drafts.Save(row);
            _revisions[row.LocalId] = RevisionOf(row.LocalId) + 1;
        }
        finally { _gate.Release(); }

        if (!uploadNow) return row;
        await _uploadGate.WaitAsync(ct).ConfigureAwait(false);
        try { return (await UploadAsync(row.AccountId, row.LocalId, ct).ConfigureAwait(false)).Row ?? row; }
        finally { _uploadGate.Release(); }
    }

    /// <summary>
    /// 窓を閉じたあとの反映。このアカウントの Dirty な下書きをバックグラウンドで Drafts へ上げる。すぐ戻る。
    /// 窓の寿命とは無関係に動く。失敗やアプリ終了で残ったものは Dirty のまま FlushDirtyAsync が拾う。
    /// </summary>
    public void QueueUpload(string accountId)
    {
        Track(async () =>
        {
            var uploaded = await FlushCoreAsync(accountId, CancellationToken.None).ConfigureAwait(false);
            if (uploaded > 0) BackgroundCompleted?.Invoke(this, new AccountEventArgs(accountId));
        }, "下書きのバックグラウンド反映に失敗");
    }

    /// <summary>実行中のバックグラウンド処理がすべて終わるまで待つ（テスト用）。</summary>
    public Task WaitForBackgroundAsync() => Task.WhenAll(_background.Keys.ToArray());

    private void Track(Func<Task> work, string failureText)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = Task.Run(async () =>
        {
            await started.Task.ConfigureAwait(false);
            try { await work().ConfigureAwait(false); }
            catch (Exception ex) { _log.Error("Draft", failureText, ex); }
        });
        _background[task] = 0;
        // 登録が済んでから走らせ、終わったら一覧から外す
        _ = task.ContinueWith(t => _background.TryRemove(t, out _), TaskScheduler.Default);
        started.SetResult();
    }

    // ---- 削除 ----

    public async Task DeleteAsync(long draftId, CancellationToken ct = default)
    {
        await _uploadGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var row = FindDraft(draftId);
            if (row is null)
            {
                // 行が無くても、残っていれば後始末だけ依頼する
                await RemoveRowAsync(draftId, ct).ConfigureAwait(false);
                return;
            }
            await TryDeleteRemoteAsync(row.AccountId, row.GmailUid ?? FindCachedUid(row), ct).ConfigureAwait(false);
            await RemoveRowAsync(draftId, ct).ConfigureAwait(false);
        }
        finally { _uploadGate.Release(); }
    }

    /// <summary>ローカルの行だけ今すぐ消し、Gmail 側の削除はバックグラウンドへ回す（窓を閉じるとき用）。</summary>
    public async Task DeleteLocalAndQueueRemoteAsync(long draftId, CancellationToken ct = default)
    {
        DraftItem? row;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            row = FindDraft(draftId);
            _drafts.Remove(draftId);
            _revisions.Remove(draftId);
        }
        finally { _gate.Release(); }
        if (row is null) return;

        // 反映中なら、そちらが「行が消えた」ことを見て追加した版を片付ける。ここは分かっている UID だけ消す
        var uid = row.GmailUid ?? FindCachedUid(row);
        if (uid is null) return;
        Track(async () =>
        {
            await _uploadGate.WaitAsync().ConfigureAwait(false);
            try { await TryDeleteRemoteAsync(row.AccountId, uid, CancellationToken.None).ConfigureAwait(false); }
            finally { _uploadGate.Release(); }
            BackgroundCompleted?.Invoke(this, new AccountEventArgs(row.AccountId));
        }, "Gmail の下書きのバックグラウンド削除に失敗");
    }

    private async Task RemoveRowAsync(long draftId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { _drafts.Remove(draftId); _revisions.Remove(draftId); }
        finally { _gate.Release(); }
    }

    // ---- 未アップロードのまとめ送り ----

    public async Task FlushDirtyAsync(string accountId, CancellationToken ct = default)
        => await FlushCoreAsync(accountId, ct).ConfigureAwait(false);

    // 上げられた件数を返す。つながらないときは 1 通ごとに待たされないよう、最初の失敗で打ち切る
    private async Task<int> FlushCoreAsync(string accountId, CancellationToken ct)
    {
        await _uploadGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var uploaded = 0;
            foreach (var row in _drafts.GetAll(accountId).Where(d => d.Dirty).ToList())
            {
                ct.ThrowIfCancellationRequested();
                var (_, ok) = await UploadAsync(accountId, row.LocalId, ct).ConfigureAwait(false);
                if (!ok) break;
                uploaded++;
            }
            return uploaded;
        }
        finally { _uploadGate.Release(); }
    }

    // ---- 編集用の初期値 ----

    public Task<ComposeInit?> OpenForEditAsync(long draftId, CancellationToken ct = default)
    {
        var row = FindDraft(draftId);
        return Task.FromResult(row is null ? null : ToInit(row));
    }

    public async Task<ComposeInit?> OpenForEditByMessageAsync(string accountId, long gmailMsgId, CancellationToken ct = default)
    {
        var uid = _mail.GetMemberships(accountId, gmailMsgId).FirstOrDefault(m => m.Folder == FolderKind.Drafts)?.Uid;

        // すでにこのメール用のローカル行があれば、それを開く（行の内容のほうが新しいことがある）
        if (uid is { } u && _drafts.GetAll(accountId).FirstOrDefault(d => d.GmailUid == u) is { } known)
            return ToInit(known);

        var message = _mail.Get(accountId, gmailMsgId);
        if (message is null) return null;

        if (message.BodyState != BodyState.Fetched)
        {
            var fetched = await _bodyFetcher.EnsureBodyAsync(accountId, gmailMsgId, ct).ConfigureAwait(false);
            if (fetched != BodyFetchResult.Ready) return null;
            message = _mail.Get(accountId, gmailMsgId) ?? message;
        }

        // 添付が 1 つでも取れないまま編集を始めると、保存したときに Gmail 側の添付を失うので開かない
        var attachments = new List<AttachmentInfo>();
        foreach (var a in _mail.GetAttachments(accountId, gmailMsgId).Where(a => !a.IsInline))
        {
            var local = a.LocalPath;
            if (string.IsNullOrEmpty(local) || !File.Exists(ResolvePath(accountId, local)))
            {
                var r = await _attachmentService.EnsureLocalAsync(accountId, a.Id, ct).ConfigureAwait(false);
                if (r.Status != AttachmentFetchStatus.Ready || string.IsNullOrEmpty(r.LocalPath)) return null;
                local = r.LocalPath;
            }
            attachments.Add(a with { LocalPath = local });
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var row = _drafts.Save(new DraftItem
            {
                AccountId = accountId, To = message.To, Cc = message.Cc, Bcc = Array.Empty<string>(),
                Subject = message.Subject, Body = message.BodyText ?? message.DerivedText ?? "",
                ReplyHeaders = string.IsNullOrWhiteSpace(message.References) ? null : new ReplyHeaders(null, message.References),
                GmailUid = uid, Dirty = false,
            });
            row = row with { Attachments = CopyAttachments(accountId, attachments, row.LocalId) };
            row = _drafts.Save(row);
            return ToInit(row);
        }
        finally { _gate.Release(); }
    }

    private static ComposeInit ToInit(DraftItem d) => new()
    {
        AccountId = d.AccountId, To = d.To, Cc = d.Cc, Subject = d.Subject, Body = d.Body,
        Attachments = d.Attachments, SourceDraftId = d.LocalId, ReplyHeaders = d.ReplyHeaders,
    };

    // ---- Gmail への反映 ----

    /// <summary>
    /// Drafts へ上げる（_uploadGate を持った状態で呼ぶ）。つながらないなどで失敗したときは Dirty のまま返す（例外にしない）。
    /// IMAP を待つ間は _gate を持たないので、その間にローカル保存が入ってもよい。
    /// 反映中に内容が変わっていたら、新しい UID だけ記録して Dirty のまま残し、次の反映でまた上げる。
    /// </summary>
    private async Task<(DraftItem? Row, bool Ok)> UploadAsync(string accountId, long localId, CancellationToken ct)
    {
        DraftItem row;
        long revision;
        byte[] raw;
        Account account;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var current = FindDraft(accountId, localId);
            if (current is null) return (null, true);
            var found = _accounts.Get(accountId);
            if (found is null) return (current, false);
            row = current;
            account = found;
            revision = RevisionOf(localId);
            try
            {
                var message = BuildMessage(row, account);
                using var ms = new MemoryStream();
                message.WriteTo(ms, ct);
                raw = ms.ToArray();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.Warn("Draft", "下書きの組み立てに失敗: " + ex.GetType().Name);
                return (row, false);
            }
        }
        finally { _gate.Release(); }

        uint? newUid;
        try
        {
            await using var gw = _gateways.Create(account);
            var folder = await FindDraftsFolderAsync(gw, ct).ConfigureAwait(false);
            var oldUid = row.GmailUid ?? FindCachedUid(row);

            // 新しい版を先に追加し、成功してから古い版を消す
            newUid = await gw.AppendAsync(folder, raw, seen: true, ct).ConfigureAwait(false);
            if (oldUid is { } old)
            {
                try { await gw.DeleteAsync(folder, new[] { old }, ct).ConfigureAwait(false); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.Warn("Draft", "古い下書きの削除に失敗: " + ex.GetType().Name);
                }
            }
            if (newUid is null) _log.Warn("Draft", "追加した下書きの UID が取れなかった");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // オフラインなど。ローカルには残っているので、次の周期か再接続後に再び試す
            _log.Warn("Draft", "下書きのアップロードに失敗: " + ex.GetType().Name);
            return (row, false);
        }

        // 追加済みの結果は取りこぼさないよう、ここからは取り消さない
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var latest = FindDraft(accountId, localId);
            if (latest is not null)
            {
                var saved = _drafts.Save(latest with { GmailUid = newUid, Dirty = RevisionOf(localId) != revision });
                return (saved, true);
            }
        }
        finally { _gate.Release(); }

        // 反映中に下書きが消された。追加した版が Gmail に残らないよう片付ける
        if (newUid is { } stray) await TryDeleteRemoteAsync(accountId, stray, CancellationToken.None).ConfigureAwait(false);
        return (null, true);
    }

    /// <summary>Gmail 側の下書きを消す。失敗しても例外にしない（つながらないときはコピーが残る）。</summary>
    private async Task TryDeleteRemoteAsync(string accountId, uint? uid, CancellationToken ct)
    {
        if (uid is null) return;
        var account = _accounts.Get(accountId);
        if (account is null) return;
        try
        {
            await using var gw = _gateways.Create(account);
            var folder = await FindDraftsFolderAsync(gw, ct).ConfigureAwait(false);
            await gw.DeleteAsync(folder, new[] { uid.Value }, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Warn("Draft", "Gmail の下書きの削除に失敗: " + ex.GetType().Name);
        }
    }

    private static async Task<string> FindDraftsFolderAsync(IImapGateway gw, CancellationToken ct)
    {
        var folders = await gw.ListFoldersAsync(ct).ConfigureAwait(false);
        return folders.FirstOrDefault(f => f.Kind == FolderKind.Drafts)?.Path
            ?? throw new InvalidOperationException("Drafts フォルダが見つからない");
    }

    /// <summary>キャッシュ上の Drafts から、この下書きの Message-ID を持つメールの UID を探す（行に UID が無いとき用）。</summary>
    private uint? FindCachedUid(DraftItem row)
    {
        var wanted = MessageIdOf(row.LocalId);
        foreach (var m in _mail.GetList(row.AccountId, FolderKind.Drafts))
        {
            if (!string.Equals(m.MessageIdHeader.Trim().Trim('<', '>'), wanted, StringComparison.OrdinalIgnoreCase)) continue;
            var membership = _mail.GetMemberships(row.AccountId, m.GmailMsgId).FirstOrDefault(x => x.Folder == FolderKind.Drafts);
            if (membership is not null) return membership.Uid;
        }
        return null;
    }

    /// <summary>下書きを MIME にする。宛先は読めるものだけ入れる（書きかけの宛先でも保存できるように）。</summary>
    private MimeMessage BuildMessage(DraftItem row, Account account)
    {
        var item = new OutboxItem
        {
            AccountId = row.AccountId, To = Readable(row.To), Cc = Readable(row.Cc), Bcc = Readable(row.Bcc),
            Subject = row.Subject, Body = row.Body, Attachments = row.Attachments, ReplyHeaders = row.ReplyHeaders,
        };
        var message = MessageBuilder.Build(item, account.Email, _paths.AccountDir(row.AccountId));
        message.MessageId = MessageIdOf(row.LocalId);
        return message;
    }

    private static IReadOnlyList<string> Readable(IReadOnlyList<string> addresses)
        => addresses.Where(a => MailboxAddress.TryParse(a, out _)).ToList();

    // ---- ローカルの補助 ----

    private DraftItem? FindDraft(long draftId)
    {
        foreach (var a in _accounts.GetAll())
            if (FindDraft(a.Id, draftId) is { } d) return d;
        return null;
    }

    private DraftItem? FindDraft(string accountId, long draftId)
        => _drafts.GetAll(accountId).FirstOrDefault(d => d.LocalId == draftId);

    private string ResolvePath(string accountId, string path)
        => Path.IsPathRooted(path) ? path : Path.Combine(_paths.AccountDir(accountId), path);

    /// <summary>
    /// 添付を &lt;アカウント&gt;\drafts\&lt;id&gt;\&lt;n&gt;.bin へ写し、相対パスの一覧を返す。元の名前は DB にだけ持つ。
    /// 元が同じフォルダの中にあっても壊さないよう、いったん別の場所へ写してから入れ替える。
    /// </summary>
    private IReadOnlyList<AttachmentInfo> CopyAttachments(string accountId, IReadOnlyList<AttachmentInfo> source, long id)
    {
        var accountDir = _paths.AccountDir(accountId);
        var relDir = Path.Combine("drafts", id.ToString());
        var dir = Path.Combine(accountDir, relDir);
        if (source.Count == 0)
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            return source;
        }

        var staging = dir + ".tmp";
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);
        var result = new List<AttachmentInfo>(source.Count);
        for (var n = 0; n < source.Count; n++)
        {
            var a = source[n];
            if (string.IsNullOrEmpty(a.LocalPath)) throw new FileNotFoundException("添付ファイルの場所が未設定");
            File.Copy(ResolvePath(accountId, a.LocalPath), Path.Combine(staging, n + ".bin"), overwrite: true);
            result.Add(a with { LocalPath = Path.Combine(relDir, n + ".bin") });
        }
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.Move(staging, dir);
        return result;
    }
}
