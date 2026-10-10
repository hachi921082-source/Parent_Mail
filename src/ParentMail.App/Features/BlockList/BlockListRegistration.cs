using Microsoft.Extensions.DependencyInjection;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.BlockList;

namespace ParentMail.App.Features.BlockList;

// block-list 機能の DI 登録（FeatureRegistrars から 1 行で呼ぶ）。
// 受信規則は登録順に実行される。拒否リストは最初に実行されるので、FeatureRegistrars では振り分けより前に置く。
public static class BlockListRegistration
{
    public static IServiceCollection AddBlockList(this IServiceCollection services)
    {
        services.AddSingleton<IBlockRepository, BlockRepository>();
        services.AddSingleton<IBlockListService, BlockListService>();
        services.AddSingleton<IIncomingRule, BlockRule>();
        return services;
    }
}
