using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.Compose;

namespace ParentMail.App.Features.Compose;

// compose 機能の DI 登録（FeatureRegistrars から 1 行で呼ぶ）。
// IOutboxService は outbox-send、連絡先・メールの保管は address-book と sync-cache が登録する。
public static class ComposeRegistration
{
    public static IServiceCollection AddCompose(this IServiceCollection services)
    {
        services.AddSingleton<RecipientSuggester>();
        services.AddSingleton<ComposeWindowRegistry>();
        // 下書きサービス（PLAN-2）。送信箱は使うときに取得するので、循環にならない
        services.TryAddSingleton<IDraftService>(sp => new DraftService(
            sp.GetRequiredService<IDraftRepository>(), sp.GetRequiredService<IMailRepository>(),
            sp.GetRequiredService<IAccountRepository>(), sp.GetRequiredService<IImapGatewayFactory>(),
            sp.GetRequiredService<IBodyFetcher>(), sp.GetRequiredService<IAttachmentService>(),
            sp.GetRequiredService<CachePaths>(), sp.GetRequiredService<IAppLog>()));
        services.AddSingleton<DraftSyncHook>();
        // アドレス帳から選ぶ画面（PLAN-3）
        services.AddSingleton<IAddressPickerService, AddressPickerService>();
        // mail-display が置いた暫定の「何もしない」実装より後に登録するので、こちらが使われる
        services.AddSingleton<IComposeWindowFactory, ComposeWindowFactory>();
        return services;
    }
}
