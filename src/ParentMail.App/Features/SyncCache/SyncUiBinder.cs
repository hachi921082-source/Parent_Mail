using System.Windows;
using ParentMail.App.Features.Shell;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;

namespace ParentMail.App.Features.SyncCache;

/// <summary>
/// 同期の状態を画面に反映する。表示中のアカウントについて、帯（オフライン・読み込み中・受信失敗）と
/// 更新ボタンの「更新中」表示を切り替え、更新ボタンの押下を同期の起動に結びつける。
/// 帯は INotifier 経由なのでどのスレッドから呼んでもよいが、更新ボタンの表示は UI スレッドで更新する。
/// </summary>
public sealed class SyncUiBinder
{
    private readonly SyncService _sync;
    private readonly SyncCoordinator _coordinator;
    private readonly ISyncCacheStore _store;
    private readonly INotifier _notifier;
    private readonly IAppState? _state;
    private readonly Action<Action> _dispatch;
    private MainViewModel? _vm;
    private bool _otherBannerShown;

    public SyncUiBinder(SyncService sync, SyncCoordinator coordinator, ISyncCacheStore store, INotifier notifier,
        IAppState? state = null, Action<Action>? dispatch = null)
    {
        _sync = sync;
        _coordinator = coordinator;
        _store = store;
        _notifier = notifier;
        _state = state;
        _dispatch = dispatch ?? DispatchToUi;
    }

    private static void DispatchToUi(Action action)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) action();
        else d.Invoke(action);
    }

    /// <summary>イベントをつなぐ。vm があれば更新ボタンも結びつける。</summary>
    public void Attach(MainViewModel? vm = null)
    {
        _vm = vm;
        _sync.StateChanged += (_, e) => { if (e.AccountId == _state?.CurrentAccountId) Refresh(); };
        if (_state is not null) _state.CurrentAccountChanged += (_, _) => Refresh();
        if (vm is not null) vm.RefreshRequested += (_, _) => RequestRefresh();
        Refresh();
    }

    /// <summary>更新ボタン: 表示中のアカウントを今すぐ同期する。</summary>
    public void RequestRefresh()
    {
        if (_state?.CurrentAccountId is { } id) _coordinator.RequestRefresh(id);
    }

    /// <summary>表示中のアカウントの状態から帯と更新ボタンを決め直す。</summary>
    public void Refresh()
    {
        var id = _state?.CurrentAccountId;
        var state = id is null ? SyncState.Idle : _sync.GetState(id);

        if (state == SyncState.Offline) _notifier.ShowBanner(BannerKind.Offline, Strings.BN_01);
        else _notifier.HideBanner(BannerKind.Offline);

        if (id is not null && state == SyncState.Syncing && !_store.IsInitialSyncDone(id))
            _notifier.ShowBanner(BannerKind.InitialSync, Strings.BN_02);
        else _notifier.HideBanner(BannerKind.InitialSync);

        // パスワード不正の赤い帯は accounts-auth が出す。ここは「それ以外の受信失敗」だけ
        var failed = id is not null && state == SyncState.Error && !_sync.IsAuthFailed(id);
        if (failed)
        {
            _notifier.ShowBanner(BannerKind.Other, Strings.BN_19);
            _otherBannerShown = true;
        }
        else if (_otherBannerShown)
        {
            _notifier.HideBanner(BannerKind.Other);
            _otherBannerShown = false;
        }

        var vm = _vm;
        if (vm is not null) _dispatch(() => vm.IsRefreshing = state == SyncState.Syncing);
    }
}
