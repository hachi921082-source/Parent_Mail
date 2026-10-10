using ParentMail.Core.Data;
using ParentMail.Core.Models;

namespace ParentMail.Core.Features.SyncCache;

/// <summary>本文を取得して保存したときの通知（表示中のプレビューを更新する契機）。</summary>
public sealed class BodyFetchedEventArgs(string accountId, long gmailMsgId) : EventArgs
{
    public string AccountId { get; } = accountId;
    public long GmailMsgId { get; } = gmailMsgId;
}

/// <summary>
/// 古いメールの本文のオンデマンド取得（sync-cache PLAN-3）。開いたときに呼ぶ。
/// 取得済みなら通信しない。オフラインなら Offline を返して控え（PendingFetchTracker）に積み、次の同期成功後に自動で取り直す。
/// オンラインでの失敗は Failed で、控えには積まない。ルールと検索はこのクラスを呼ばない（D12、Q30）。
/// </summary>
public sealed class BodyFetcher : IBodyFetcher
{
    private readonly MailRepository _mail;
    private readonly IAttachmentStore _files;
    private readonly FetchSession _session;
    private readonly PendingFetchTracker _pending;
    private readonly IAppLog _log;

    public BodyFetcher(MailRepository mail, IAttachmentStore files, FetchSession session, PendingFetchTracker pending, IAppLog log)
    {
        _mail = mail;
        _files = files;
        _session = session;
        _pending = pending;
        _log = log;
    }

    /// <summary>本文を保存し終えたとき（取得済みで通信しなかった場合は出さない）。</summary>
    public event EventHandler<BodyFetchedEventArgs>? BodyFetched;

    public Task<BodyFetchResult> EnsureBodyAsync(string accountId, long gmailMsgId, CancellationToken ct = default)
        => EnsureBodyCoreAsync(accountId, gmailMsgId, registerPending: true, ct);

    /// <summary>先読みはオフラインでも控えに積まない（registerPending=false）。</summary>
    internal async Task<BodyFetchResult> EnsureBodyCoreAsync(string accountId, long gmailMsgId, bool registerPending, CancellationToken ct)
    {
        var current = _mail.Get(accountId, gmailMsgId);
        if (current is null) return BodyFetchResult.Failed;
        if (current.BodyState == BodyState.Fetched) return BodyFetchResult.Ready;

        var stored = false;
        var (outcome, value) = await _session.RunAsync(accountId, async gw =>
        {
            // 待っている間に別の経路で取得済みになっていれば、通信せずに終える
            var again = _mail.Get(accountId, gmailMsgId);
            if (again is null) return BodyFetchResult.Failed;
            if (again.BodyState == BodyState.Fetched) return BodyFetchResult.Ready;

            // どのフォルダの UID でもよいが、AllMail は最後に回す
            var memberships = _mail.GetMemberships(accountId, gmailMsgId)
                .OrderBy(m => m.Folder == FolderKind.AllMail).ThenBy(m => (int)m.Folder).ToList();
            foreach (var m in memberships)
            {
                var path = _mail.GetImapName(accountId, m.Folder);
                if (path is null) continue;
                var body = await gw.FetchBodyAsync(path, m.Uid, ct).ConfigureAwait(false);
                if (body is null) continue;

                var derived = body.Html is null ? null : BodyParser.HtmlToText(body.Html);
                _mail.SetBody(accountId, gmailMsgId, body.Text, body.Html, derived);
                if (body.Attachments.Count > 0)
                {
                    var saved = _mail.SaveAttachments(accountId, gmailMsgId, body.Attachments);
                    await FetchInlinePartsAsync(gw, accountId, path, m.Uid, saved, ct).ConfigureAwait(false);
                }
                stored = true;
                return BodyFetchResult.Ready;
            }
            return BodyFetchResult.Failed;
        }, ct).ConfigureAwait(false);

        switch (outcome)
        {
            case FetchOutcome.Offline:
                if (registerPending) _pending.Add(accountId, gmailMsgId);
                return BodyFetchResult.Offline;
            case FetchOutcome.Failed:
                return BodyFetchResult.Failed;
        }

        if (stored) RaiseFetched(accountId, gmailMsgId);
        return value;
    }

    /// <summary>cid 参照の埋め込み画像（5 MB 以下）を本文と一緒に取る。失敗しても本文の取得は成功のまま（画像は表示時に再試行できる）。</summary>
    private async Task FetchInlinePartsAsync(IImapGateway gw, string accountId, string path, uint uid,
        IReadOnlyList<AttachmentInfo> attachments, CancellationToken ct)
    {
        foreach (var a in attachments)
        {
            if (!a.IsInline || a.LocalPath is not null || a.Size > Constants.IdleBackfillInlinePartMaxBytes) continue;
            try
            {
                var stream = await gw.FetchPartAsync(path, uid, a.PartId, ct).ConfigureAwait(false);
                if (stream is null) continue;
                await using var _ = stream;
                if (stream.CanSeek && stream.Length > Constants.IdleBackfillInlinePartMaxBytes) continue;
                var relative = _files.SaveFile(accountId, a.Id, stream);
                _mail.SetAttachmentPath(accountId, a.Id, relative);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Info("Fetch", "Inline part skipped: " + ex.GetType().Name);
                if (SyncService.IsNetworkError(ex)) return; // 通信が切れているなら残りも試さない
            }
        }
    }

    private void RaiseFetched(string accountId, long gmailMsgId)
    {
        try { BodyFetched?.Invoke(this, new BodyFetchedEventArgs(accountId, gmailMsgId)); }
        catch (Exception ex) { _log.Warn("Fetch", "BodyFetched handler failed: " + ex.GetType().Name); }
    }
}
