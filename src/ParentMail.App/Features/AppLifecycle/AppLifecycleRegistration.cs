using Microsoft.Extensions.DependencyInjection;

namespace ParentMail.App.Features.AppLifecycle;

/// <summary>app-lifecycle の DI 登録（起動手順、終了手順、Windows の終了への対応）。</summary>
public static class AppLifecycleRegistration
{
    public static IServiceCollection AddAppLifecycle(this IServiceCollection services)
    {
        services.AddSingleton<IQuitComposeWindows, ComposeQuitAdapter>();
        services.AddSingleton<IQuitPrompt, DialogQuitPrompt>();
        services.AddSingleton<IQuitFinalizer, QuitFinalizer>();
        services.AddSingleton<QuitCoordinator>();
        services.AddSingleton<SessionEndingHandler>();
        services.AddSingleton<IStartupSteps, StartupSteps>();
        services.AddSingleton<StartupCoordinator>();
        return services;
    }
}
