using Microsoft.Extensions.DependencyInjection;
using ParentMail.App;
using ParentMail.App.Features.AppLifecycle;

namespace ParentMail.Tests.AppLifecycle;

// FeatureRegistrars の一覧から、app-lifecycle のサービスが本物の実装で登録されていることを確かめる
// （RealServiceWiringTests と同じ考え方。仮実装が本物を隠す不具合の再発防止）。
public class AppLifecycleWiringTests
{
    private static ServiceDescriptor Effective<T>()
    {
        var services = new ServiceCollection();
        foreach (var register in FeatureRegistrars.All) register(services);
        // 解決時は最後の登録が使われる
        return services.Last(d => d.ServiceType == typeof(T));
    }

    [Fact]
    public void Quit_services_resolve_to_real_implementations()
    {
        Assert.Equal(typeof(ComposeQuitAdapter), Effective<IQuitComposeWindows>().ImplementationType);
        Assert.Equal(typeof(DialogQuitPrompt), Effective<IQuitPrompt>().ImplementationType);
        Assert.Equal(typeof(QuitFinalizer), Effective<IQuitFinalizer>().ImplementationType);
        Assert.Equal(typeof(QuitCoordinator), Effective<QuitCoordinator>().ImplementationType);
        Assert.Equal(typeof(SessionEndingHandler), Effective<SessionEndingHandler>().ImplementationType);
    }

    [Fact]
    public void Startup_services_resolve_to_real_implementations()
    {
        Assert.Equal(typeof(StartupSteps), Effective<IStartupSteps>().ImplementationType);
        Assert.Equal(typeof(StartupCoordinator), Effective<StartupCoordinator>().ImplementationType);
    }

    [Fact]
    public void AddAppLifecycle_is_registered_once_in_feature_registrars()
    {
        var services = new ServiceCollection();
        foreach (var register in FeatureRegistrars.All) register(services);
        Assert.Single(services, d => d.ServiceType == typeof(QuitCoordinator));
    }
}
