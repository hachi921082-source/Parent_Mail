using ParentMail.Core;

namespace ParentMail.App.Features.Compose;

/// <summary>
/// 同期が終わるたびに、Gmail へ上げられていない下書き（オフライン中に保存したもの）をまとめて上げる（compose PLAN-2, A12）。
/// Attach() は何度呼んでも 1 回だけ結ぶ。起動時の結線（app-lifecycle）か、最初の作成窓を開くときに呼ばれる。
/// </summary>
public sealed class DraftSyncHook
{
    private readonly ISyncService _sync;
    private readonly IDraftService _drafts;
    private readonly IAppLog _log;
    private int _attached;

    public DraftSyncHook(ISyncService sync, IDraftService drafts, IAppLog log)
    {
        _sync = sync;
        _drafts = drafts;
        _log = log;
    }

    public void Attach()
    {
        if (Interlocked.Exchange(ref _attached, 1) == 1) return;
        _sync.SyncCompleted += OnSyncCompleted;
    }

    private void OnSyncCompleted(object? sender, AccountEventArgs e)
    {
        // 同期を待たせないよう、別のタスクで流す
        _ = Task.Run(async () =>
        {
            try { await _drafts.FlushDirtyAsync(e.AccountId).ConfigureAwait(false); }
            catch (Exception ex) { _log.Error("Draft", "未アップロードの下書きの送り出しに失敗", ex); }
        });
    }
}
