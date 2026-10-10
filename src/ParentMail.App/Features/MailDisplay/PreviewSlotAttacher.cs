using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.App.Features.Shell;
using ParentMail.App.Resources;

namespace ParentMail.App.Features.MailDisplay;

/// <summary>
/// 主窓の PreviewSlot にプレビューを差し込む。MainWindow（app-shell の持ち物）を書き換えないよう、
/// 窓の Loaded を見て、スロットが空なら差し込む。
/// あわせて上部バーの「HTMLで表示」チェックを、HTML 本文があるメールのときだけ押せるようにする。
/// </summary>
public static class PreviewSlotAttacher
{
    private static bool _installed;
    private static Func<IServiceProvider?> _services = () => null;

    /// <summary>登録時に呼ぶ。サービスの取り先は最後に渡したものを使う（クラスハンドラは 1 回だけ登録する）。</summary>
    public static void Install(Func<IServiceProvider?> services)
    {
        _services = services;
        if (_installed) return;
        _installed = true;
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => Attach((MainWindow)sender, _services())));
    }

    private static void Attach(MainWindow window, IServiceProvider? sp)
    {
        if (sp is null) return;
        if (window.FindName("PreviewSlot") is not ContentControl slot || slot.Content != null) return;

        var vm = sp.GetRequiredService<PreviewViewModel>();
        slot.Content = sp.GetRequiredService<PreviewView>();
        BindHtmlCheckBox(window, vm);
    }

    // チェックボックスは app-shell の XAML にあるので、中身の文言で見つけて IsEnabled を結びつける
    private static void BindHtmlCheckBox(MainWindow window, PreviewViewModel vm)
    {
        if (window.FindName("TopBarSlot") is not ContentControl top || top.Content is not DependencyObject root) return;
        var box = FindCheckBox(root, Strings.TB_11);
        if (box is null) return;
        box.SetBinding(UIElement.IsEnabledProperty, new System.Windows.Data.Binding(nameof(PreviewViewModel.HtmlAvailable))
        {
            Source = vm,
            Mode = System.Windows.Data.BindingMode.OneWay,
        });
    }

    private static CheckBox? FindCheckBox(DependencyObject parent, string content)
    {
        if (parent is CheckBox cb && cb.Content is string s && s == content) return cb;
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            var found = FindCheckBox(child, content);
            if (found != null) return found;
        }
        return null;
    }
}
