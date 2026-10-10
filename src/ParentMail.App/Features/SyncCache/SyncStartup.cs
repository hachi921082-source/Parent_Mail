using ParentMail.App.Features.Shell;
using ParentMail.Core.Features.SyncCache;

namespace ParentMail.App.Features.SyncCache;

/// <summary>
/// 同期の起動点。アプリ起動後に Start() を 1 回呼ぶと、画面への反映を結び、全アカウントの同期と 5 分ごとのタイマーを始める。
/// </summary>
public sealed class SyncStartup
{
    private readonly SyncUiBinder _binder;
    private readonly SyncCoordinator _coordinator;
    private readonly MainViewModel _vm;

    public SyncStartup(SyncUiBinder binder, SyncCoordinator coordinator, MainViewModel vm)
    {
        _binder = binder;
        _coordinator = coordinator;
        _vm = vm;
    }

    public void Start()
    {
        _binder.Attach(_vm);
        _coordinator.Start();
    }
}
