using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ParentMail.App.Features.Settings;

/// <summary>
/// 設定窓（モーダル、幅 720）。Enter = 閉じる、Esc = 閉じる。最初に数字の入力欄へフォーカスする。
/// 変更はその場で全画面に反映されるので、閉じても保存の操作は要らない。
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm, UIElement accountSection, Window? owner)
    {
        DataContext = vm;
        InitializeComponent();
        AccountSection.Content = accountSection;
        Owner = owner;

        WindowStartupLocation = owner is not null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        // 高さは内容に合わせるが、作業領域からははみ出さない（はみ出す分はスクロール）
        MaxHeight = SystemParameters.WorkArea.Height;

        CloseButton.IsDefault = true; // Enter
        CloseButton.IsCancel = true;  // Esc
        Loaded += (_, _) => SizeBox.Focus();
        Closed += (_, _) => vm.Dispose();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        // Enter / Esc で閉じるとき、入力中の数字も反映する（反映できない値は元に戻る）。
        // 窓を閉じるのは IsCancel の働き（DialogResult）に任せる。
        BindingOperations.GetBindingExpression(SizeBox, TextBox.TextProperty)?.UpdateSource();
    }
}
