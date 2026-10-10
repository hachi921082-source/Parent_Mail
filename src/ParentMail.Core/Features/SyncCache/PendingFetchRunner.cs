namespace ParentMail.Core.Features.SyncCache;

/// <summary>
/// 同期が成功してオンラインとわかったとき、オフライン中に取れなかった本文（PendingFetchTracker の控え）を自動で取り直す。
/// 取れたものは BodyFetcher.BodyFetched で知らせ、表示中ならプレビューが更新される。
/// まだオフラインなら BodyFetcher が控えに積み直すので、次の同期成功でまた試す。
/// </summary>
public sealed class PendingFetchRunner : IDisposable
{
    private readonly ISyncService _sync;
    private readonly IBodyFetcher _fetcher;
    private readonly PendingFetchTracker _pending;
    private readonly IAppLog _log;
    private bool _attached;

    public PendingFetchRunner(ISyncService sync, IBodyFetcher fetcher, PendingFetchTracker pending, IAppLog log)
    {
        _sync = sync;
        _fetcher = fetcher;
        _pending = pending;
        _log = log;
    }

    public void Attach()
    {
        if (_attached) return;
        _attached = true;
        _sync.SyncCompleted += OnSyncCompleted;
    }

    public void Detach()
    {
        if (!_attached) return;
        _attached = false;
        _sync.SyncCompleted -= OnSyncCompleted;
    }

    private void OnSyncCompleted(object? sender, AccountEventArgs e) => _ = RunAsync(e.AccountId);

    /// <summary>控えを取り出して順に取得する。例外でプロセスを落とさない。</summary>
    public async Task RunAsync(string accountId, CancellationToken ct = default)
    {
        try
        {
            foreach (var id in _pending.Drain(accountId))
            {
                ct.ThrowIfCancellationRequested();
                await _fetcher.EnsureBodyAsync(accountId, id, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _log.Warn("Fetch", "Pending fetch failed: " + ex.GetType().Name);
        }
    }

    public void Dispose() => Detach();
}
