using Microsoft.Extensions.DependencyInjection;
using ParentMail.App.Features.MailDisplay;
using ParentMail.App.Features.MailList;
using ParentMail.App.Features.Shell;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Features.FolderActions;
using ParentMail.Core.Features.MailDisplay;
using ParentMail.Core.Features.SyncCache;

namespace ParentMail.App.Features.FolderActions;

/// <summary>folder-actions の DI 登録（業務サービス、操作の制御、見出しボタン、右クリックメニュー、主窓への差し込み）。</summary>
public static class FolderActionsRegistration
{
    public static IServiceCollection AddFolderActions(this IServiceCollection services)
    {
        services.AddSingleton<IFolderActionService>(sp => new FolderActionService(
            sp.GetRequiredService<FetchSession>(), sp.GetRequiredService<MailRepository>(),
            sp.GetRequiredService<IDraftRepository>(), sp.GetService<IDraftService>(),
            sp.GetRequiredService<IReadStateSync>(), sp.GetService<ISyncService>(),
            sp.GetRequiredService<IClock>(), sp.GetRequiredService<IAppLog>()));
        // 返信・下書き・送信箱などの相手は、ほかの機能が登録していれば使い、無ければ何も起きない
        services.AddSingleton<FolderActionController>(sp => new FolderActionController(
            sp.GetRequiredService<IFolderActionService>(), sp.GetRequiredService<ListViewModel>(), sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<INotifier>(), sp.GetRequiredService<IDialogService>(), sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IMailRepository>(), sp.GetRequiredService<IAppLog>(),
            sp.GetService<IAddressBookService>(), sp.GetService<IBlockListService>(), sp.GetService<IDraftService>(),
            sp.GetService<IComposeWindowFactory>(), sp.GetService<IOutboxService>(), sp.GetService<IReplyForwardHandler>()));
        services.AddSingleton<HeaderActionsViewModel>(sp => new HeaderActionsViewModel(
            sp.GetRequiredService<FolderActionController>(), sp.GetRequiredService<ListViewModel>()));
        services.AddSingleton<ListContextMenuHost>(sp => new ListContextMenuHost(
            sp.GetRequiredService<ListViewModel>(), sp.GetRequiredService<FolderActionController>(), sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<IMailRepository>(), sp.GetRequiredService<ITextScale>()));
        services.AddSingleton<FolderActionsHost>(sp => new FolderActionsHost(
            sp.GetRequiredService<ListViewModel>(), sp.GetRequiredService<ListContextMenuHost>(),
            sp.GetRequiredService<HeaderActionsViewModel>(), () => sp.GetRequiredService<PreviewView>(),
            sp.GetService<IPreviewSelectionHints>()));

        PlugIntoMainWindow(services);
        return services;
    }

    // 主窓の登録を包み、作られた直後に部品をつなぐ（mail-list の PlugIntoMainWindow と同じ方法で重ねる）。
    private static void PlugIntoMainWindow(IServiceCollection services)
    {
        var previous = services.LastOrDefault(d => d.ServiceType == typeof(MainWindow));
        if (previous is null) return;
        services.Remove(previous);
        services.AddSingleton<MainWindow>(sp =>
        {
            var window = CreatePrevious(sp, previous);
            sp.GetRequiredService<FolderActionsHost>().Plug(window);
            return window;
        });
    }

    private static MainWindow CreatePrevious(IServiceProvider sp, ServiceDescriptor previous)
    {
        if (previous.ImplementationInstance is MainWindow instance) return instance;
        if (previous.ImplementationFactory is { } factory) return (MainWindow)factory(sp);
        return (MainWindow)ActivatorUtilities.CreateInstance(sp, previous.ImplementationType ?? typeof(MainWindow));
    }
}
