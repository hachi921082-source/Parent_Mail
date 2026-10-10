using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParentMail.App.Services;
using ParentMail.Core;
using ParentMail.Core.Platform;

namespace ParentMail.App.Features.Shell;

public static class AppShellRegistration
{
    /// <summary>app-shell が提供するサービスと主窓の登録。</summary>
    public static IServiceCollection AddAppShell(this IServiceCollection services)
    {
        services.AddSingleton<AppPaths>();
        services.AddSingleton<IAppLog, AppLog>(sp => new AppLog(sp.GetRequiredService<AppPaths>()));
        services.AddSingleton<NotifierService>();
        services.AddSingleton<INotifier>(sp => sp.GetRequiredService<NotifierService>());
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<GlobalErrorHandler>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        // settings レーンが実装するまでの仮実装。後から AddSingleton された登録が優先される。
        services.TryAddSingleton<ISettingsStore, InMemorySettingsStore>();
        services.TryAddSingleton<ITextScale, DefaultTextScale>();
        services.TryAddSingleton<IClock, SystemClock>();
        return services;
    }

    private sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime LocalNow => DateTime.Now;
    }
}
