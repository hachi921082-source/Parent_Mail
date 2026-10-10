using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ParentMail.App.Services;

/// <summary>
/// 共通ウィンドウ規則（window-layouts 1.1, detailed-spec 21）。
/// Enter = 安全な既定ボタン、Esc = 取り消し（窓の × も同じ）、既定フォーカス、所有者の中央に表示。
/// </summary>
public static class WindowRules
{
    /// <summary>ダイアログに規則を適用する。defaultButton が null ならフォーム窓として最初の入力欄へフォーカスする。</summary>
    public static void Apply(Window window, Button? defaultButton, Button? cancelButton)
    {
        window.WindowStartupLocation = window.Owner is not null
            ? WindowStartupLocation.CenterOwner
            : WindowStartupLocation.CenterScreen;
        window.ShowInTaskbar = false;
        window.ResizeMode = ResizeMode.NoResize;

        // IsDefault は Enter、IsCancel は Esc で押されたのと同じになる。
        if (defaultButton is not null) defaultButton.IsDefault = true;
        if (cancelButton is not null) cancelButton.IsCancel = true;

        window.Loaded += (_, _) =>
        {
            var target = (UIElement?)defaultButton ?? FindFirstInput(window);
            target?.Focus();
        };
    }

    /// <summary>メイン窓の F5 を更新の操作に結び付ける。</summary>
    public static void BindRefreshKey(Window window, ICommand refresh)
        => window.InputBindings.Add(new KeyBinding(refresh, Key.F5, ModifierKeys.None));

    private static UIElement? FindFirstInput(DependencyObject root)
    {
        if (root is TextBox or ComboBox or CheckBox) return (UIElement)root;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindFirstInput(VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }
}
