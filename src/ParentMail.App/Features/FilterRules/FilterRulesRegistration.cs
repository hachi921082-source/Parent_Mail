using Microsoft.Extensions.DependencyInjection;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.FilterRules;

namespace ParentMail.App.Features.FilterRules;

// filter-rules 機能の DI 登録（FeatureRegistrars から 1 行で呼ぶ）。
// 受信規則は登録順に実行される。振り分けは拒否リストの次なので、FeatureRegistrars では AddBlockList の後に置く。
public static class FilterRulesRegistration
{
    public static IServiceCollection AddFilterRules(this IServiceCollection services)
    {
        services.AddSingleton<IFilterRepository, FilterRepository>();
        services.AddSingleton<FilterRulesService>();
        services.AddSingleton<IFilterRulesService>(sp => sp.GetRequiredService<FilterRulesService>());
        services.AddSingleton<IIncomingRule, FilterRuleHandler>();
        return services;
    }
}
