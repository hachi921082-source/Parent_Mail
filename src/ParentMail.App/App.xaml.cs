using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace ParentMail.App;

// アプリ起動点（DI 構成ルート）。各機能の登録は FeatureRegistrars に集約する。
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<MainWindow>();
        foreach (var register in FeatureRegistrars.All)
        {
            register(services);
        }
        Services = services.BuildServiceProvider();

        Services.GetRequiredService<MainWindow>().Show();
    }
}
