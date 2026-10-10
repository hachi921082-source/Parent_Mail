using System.Windows;
using System.Windows.Controls;
using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.App.Features.AddressBook;

// 人の追加・編集ダイアログ（モーダル）。Enter（メモ以外）= 保存、Esc = やめる、最初のフォーカス = 名前。
public partial class ContactEditWindow : Window
{
    private readonly ContactEditViewModel _vm;

    public ContactEditWindow(ContactEditViewModel vm, ITextScale scale, Window? owner)
    {
        _vm = vm;
        DataContext = vm;
        Owner = owner;
        InitializeComponent();
        Title = vm.Title;
        FontSize = scale.FontSizeDip;
        Width = Math.Min(Width * Math.Max(1.0, scale.TextSizePt / 18.0), SystemParameters.WorkArea.Width * 0.95);
        MaxHeight = SystemParameters.WorkArea.Height * 0.95;

        // 既定ボタン（Enter）は保存。既定フォーカスは名前欄にしたいので defaultButton は渡さず自分で IsDefault にする。
        SaveButton.IsDefault = true;
        WindowRules.Apply(this, defaultButton: null, cancelButton: CancelButton);
        Loaded += (_, _) => NameBox.Focus();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_vm.TrySave()) DialogResult = true;
    }

    private void OnAddEmail(object sender, RoutedEventArgs e)
    {
        _vm.AddEmail();
        // 増やした行の入力欄へ移る（キーボードだけで続けて入れられる）
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (EmailList.ItemContainerGenerator.ContainerFromIndex(_vm.Emails.Count - 1) is DependencyObject row
                && FindTextBox(row) is { } box)
            {
                box.Focus();
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnRemoveEmail(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is EmailRowViewModel row) _vm.RemoveEmail(row);
    }

    private static TextBox? FindTextBox(DependencyObject root)
    {
        if (root is TextBox t) return t;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindTextBox(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
            if (found is not null) return found;
        }
        return null;
    }
}
