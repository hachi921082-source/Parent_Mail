using Microsoft.Extensions.DependencyInjection;
using ParentMail.App.Features.MailList;
using ParentMail.App.Features.Shell;
using ParentMail.Core;

namespace ParentMail.App.Features.Search;

/// <summary>search の DI 登録（検索サービス、検索ボックスの状態、一覧ペイン上部への差し込み）。</summary>
public static class SearchRegistration
{
    public static IServiceCollection AddSearch(this IServiceCollection services)
    {
        services.AddSingleton<SearchService>(sp => new SearchService(
            sp.GetRequiredService<IMailRepository>(), sp.GetRequiredService<IClock>()));
        services.AddSingleton<SearchViewModel>(sp => new SearchViewModel(
            sp.GetRequiredService<SearchService>(), sp.GetRequiredService<ListViewModel>(),
            sp.GetRequiredService<IAppState>(), sp.GetService<IAppLog>()));

        PlugIntoMainWindow(services);
        return services;
    }

    // 主窓の登録を包み、作られた直後に一覧ビューの検索枠へ検索ボックスを差し込む（mail-list の差し込みのあとで動く）。
    private static void PlugIntoMainWindow(IServiceCollection services)
    {
        var previous = services.LastOrDefault(d => d.ServiceType == typeof(MainWindow));
        if (previous is null) return;
        services.Remove(previous);
        services.AddSingleton<MainWindow>(sp =>
        {
            var window = CreatePrevious(sp, previous);
            Plug(window, sp);
            return window;
        });
    }

    internal static void Plug(MainWindow window, IServiceProvider sp)
    {
        if (window.ListPaneSlot.Content is not MailListView listView) return;
        var vm = sp.GetRequiredService<SearchViewModel>();
        var view = new SearchView(vm);
        listView.SearchHost.Content = view;
    }

    private static MainWindow CreatePrevious(IServiceProvider sp, ServiceDescriptor previous)
    {
        if (previous.ImplementationInstance is MainWindow instance) return instance;
        if (previous.ImplementationFactory is { } factory) return (MainWindow)factory(sp);
        return (MainWindow)ActivatorUtilities.CreateInstance(sp, previous.ImplementationType ?? typeof(MainWindow));
    }
}
