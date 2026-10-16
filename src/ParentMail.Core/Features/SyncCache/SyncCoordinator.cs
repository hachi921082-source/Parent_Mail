using System.Net.NetworkInformation;

namespace ParentMail.Core.Features.SyncCache;

/// <summary>
/// 同期の起動役。アプリ起動時に全アカウントを同期し、その後 5 分ごとに繰り返す。
/// 更新ボタン（RequestRefresh）と、ネットワーク復帰時の再試行も受け持つ。
/// 負荷を抑えるため、同期は常に 1 アカウントずつ順に行う（並列にしない、A89）。
/// 順番は表示中のアカウントが先頭、残りは登録順。実行中に来たタイマーなどの一括同期は丸ごと見送る。
/// </summary>
public sealed class SyncCoordinator : IDisposable
{
    private readonly ISyncService _sync;
    private readonly IAccountRepository _accounts;
    private readonly IAppLog _log;
    private readonly IAppState? _state;
    private readonly TimeSpan _interval;
    private readonly object _lock = new();
    // 待ち行列（先頭から順に実行）。_running は同期の実行役が動いている間 true。_current は実行中のアカウント
    private readonly List<(string AccountId, SyncTrigger Trigger)> _queue = new();
    private bool _running;
    private string? _current;
    private Timer? _timer;
    private bool _started;

    public SyncCoordinator(ISyncService sync, IAccountRepository accounts, IAppLog log, TimeSpan? interval = null,
        IAppState? state = null)
    {
        _sync = sync;
        _accounts = accounts;
        _log = log;
        _state = state;
        _interval = interval ?? TimeSpan.FromMinutes(Constants.SyncIntervalMinutes);
    }

    /// <summary>起動時の同期を始め、定期同期のタイマーを動かす。2 回目以降は何もしない。</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        _ = SyncAllAsync(SyncTrigger.Startup);
        _timer = new Timer(_ => _ = SyncAllAsync(SyncTrigger.Timer), null, _interval, _interval);
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// 更新ボタン。実行中の同じアカウントには合流する（二重には走らない）。
    /// 待ち行列にいれば先頭へ移し、いなければ先頭に足す。何も走っていなければそのアカウントだけ同期する。
    /// </summary>
    public void RequestRefresh(string accountId)
    {
        bool start;
        lock (_lock)
        {
            if (_running && _current == accountId) return;
            _queue.RemoveAll(q => q.AccountId == accountId);
            _queue.Insert(0, (accountId, SyncTrigger.Manual));
            start = !_running;
            _running = true;
        }
        if (start) _ = RunQueueAsync();
    }

    /// <summary>全アカウントを順に同期する（タイマーの 1 回分）。すでに走っている間は何もせず戻る。</summary>
    public async Task SyncAllAsync(SyncTrigger trigger)
    {
        try
        {
            var all = _accounts.GetAll();
            var displayed = _state?.CurrentAccountId;
            var ordered = all.Where(a => a.Id == displayed).Concat(all.Where(a => a.Id != displayed)).ToList();
            lock (_lock)
            {
                if (_running) return;
                _queue.Clear();
                foreach (var a in ordered) _queue.Add((a.Id, trigger));
                _running = true;
            }
            await RunQueueAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // タイマーやネットワーク通知から呼ばれるため、例外でプロセスを落とさない
            _log.Error("Sync", "Sync all failed", ex);
        }
    }

    // 待ち行列が空になるまで 1 件ずつ同期する。_running を呼び出し側が立ててから呼ぶこと
    private async Task RunQueueAsync()
    {
        while (true)
        {
            (string AccountId, SyncTrigger Trigger) item;
            lock (_lock)
            {
                if (_queue.Count == 0)
                {
                    _running = false;
                    _current = null;
                    return;
                }
                item = _queue[0];
                _queue.RemoveAt(0);
                _current = item.AccountId;
            }
            await SyncOneAsync(item.AccountId, item.Trigger).ConfigureAwait(false);
        }
    }

    private async Task SyncOneAsync(string accountId, SyncTrigger trigger)
    {
        try
        {
            await _sync.SyncNowAsync(accountId, trigger).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error("Sync", "Sync trigger failed", ex);
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable) _ = SyncAllAsync(SyncTrigger.Reconnect);
    }

    public void Dispose() => Stop();
}
