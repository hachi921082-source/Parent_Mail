using Microsoft.Extensions.DependencyInjection;
using ParentMail.Core.SafetyWarnings;

namespace ParentMail.App.SafetyWarnings;

// safety-warnings の DI 登録（FeatureRegistrars から 1 行で呼ぶ）。
public static class SafetyWarningsServices
{
    public static IServiceCollection AddSafetyWarnings(this IServiceCollection services)
    {
        services.AddSingleton<IExternalLauncher, ShellExternalLauncher>();
        services.AddSingleton<LinkClickHandler>();
        return services;
    }
}
