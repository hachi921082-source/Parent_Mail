using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.Core;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>アカウント関連の窓を実際に出す実装。持ち主は、いま前面にある窓（設定窓や主窓）の中央に出す。</summary>
public sealed class AccountDialogs : IAccountDialogs
{
    private readonly IServiceProvider _services;

    public AccountDialogs(IServiceProvider services) => _services = services;

    public bool ShowRename(RenameViewModel vm)
    {
        var window = new RenameWindow(vm, _services.GetRequiredService<ITextScale>());
        AttachOwner(window);
        return window.ShowDialog() == true;
    }

    public bool ShowReenter(ReenterPasswordViewModel vm)
    {
        var window = new ReenterPasswordWindow(vm, _services.GetRequiredService<ITextScale>());
        AttachOwner(window);
        return window.ShowDialog() == true;
    }

    public bool ShowAdd(bool firstLaunch)
        => _services.GetRequiredService<AccountAddLauncher>().ShowAddWindow(firstLaunch);

    private static void AttachOwner(Window window)
    {
        var app = Application.Current;
        if (app is null) return;
        // 開いているモーダルの窓（設定窓など）があればその上、無ければ主窓の上に重ねる
        var owner = app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
                    ?? (app.MainWindow is { IsVisible: true } main ? main : null);
        if (owner is not null && !ReferenceEquals(owner, window)) window.Owner = owner;
    }
}
