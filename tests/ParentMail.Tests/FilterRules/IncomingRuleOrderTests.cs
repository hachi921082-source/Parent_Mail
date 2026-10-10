using Microsoft.Extensions.DependencyInjection;
using ParentMail.App;
using ParentMail.Core;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Features.FilterRules;

namespace ParentMail.Tests.FilterRules;

// 受信規則の登録順（拒否リストが先、振り分けが後）を、FeatureRegistrars の一覧そのものから確かめる。
public class IncomingRuleOrderTests
{
    [Fact]
    public void Block_rule_is_registered_before_filter_rule()
    {
        var services = new ServiceCollection();
        foreach (var register in FeatureRegistrars.All) register(services);

        var rules = services.Where(d => d.ServiceType == typeof(IIncomingRule)).Select(d => d.ImplementationType).ToList();

        Assert.Equal(new[] { typeof(BlockRule), typeof(FilterRuleHandler) }, rules);
    }

    [Fact]
    public void Feature_services_and_repositories_are_published()
    {
        var services = new ServiceCollection();
        foreach (var register in FeatureRegistrars.All) register(services);

        Assert.Contains(services, d => d.ServiceType == typeof(IBlockListService));
        Assert.Contains(services, d => d.ServiceType == typeof(IFilterRulesService));
        Assert.Contains(services, d => d.ServiceType == typeof(IBlockRepository));
        Assert.Contains(services, d => d.ServiceType == typeof(IFilterRepository));
    }
}
