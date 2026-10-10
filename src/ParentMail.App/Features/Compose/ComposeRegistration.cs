using Microsoft.Extensions.DependencyInjection;
using ParentMail.Core;
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
        // mail-display が置いた暫定の「何もしない」実装より後に登録するので、こちらが使われる
        services.AddSingleton<IComposeWindowFactory, ComposeWindowFactory>();
        return services;
    }
}
