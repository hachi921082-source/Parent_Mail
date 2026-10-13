using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.App.Features.AppLifecycle;
using ParentMail.App.Features.Shell;
using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.App;

// アプリ起動点（DI 構成ルート）。各機能の登録は FeatureRegistrars に集約する。
public partial class App : Application
{
    private SingleInstanceGuard? _guard;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 2 つ目の起動は、1 つ目の窓を前面に出して終了する。
        _guard = new SingleInstanceGuard();
        if (!_guard.TryAcquire(() => Dispatcher.BeginInvoke(ActivateMainWindow)))
        {
            _guard.SignalExisting();
            _guard.Dispose();
            _guard = null;
            Shutdown();
            return;
        }

        var services = new ServiceCollection();
        foreach (var register in FeatureRegistrars.All)
        {
            register(services);
        }
        Services = services.BuildServiceProvider();

        Services.GetRequiredService<GlobalErrorHandler>().Attach(this);
        Services.GetRequiredService<IAppLog>().Info("App", "startup");

        // Windows のサインアウト・シャットダウン時も、窓を閉じるときと同じ終了手順を通す
        var sessionEnding = Services.GetRequiredService<SessionEndingHandler>();
        SessionEnding += (_, args) => sessionEnding.Handle(args, MainWindow);

        // 主窓の表示、アカウントの選択、同期の開始（初回起動のアカウント追加は accounts-auth が出す）
        Services.GetRequiredService<StartupCoordinator>().RunAsync().GetAwaiter().GetResult();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _guard?.Dispose();
        // 同期やDB接続など、破棄が必要なサービスを閉じる
        try { (Services as IDisposable)?.Dispose(); }
        catch { /* 終了処理の失敗でプロセスを残さない */ }
        base.OnExit(e);
    }

    private void ActivateMainWindow() => (MainWindow as MainWindow)?.BringToFront();
}
