using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ParentMail.Core.Data;

/// <summary>sync-cache の DI 登録（cache.db、リポジトリ、添付ファイルストア）。</summary>
public static class SyncCacheRegistration
{
    /// <param name="dataRoot">データのルート。省略時は AppPaths が解決したデータルート。</param>
    public static IServiceCollection AddSyncCache(this IServiceCollection services, string? dataRoot = null)
    {
        services.TryAddSingleton(new CachePaths(dataRoot ?? CachePaths.DefaultRoot()));
        services.TryAddSingleton<ICacheDb, CacheDb>();
        services.TryAddSingleton<IAttachmentStore, AttachmentStore>();
        services.TryAddSingleton<MailRepository>();
        // 同じインスタンスを契約型でも具象型（SaveAttachments など契約外の追加分）でも引けるようにする
        services.TryAddSingleton<IMailRepository>(sp => sp.GetRequiredService<MailRepository>());
        services.TryAddSingleton<IOutboxRepository, OutboxRepository>();
        services.TryAddSingleton<IDraftRepository, DraftRepository>();
        return services;
    }
}
