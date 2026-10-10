using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.OutboxSend;

namespace ParentMail.App.Features.OutboxSend;

// outbox-send 機能の DI 登録（送信箱サービスと MailKit の SMTP ゲートウェイ）。
public static class OutboxSendRegistration
{
    public static IServiceCollection AddOutboxSend(this IServiceCollection services)
    {
        services.TryAddSingleton<ISmtpGateway, MailKitSmtpGateway>();
        // 同期サービス・下書きサービスは別機能の登録で、循環や未登録に備えて使うときに取得する
        services.TryAddSingleton<OutboxService>(sp => new OutboxService(
            sp.GetRequiredService<IOutboxRepository>(), sp.GetRequiredService<IDraftRepository>(),
            sp.GetRequiredService<IAccountRepository>(), sp.GetRequiredService<ISmtpGateway>(),
            sp.GetRequiredService<CachePaths>(), sp.GetRequiredService<INotifier>(), sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<IAppLog>(),
            () => sp.GetService<ISyncService>(), () => sp.GetService<IDraftService>()));
        services.TryAddSingleton<IOutboxService>(sp => sp.GetRequiredService<OutboxService>());
        return services;
    }
}
