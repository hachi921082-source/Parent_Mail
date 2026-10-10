using Microsoft.Extensions.DependencyInjection;
using ParentMail.App.Features.Shell;
using ParentMail.Core;
using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Features.FilterRules;
using ParentMail.Core.Features.SyncCache;

namespace ParentMail.App.Features.MailList;

/// <summary>mail-list の DI 登録（一覧の表示データ、下部ボタン帯、未読数、主窓への差し込み）。</summary>
public static class MailListRegistration
{
    public static IServiceCollection AddMailList(this IServiceCollection services)
    {
        services.AddSingleton<ListViewModel>(sp => new ListViewModel(
            sp.GetRequiredService<IMailRepository>(), sp.GetRequiredService<IOutboxRepository>(),
            sp.GetRequiredService<IDraftRepository>(), sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<IClock>(), sp.GetService<ISyncService>(), sp.GetService<ISyncCacheStore>(),
            sp.GetService<IAppLog>(), sp.GetService<IDialogService>()));
        // 作成窓（compose）の登録はまだ無いので GetService（無ければ押しても何も起きない）
        services.AddSingleton<ListButtonStripViewModel>(sp => new ListButtonStripViewModel(
            sp.GetRequiredService<IAppState>(), sp.GetRequiredService<ITextScale>(),
            sp.GetService<IComposeWindowFactory>(), sp.GetService<IAddressBookService>(),
            sp.GetService<IBlockListService>(), sp.GetService<IFilterRulesService>()));
        services.AddSingleton<UnreadCounter>(sp => new UnreadCounter(
            sp.GetRequiredService<IMailRepository>(), sp.GetRequiredService<IAppState>(), sp.GetService<IOutboxService>()));
        services.AddSingleton<MailListHost>(sp => new MailListHost(
            sp.GetRequiredService<ListViewModel>(), sp.GetRequiredService<ListButtonStripViewModel>(),
            sp.GetRequiredService<UnreadCounter>(), sp.GetRequiredService<MainViewModel>(), sp.GetService<IOutboxService>()));

        PlugIntoMainWindow(services);
        return services;
    }

    // 主窓の登録を包み、作られた直後にスロットへビューを差し込む。
    // 直前の登録（app-shell、他機能の包みを含む）をそのまま呼ぶので、複数の機能が同じ方法で重ねても互いを壊さない。
    private static void PlugIntoMainWindow(IServiceCollection services)
    {
        var previous = services.LastOrDefault(d => d.ServiceType == typeof(MainWindow));
        if (previous is null) return;
        services.Remove(previous);
        services.AddSingleton<MainWindow>(sp =>
        {
            var window = CreatePrevious(sp, previous);
            sp.GetRequiredService<MailListHost>().Plug(window);
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
