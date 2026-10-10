using System.Net.NetworkInformation;

namespace ParentMail.Core.Features.SyncCache;

/// <summary>
/// 同期の起動役。アプリ起動時に全アカウントを同期し、その後 5 分ごとに繰り返す。
/// 更新ボタン（RequestRefresh）と、ネットワーク復帰時の再試行も受け持つ。実行中の重複は ISyncService 側で処理される。
/// </summary>
public sealed class SyncCoordinator : IDisposable
{
    private readonly ISyncService _sync;
    private readonly IAccountRepository _accounts;
    private readonly IAppLog _log;
    private readonly TimeSpan _interval;
    private Timer? _timer;
    private bool _started;

    public SyncCoordinator(ISyncService sync, IAccountRepository accounts, IAppLog log, TimeSpan? interval = null)
    {
        _sync = sync;
        _accounts = accounts;
        _log = log;
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

    /// <summary>更新ボタン。実行中なら合流する（二重には走らない）。</summary>
    public void RequestRefresh(string accountId) => _ = SyncOneAsync(accountId, SyncTrigger.Manual);

    /// <summary>全アカウントを同期する（タイマーの 1 回分）。</summary>
    public async Task SyncAllAsync(SyncTrigger trigger)
    {
        try
        {
            await Task.WhenAll(_accounts.GetAll().Select(a => SyncOneAsync(a.Id, trigger))).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // タイマーやネットワーク通知から呼ばれるため、例外でプロセスを落とさない
            _log.Error("Sync", "Sync all failed", ex);
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
