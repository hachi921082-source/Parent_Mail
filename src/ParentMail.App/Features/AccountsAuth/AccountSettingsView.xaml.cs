using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>
/// 設定窓の「アカウント」欄の画面部品。設定窓の差し込み口（アカウント欄の ContentControl）には
/// <c>AccountSettingsView.Create(services)</c> の戻り値を入れる。文字の大きさの欄には触れない。
/// </summary>
public partial class AccountSettingsView : UserControl
{
    public AccountSettingsView() => InitializeComponent();

    /// <summary>DI から作った表示モデルをつないだ部品を返す。</summary>
    public static AccountSettingsView Create(IServiceProvider services)
        => new() { DataContext = services.GetRequiredService<AccountSettingsViewModel>() };
}
