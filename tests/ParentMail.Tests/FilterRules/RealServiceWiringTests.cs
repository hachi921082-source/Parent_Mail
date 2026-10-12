using Microsoft.Extensions.DependencyInjection;
using ParentMail.App;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;

namespace ParentMail.Tests.FilterRules;

// 仮実装（Unavailable*）が本物の登録を隠していないかを、FeatureRegistrars の一覧そのものから確かめる。
// 過去に MailDisplay の TryAddSingleton が本物より先に仮実装を登録し、本文取得・添付・作成窓が動かなかった不具合の再発防止。
public class RealServiceWiringTests
{
    private static ServiceDescriptor Effective<T>()
    {
        var services = new ServiceCollection();
        foreach (var register in FeatureRegistrars.All) register(services);
        // 解決時は最後の登録が使われる
        return services.Last(d => d.ServiceType == typeof(T));
    }

    private static void AssertNotUnavailable(ServiceDescriptor d)
    {
        Assert.DoesNotContain("Unavailable", d.ImplementationType?.Name ?? "");
        Assert.DoesNotContain("Unavailable", d.ImplementationInstance?.GetType().Name ?? "");
    }

    [Fact]
    public void BodyFetcher_resolves_to_real_implementation()
    {
        var services = new ServiceCollection();
        foreach (var register in FeatureRegistrars.All) register(services);
        var d = Effective<IBodyFetcher>();
        AssertNotUnavailable(d);
        // 実装は BodyFetcher 本体への転送ファクトリ。BodyFetcher 自体が登録されていること
        Assert.NotNull(d.ImplementationFactory);
        Assert.Contains(services, s => s.ServiceType == typeof(BodyFetcher));
    }

    [Fact]
    public void AttachmentService_resolves_to_real_implementation()
        => Assert.Equal(typeof(AttachmentService), Effective<IAttachmentService>().ImplementationType);

    [Fact]
    public void ComposeWindowFactory_resolves_to_real_implementation()
        => Assert.Equal("ComposeWindowFactory", Effective<IComposeWindowFactory>().ImplementationType?.Name);

    [Fact]
    public void ImapGatewayFactory_resolves_to_real_implementation()
    {
        var d = Effective<IImapGatewayFactory>();
        AssertNotUnavailable(d);
        Assert.Equal("ImapGatewayFactory", d.ImplementationType?.Name);
    }
}
