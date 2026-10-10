using System.Windows;
using Microsoft.Extensions.DependencyInjection;
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

        var window = Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _guard?.Dispose();
        base.OnExit(e);
    }

    private void ActivateMainWindow() => (MainWindow as MainWindow)?.BringToFront();
}
