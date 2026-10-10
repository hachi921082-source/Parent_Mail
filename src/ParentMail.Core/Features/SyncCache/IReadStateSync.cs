namespace ParentMail.Core.Features.SyncCache;

/// <summary>
/// 既読・未読の書き込み口（sync-cache PLAN-2 の SetSeenAsync）。
/// ローカルの既読状態をすぐ更新し、オンラインなら Gmail の全所属フォルダに \Seen を反映する。
/// オフラインや同期中は pending_ops に積み、次の同期で反映する。
/// </summary>
public interface IReadStateSync
{
    Task SetSeenAsync(string accountId, long gmailMsgId, bool seen, CancellationToken ct = default);
}
