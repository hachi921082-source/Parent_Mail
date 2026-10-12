using Microsoft.Extensions.DependencyInjection.Extensions;
using ParentMail.App.Features.SyncCache;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;

// DI 拡張の慣例どおり Microsoft.Extensions.DependencyInjection に置く。
// FeatureRegistrars.cs に using を足さず、All への 1 行追加だけで済ませるため。
namespace Microsoft.Extensions.DependencyInjection;

public static class OnDemandFetchRegistration
{
    /// <summary>
    /// sync-cache PLAN-3 の登録（本文・添付のオンデマンド取得、再接続後の自動取得、アイドル時の先読み）。
    /// IAccountRepository・IImapGatewayFactory・MailRepository・ISyncService は他の機能が登録する。
    /// </summary>
    public static IServiceCollection AddOnDemandFetch(this IServiceCollection services)
    {
        services.TryAddSingleton<FetchSession>();
        services.TryAddSingleton<PendingFetchTracker>();
        services.TryAddSingleton<BodyFetcher>();
        // mail-display の仮実装（TryAdd で先に登録済み）を上書きするため、ここは AddSingleton（後勝ち）にする
        services.AddSingleton<IBodyFetcher>(sp => sp.GetRequiredService<BodyFetcher>());
        services.AddSingleton<IAttachmentService, AttachmentService>();
        services.TryAddSingleton<PendingFetchRunner>();
        services.TryAddSingleton<InputIdleMonitor>();
        services.TryAddSingleton<IIdleMonitor>(sp => sp.GetRequiredService<InputIdleMonitor>());
        services.TryAddSingleton<BackfillWorker>();
        services.TryAddSingleton<OnDemandFetchStartup>();
        return services;
    }
}
