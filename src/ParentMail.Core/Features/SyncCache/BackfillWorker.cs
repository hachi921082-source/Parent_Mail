namespace ParentMail.Core.Features.SyncCache;

/// <summary>
/// アイドル時の本文の先読み（sync-cache PLAN-3、Q67、A57）。
/// どの同期も走っておらず、60 秒操作がなく、オンラインのときだけ、本文が未取得のメールを新しい順に 1 周期 20 件まで取る。
/// 利用者の操作か同期の開始があれば、その場で止める。取れなかったメールは控えに積まない（先読みは自動再試行しない）。
/// </summary>
public sealed class BackfillWorker : IDisposable
{
    private readonly IIdleMonitor _idle;
    private readonly ISyncService _sync;
    private readonly IAccountRepository _accounts;
    private readonly ISyncCacheStore _store;
    private readonly BodyFetcher _fetcher;
    private readonly IAppLog _log;
    private readonly TimeSpan _idleThreshold;
    private readonly int _perCycle;
    private readonly TimeSpan _tick;

    private readonly object _lock = new();
    private readonly HashSet<(string, long)> _failed = new();
    private Timer? _timer;
    private CancellationTokenSource? _runCts;
    private bool _busy;

    public BackfillWorker(IIdleMonitor idle, ISyncService sync, IAccountRepository accounts, ISyncCacheStore store,
        BodyFetcher fetcher, IAppLog log, TimeSpan? idleThreshold = null, int? perCycle = null, TimeSpan? tick = null)
    {
        _idle = idle;
        _sync = sync;
        _accounts = accounts;
        _store = store;
        _fetcher = fetcher;
        _log = log;
        _idleThreshold = idleThreshold ?? TimeSpan.FromSeconds(Constants.IdleBackfillIdleSeconds);
        _perCycle = perCycle ?? Constants.IdleBackfillBodiesPerCycle;
        _tick = tick ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>定期的に条件を見て、満たしていれば 1 周期ぶん取りにいく。2 回目以降は何もしない。</summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_timer is not null) return;
            _timer = new Timer(_ => _ = TickAsync(), null, _tick, _tick);
        }
    }

    /// <summary>タイマーを止め、走っている周期があれば中断する。</summary>
    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            cts = _runCts;
        }
        try { cts?.Cancel(); } catch (ObjectDisposedException) { }
    }

    private async Task TickAsync()
    {
        lock (_lock)
        {
            if (_busy || _timer is null) return;
            _busy = true;
        }
        try
        {
            await RunCycleAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warn("Backfill", "Cycle failed: " + ex.GetType().Name);
        }
        finally
        {
            lock (_lock) _busy = false;
        }
    }

    /// <summary>1 周期ぶんの先読み。取得できた件数を返す。条件を満たさなければ 0。</summary>
    public async Task<int> RunCycleAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        void Cancel()
        {
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
        }
        void OnActivity(object? s, EventArgs e) => Cancel();
        void OnState(object? s, AccountEventArgs e)
        {
            if (_sync.GetState(e.AccountId) == SyncState.Syncing) Cancel();
        }

        // 条件の確認より先に購読して、確認と開始のあいだの操作や同期開始を取りこぼさない
        _idle.UserActivity += OnActivity;
        _sync.StateChanged += OnState;
        lock (_lock) _runCts = cts;
        var fetched = 0;
        try
        {
            var accounts = _accounts.GetAll();
            if (_idle.IdleFor < _idleThreshold) return 0;
            if (accounts.Any(a => _sync.GetState(a.Id) == SyncState.Syncing)) return 0;

            foreach (var account in accounts)
            {
                // 通信できない・パスワード不正のアカウントは飛ばす
                var state = _sync.GetState(account.Id);
                if (state is SyncState.Offline or SyncState.Error) continue;

                List<long> ids;
                lock (_lock)
                    ids = _store.GetBodylessMessageIds(account.Id, _perCycle - fetched + _failed.Count)
                        .Where(id => !_failed.Contains((account.Id, id))).ToList();

                foreach (var id in ids)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    var result = await _fetcher.EnsureBodyCoreAsync(account.Id, id, registerPending: false, cts.Token).ConfigureAwait(false);
                    switch (result)
                    {
                        case BodyFetchResult.Ready:
                            fetched++;
                            break;
                        case BodyFetchResult.Offline:
                            return fetched; // 通信できない: この周期は終える
                        default:
                            lock (_lock) _failed.Add((account.Id, id));
                            break;
                    }
                    if (fetched >= _perCycle) return fetched;
                }
            }
            return fetched;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return fetched; // 利用者の操作か同期の開始で中断した（取れた分は保存済み）
        }
        finally
        {
            _idle.UserActivity -= OnActivity;
            _sync.StateChanged -= OnState;
            lock (_lock) _runCts = null;
        }
    }

    public void Dispose() => Stop();
}
