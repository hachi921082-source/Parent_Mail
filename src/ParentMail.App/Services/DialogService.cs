using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ParentMail.App.Resources;
using ParentMail.Core;

namespace ParentMail.App.Services;

/// <summary>
/// モーダルダイアログ（IDialogService）。大きなボタン、文字サイズは設定に従う。
/// 幅 520 DIP、内容に合わせて高さを決め、作業領域の 90% を超えたらスクロールする。
/// </summary>
public sealed class DialogService : IDialogService
{
    private const double DialogWidth = 520;
    private const double DialogMargin = 24;
    private const double Gap = 16;

    private readonly ITextScale _scale;

    // 直前に前面だった自アプリの窓。Explorer からのドラッグ＆ドロップなどで
    // 自アプリの窓がどれも前面でないときに、ダイアログの持ち主を決めるために使う。
    private static WeakReference<Window>? _lastActive;

    static DialogService()
    {
        // 窓が読み込まれるたびに、前面になった記録を取る登録をする。
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) =>
        {
            if (sender is Window w)
            {
                // 読み込みより前にすでに前面になっている窓（作成窓など）も記録する。
                if (w.IsActive) _lastActive = new WeakReference<Window>(w);
                w.Activated += (_, _) => _lastActive = new WeakReference<Window>(w);
                // 他のアプリ（Explorer など）へ前面が移った瞬間にも記録する。
                w.Deactivated += (_, _) => _lastActive = new WeakReference<Window>(w);
            }
        }));
    }

    public DialogService(ITextScale scale) => _scale = scale;

    public void ShowNotice(string title, string message)
        => Show(title, message, new[] { Strings.BN_12 }, defaultIndex: 0);

    public bool Confirm(string title, string message, string yesText, string noText, bool safeIsDefault = true)
        => Show(title, message, new[] { yesText, noText }, safeIsDefault ? 1 : 0) == 0;

    public int Choose(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int safeIndex)
    {
        // Esc・× は -1（契約: キャンセル）。安全側のボタンを実際に押したときは safeIndex。
        return Show(title, message, buttons, defaultIndex);
    }

    // 戻り値: 押されたボタンの位置。Esc・× は -1。
    private int Show(string title, string message, IReadOnlyList<string> buttons, int defaultIndex)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (!dispatcher.CheckAccess())
        {
            return dispatcher.Invoke(() => Show(title, message, buttons, defaultIndex));
        }

        var chosen = -1;
        var window = new Window
        {
            Title = title,
            Width = DialogWidth,
            SizeToContent = SizeToContent.Height,
            MaxHeight = SystemParameters.WorkArea.Height * 0.9,
            Owner = FindOwner(),
            FontSize = _scale.FontSizeDip,
            Background = (Brush)Application.Current.FindResource("Page"),
        };

        var panel = new DockPanel { Margin = new Thickness(DialogMargin) };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, Gap, 0, 0),
        };
        DockPanel.SetDock(row, Dock.Bottom);

        var created = new List<Button>();
        for (var i = 0; i < buttons.Count; i++)
        {
            var index = i;
            var button = new Button
            {
                Content = buttons[i],
                Style = (Style)Application.Current.FindResource("AppButton"),
                Margin = new Thickness(i == 0 ? 0 : Gap, 0, 0, 0),
            };
            button.Click += (_, _) =>
            {
                chosen = index;
                window.DialogResult = true;
            };
            row.Children.Add(button);
            created.Add(button);
        }
        panel.Children.Add(row);
        panel.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
        });
        window.Content = panel;

        var defaultButton = created[Math.Clamp(defaultIndex, 0, created.Count - 1)];
        // Esc は「取り消し」（-1）。窓の × も同じ。
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                window.DialogResult = false;
            }
        };
        WindowRules.Apply(window, defaultButton, cancelButton: null);

        var owner = window.Owner;
        window.ShowDialog();
        // 閉じたあと、ダイアログを出した窓（作成窓など）を前面に戻す。
        // 戻さないと、持ち主が主窓のとき作成窓が主窓の後ろに隠れて見失う。
        owner?.Activate();
        return chosen;
    }

    private static Window? FindOwner()
    {
        var windows = Application.Current.Windows.OfType<Window>().Where(w => w.IsVisible).ToList();
        var active = windows.FirstOrDefault(w => w.IsActive);
        if (active != null) return active;
        if (_lastActive != null && _lastActive.TryGetTarget(out var last) && last.IsVisible) return last;
        return Application.Current.MainWindow ?? windows.FirstOrDefault();
    }
}
