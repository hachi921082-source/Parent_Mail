using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParentMail.Core.Features.MailDisplay;

namespace ParentMail.App.Features.MailDisplay;

// mail-display 機能の DI 登録（FeatureRegistrars から 1 行で呼ぶ）。
// IBodyFetcher の実装は sync-cache PLAN-3、IReadStateSync は同期エンジン、IContactLookup は address-book が登録する。
public static class MailDisplayRegistration
{
    public static IServiceCollection AddMailDisplay(this IServiceCollection services)
    {
        // 他機能の実装が入るまでの仮実装（後から登録された本物が優先される）
        services.TryAddSingleton<ParentMail.Core.IBodyFetcher, UnavailableBodyFetcher>();
        services.TryAddSingleton<ParentMail.Core.IAttachmentService, UnavailableAttachmentService>();
        services.TryAddSingleton<ParentMail.Core.IComposeWindowFactory, UnavailableComposeWindowFactory>();

        services.AddSingleton<IPreviewSelectionHints, PreviewSelectionHints>();
        services.AddSingleton<IHtmlModeSwitch, MainViewModelHtmlSwitch>();
        services.AddSingleton<IAttachmentFileUi, WindowsAttachmentFileUi>();
        services.AddSingleton<AttachmentActions>(sp =>
        {
            var actions = new AttachmentActions(
                sp.GetRequiredService<ParentMail.Core.IAttachmentService>(),
                sp.GetRequiredService<ParentMail.Core.IDialogService>(),
                sp.GetRequiredService<IAttachmentFileUi>(),
                sp.GetRequiredService<ParentMail.Core.IAppLog>(),
                System.IO.Path.Combine(new ParentMail.Core.Platform.AppPaths().DataRoot, "temp"));
            // アプリの起動時に、前回の「開く」で作った一時ファイルを消す
            actions.CleanTemp();
            return actions;
        });
        services.AddSingleton<AttachmentListViewModel>();
        services.AddSingleton<PreviewViewModel>(sp => new PreviewViewModel(
            sp.GetRequiredService<ParentMail.Core.IAppState>(),
            sp.GetRequiredService<ParentMail.Core.IMailRepository>(),
            sp.GetRequiredService<ParentMail.Core.IBodyFetcher>(),
            sp.GetRequiredService<ParentMail.Core.IAttachmentService>(),
            sp.GetRequiredService<ParentMail.Core.Features.SyncCache.IReadStateSync>(),
            sp.GetRequiredService<ParentMail.Core.IContactLookup>(),
            sp.GetRequiredService<ParentMail.Core.ITextScale>(),
            sp.GetRequiredService<IHtmlModeSwitch>(),
            sp.GetRequiredService<IPreviewSelectionHints>(),
            sp.GetRequiredService<ParentMail.Core.IOutboxRepository>(),
            sp.GetRequiredService<ParentMail.Core.IDraftRepository>(),
            sp.GetRequiredService<ParentMail.Core.IAccountRepository>(),
            sp.GetRequiredService<ParentMail.Core.IDialogService>(),
            sp.GetRequiredService<ParentMail.Core.IAppLog>(),
            sp.GetRequiredService<ParentMail.App.SafetyWarnings.LinkClickHandler>(),
            WebViewRuntime.IsAvailable,
            null,
            sp.GetRequiredService<AttachmentListViewModel>()));
        services.AddSingleton<PreviewView>();

        PreviewSlotAttacher.Install(() => App.Services);
        return services;
    }
}
