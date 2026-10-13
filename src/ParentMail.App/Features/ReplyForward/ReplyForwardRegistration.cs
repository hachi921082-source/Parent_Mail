using Microsoft.Extensions.DependencyInjection;
using ParentMail.App.Features.FolderActions;
using ParentMail.Core;

namespace ParentMail.App.Features.ReplyForward;

/// <summary>reply-forward の DI 登録。返信・転送の入口（IReplyForwardHandler）を folder-actions へ差し込む。</summary>
public static class ReplyForwardRegistration
{
    public static IServiceCollection AddReplyForward(this IServiceCollection services)
    {
        services.AddSingleton<ForwardBuilder>(sp => new ForwardBuilder(
            sp.GetRequiredService<IMailRepository>(), sp.GetRequiredService<IAttachmentService>(), sp.GetRequiredService<IAppLog>()));
        services.AddSingleton<IReplyForwardHandler>(sp => new ReplyForwardHandler(
            sp.GetRequiredService<IMailRepository>(), sp.GetRequiredService<IAccountRepository>(),
            sp.GetRequiredService<IBodyFetcher>(), sp.GetRequiredService<ForwardBuilder>(),
            sp.GetRequiredService<IComposeWindowFactory>(), sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<IAppLog>()));
        return services;
    }
}
