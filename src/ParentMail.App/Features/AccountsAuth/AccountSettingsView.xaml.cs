using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>
/// 設定窓の「アカウント」欄の画面部品。設定窓の差し込み口（アカウント欄の ContentControl）には
/// <c>AccountSettingsView.Create(services)</c> の戻り値を入れる。文字の大きさの欄には触れない。
/// </summary>
public partial class AccountSettingsView : UserControl
{
    /// <summary>一覧の枠に見せる最大の行数。これを超えると縦スクロールになる（A87）。</summary>
    public const int MaxVisibleRows = 3;

    public AccountSettingsView() => InitializeComponent();

    // 行の高さは文字の大きさで変わるので、実際の行の高さの 3 倍を一覧の最大の高さにする（2〜3 行が見える）
    private void OnRowsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var count = RowsHost.Items.Count;
        if (count == 0 || RowsHost.ActualHeight <= 0) return;
        var max = RowsHost.ActualHeight / count * MaxVisibleRows;
        if (Math.Abs(RowsScroller.MaxHeight - max) > 0.5) RowsScroller.MaxHeight = max;
    }

    /// <summary>DI から作った表示モデルをつないだ部品を返す。</summary>
    public static AccountSettingsView Create(IServiceProvider services)
        => new() { DataContext = services.GetRequiredService<AccountSettingsViewModel>() };
}
