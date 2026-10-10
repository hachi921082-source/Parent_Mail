using ParentMail.Core.Features.SyncCache;

namespace ParentMail.App.Features.SyncCache;

/// <summary>
/// 本文のオンデマンド取得まわりの起動点。UI スレッドから Start() を 1 回呼ぶと、
/// 入力の監視、再接続後の自動取得、アイドル時の先読みが動き出す。SyncStartup.Start() の直後に呼ぶ。
/// </summary>
public sealed class OnDemandFetchStartup
{
    private readonly InputIdleMonitor _idle;
    private readonly PendingFetchRunner _pending;
    private readonly BackfillWorker _backfill;

    public OnDemandFetchStartup(InputIdleMonitor idle, PendingFetchRunner pending, BackfillWorker backfill)
    {
        _idle = idle;
        _pending = pending;
        _backfill = backfill;
    }

    public void Start()
    {
        _idle.Attach();
        _pending.Attach();
        _backfill.Start();
    }
}
