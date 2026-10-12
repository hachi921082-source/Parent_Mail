using ParentMail.Core.Data;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;

namespace ParentMail.Core.Features.FolderActions;

/// <summary>
/// IFolderActionService の実装。IMAP の MOVE を 1 通ずつ行い、成功したぶんだけ cache.db の所属を更新する。
/// 接続は FetchSession（アカウントごとに同時 1 本）を借りるので、まとめて削除しても接続は 1 回で済む。
/// 途中で失敗しても、すでに移したぶんは戻さない（A64）。失敗したメールは所属を変えないので、一覧を読み直せば元の場所に戻る。
/// </summary>
public sealed class FolderActionService : IFolderActionService
{
    private readonly FetchSession _session;
    private readonly MailRepository _mail;
    private readonly IDraftRepository _drafts;
    private readonly IDraftService? _draftService;
    private readonly IReadStateSync _readState;
    private readonly ISyncService? _sync;
    private readonly IClock _clock;
    private readonly IAppLog _log;

    public FolderActionService(FetchSession session, MailRepository mail, IDraftRepository drafts, IDraftService? draftService,
        IReadStateSync readState, ISyncService? sync, IClock clock, IAppLog log)
    {
        _session = session;
        _mail = mail;
        _drafts = drafts;
        _draftService = draftService;
        _readState = readState;
        _sync = sync;
        _clock = clock;
        _log = log;
    }

    public bool IsOffline(string accountId) => _sync is not null && _sync.GetState(accountId) == SyncState.Offline;

    public uint? FindUid(string accountId, long gmailMsgId, FolderKind folder)
        => _mail.GetMemberships(accountId, gmailMsgId).FirstOrDefault(m => m.Folder == folder)?.Uid;

    // ---- 削除と元に戻す ----

    public async Task<DeleteResult> DeleteAsync(string accountId, IReadOnlyList<DeleteTarget> targets, CancellationToken ct = default)
    {
        if (targets.Count > Constants.MaxDeleteBatch)
            throw new ArgumentException("delete batch is over the limit", nameof(targets));

        // ごみ箱にあるものは対象外（検索結果から選んだ場合）。同じメールが重なっても 1 回だけ移す
        var real = targets.Where(t => t.FromFolder != FolderKind.Trash).DistinctBy(t => t.GmMsgId).ToList();
        var skipped = targets.Count - real.Count;
        var move = await MoveAsync(accountId, real.Select(t => (t, FolderKind.Trash)).ToList(), ct).ConfigureAwait(false);
        move = move with { Skipped = skipped };

        UndoToken? token = null;
        if (move.Moved.Count > 0)
        {
            var expires = new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc))
                .AddSeconds(Constants.UndoNoticeSeconds);
            token = new UndoToken(accountId, move.Moved, expires);
        }
        return new DeleteResult(move, token);
    }

    public async Task<MoveResult> UndoDeleteAsync(UndoToken token, CancellationToken ct = default)
    {
        if (new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)) > token.ExpiresUtc)
            return new MoveResult(MoveStatus.Expired, Array.Empty<UndoEntry>(), 0);

        // ごみ箱での現在の UID を引く。引けないもの（移動後の UID が取れなかった）は戻せない
        var pairs = new List<(DeleteTarget, FolderKind)>();
        var unresolved = 0;
        foreach (var e in token.Entries)
        {
            var uid = FindUid(token.AccountId, e.GmMsgId, FolderKind.Trash);
            if (uid is null) { unresolved++; continue; }
            pairs.Add((new DeleteTarget(e.GmMsgId, FolderKind.Trash, uid.Value), e.FromFolder));
        }
        var result = await MoveAsync(token.AccountId, pairs, ct).ConfigureAwait(false);
        if (unresolved == 0) return result;
        var status = result.Moved.Count > 0 ? MoveStatus.Partial : (result.Status == MoveStatus.Offline ? MoveStatus.Offline : MoveStatus.Failed);
        return result with { Status = status, Failed = result.Failed + unresolved };
    }

    // ---- 1 通の移動 ----

    public Task<MoveResult> MoveToSpamAsync(string accountId, DeleteTarget target, CancellationToken ct = default)
        => MoveAsync(accountId, new[] { (target, FolderKind.Spam) }, ct);

    public Task<MoveResult> NotSpamAsync(string accountId, DeleteTarget target, CancellationToken ct = default)
        => MoveAsync(accountId, new[] { (target, FolderKind.Inbox) }, ct);

    // ごみ箱から戻す: 削除前のフォルダへ。記録がない・削除元がごみ箱・IMAP 名が未解決のときは受信トレイ
    public Task<MoveResult> RestoreAsync(string accountId, DeleteTarget target, CancellationToken ct = default)
    {
        var origin = _mail.GetTrashOrigin(accountId, target.GmMsgId);
        var to = origin is null or FolderKind.Trash || _mail.GetImapName(accountId, origin.Value) is null
            ? FolderKind.Inbox : origin.Value;
        return MoveAsync(accountId, new[] { (target, to) }, ct);
    }

    // ---- 既読・未読 ----

    public async Task<bool?> ToggleReadAsync(string accountId, long gmailMsgId, CancellationToken ct = default)
    {
        var mail = _mail.Get(accountId, gmailMsgId);
        if (mail is null) return null;
        var now = !mail.IsRead;
        await _readState.SetSeenAsync(accountId, gmailMsgId, now, ct).ConfigureAwait(false);
        return now;
    }

    public async Task DeleteLocalDraftAsync(long draftId, CancellationToken ct = default)
    {
        if (_draftService is not null) await _draftService.DeleteAsync(draftId, ct).ConfigureAwait(false);
        else _drafts.Remove(draftId);
    }

    // ---- 共通の移動処理 ----

    private async Task<MoveResult> MoveAsync(string accountId, IReadOnlyList<(DeleteTarget Target, FolderKind To)> pairs, CancellationToken ct)
    {
        if (pairs.Count == 0) return MoveResult.Empty;
        if (IsOffline(accountId)) return new MoveResult(MoveStatus.Offline, Array.Empty<UndoEntry>(), pairs.Count);

        var moved = new List<UndoEntry>();
        var failed = 0;
        var networkLost = false;

        var (outcome, _) = await _session.RunAsync<bool>(accountId, async gw =>
        {
            for (var i = 0; i < pairs.Count; i++)
            {
                var (t, to) = pairs[i];
                var fromPath = _mail.GetImapName(accountId, t.FromFolder);
                var toPath = _mail.GetImapName(accountId, to);
                if (fromPath is null || toPath is null) { failed++; continue; }
                try
                {
                    var newUid = await gw.MoveAsync(fromPath, t.Uid, toPath, ct).ConfigureAwait(false);
                    ApplyLocal(accountId, t, to, newUid);
                    moved.Add(new UndoEntry(t.GmMsgId, t.FromFolder, t.Uid));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 内容は記録せず例外の型だけ残す
                    _log.Warn("FolderActions", "Move failed: " + ex.GetType().Name);
                    if (SyncService.IsNetworkError(ex))
                    {
                        // 通信が切れた: この 1 通と残りはすべて失敗として数える
                        networkLost = true;
                        failed += pairs.Count - i;
                        break;
                    }
                    failed++;
                }
            }
            return true;
        }, ct).ConfigureAwait(false);

        // 接続できなかった・認証に失敗した・ローカル保存で例外が出た場合は、移せなかった残りをまとめて失敗にする
        failed = pairs.Count - moved.Count;
        var status = failed == 0 ? MoveStatus.Ok
            : moved.Count > 0 ? MoveStatus.Partial
            : (outcome == FetchOutcome.Offline || networkLost) ? MoveStatus.Offline
            : MoveStatus.Failed;
        return new MoveResult(status, moved, failed);
    }

    // 成功した 1 通のローカル更新: 元のフォルダの所属を外し、移動先の所属（新しい UID）を足す
    private void ApplyLocal(string accountId, DeleteTarget t, FolderKind to, uint? newUid)
    {
        _mail.RemoveMembership(accountId, t.GmMsgId, t.FromFolder);
        // ごみ箱へ移したら削除元を記録し、ごみ箱から出たら（戻す・元に戻す）記録を消す
        if (to == FolderKind.Trash) _mail.SetTrashOrigin(accountId, t.GmMsgId, t.FromFolder);
        else if (t.FromFolder == FolderKind.Trash) _mail.ClearTrashOrigin(accountId, t.GmMsgId);
        if (newUid is { } uid) _mail.AddMembership(accountId, new MessageFolder(t.GmMsgId, to, uid, 0));

        // 下書きをごみ箱へ移したら、対応するローカル下書き行も消す（同期のあとに再び現れないように）
        if (t.FromFolder == FolderKind.Drafts && to == FolderKind.Trash)
        {
            foreach (var d in _drafts.GetAll(accountId).Where(d => d.GmailUid == t.Uid).ToList())
                _drafts.Remove(d.LocalId);
        }
    }
}
