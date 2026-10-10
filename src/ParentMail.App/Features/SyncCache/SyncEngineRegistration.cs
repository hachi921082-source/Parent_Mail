using ParentMail.App.Features.SyncCache;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;

// DI 拡張の慣例どおり Microsoft.Extensions.DependencyInjection に置く。
// FeatureRegistrars.cs に using を足さず、All への 1 行追加だけで済ませるため。
namespace Microsoft.Extensions.DependencyInjection;

public static class SyncEngineRegistration
{
    /// <summary>
    /// sync-cache PLAN-2 の同期エンジンの登録。
    /// IAccountRepository と ICredentialStore は accounts-auth、IOutboxService は outbox-send、
    /// IIncomingRule は block-list / filter-rules が登録する（無い間は規則なし・送信箱再送なしで動く）。
    /// </summary>
    public static IServiceCollection AddSyncEngine(this IServiceCollection services)
    {
        services.AddSingleton<ISyncCacheStore, SyncCacheStore>();
        services.AddSingleton<IImapGatewayFactory, ImapGatewayFactory>();
        services.AddSingleton<SyncService>();
        services.AddSingleton<ISyncService>(sp => sp.GetRequiredService<SyncService>());
        services.AddSingleton<IReadStateSync>(sp => sp.GetRequiredService<SyncService>());
        services.AddSingleton<SyncCoordinator>();
        services.AddSingleton<SyncUiBinder>();
        services.AddSingleton<SyncStartup>();
        return services;
    }
}
