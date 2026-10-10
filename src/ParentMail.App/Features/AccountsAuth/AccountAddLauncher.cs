using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.Core;
using ParentMail.Core.Features.AccountsAuth;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>アカウント追加の窓を出す入口（初回起動と、設定画面の「アカウントを追加」の両方から使う）。</summary>
public sealed class AccountAddLauncher
{
    private readonly IServiceProvider _services;

    public AccountAddLauncher(IServiceProvider services) => _services = services;

    /// <summary>追加できる状態か（2 つまで）。設定画面は false のとき追加ボタンを灰色にして AC-04 を出す。</summary>
    public bool CanAdd => _services.GetRequiredService<AccountService>().CanAddMore;

    /// <summary>追加の窓をモーダルで出す。登録できたら true。</summary>
    public bool ShowAddWindow(bool firstLaunch)
    {
        var vm = new AddAccountViewModel(
            _services.GetRequiredService<AccountService>(),
            _services.GetRequiredService<IDialogService>(),
            firstLaunch);
        var window = new AddAccountWindow(vm, _services.GetRequiredService<ITextScale>());
        // 初回起動は主窓がまだ前面に無いので所有者を付けず画面中央。それ以外は主窓の中央。
        if (!firstLaunch && Application.Current?.MainWindow is { IsVisible: true } main) window.Owner = main;
        return window.ShowDialog() == true;
    }
}
