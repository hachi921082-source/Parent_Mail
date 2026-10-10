using System.Windows;
using System.Windows.Input;
using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.App.Features.AddressBook;

// アドレス帳ウィンドウ（モーダル、900 x 600 DIP at 18 pt を文字サイズに合わせて拡大）。
// 最初のフォーカス = 一覧、行で Enter = 編集、Esc = 閉じる。
public partial class AddressBookWindow : Window
{
    private readonly AddressBookViewModel _vm;

    public AddressBookWindow(AddressBookViewModel vm, ITextScale scale, Window? owner)
    {
        _vm = vm;
        DataContext = vm;
        Owner = owner;
        InitializeComponent();

        FontSize = scale.FontSizeDip;
        var factor = Math.Max(1.0, scale.TextSizePt / 18.0);
        Width = Math.Min(Width * factor, SystemParameters.WorkArea.Width * 0.95);
        Height = Math.Min(Height * factor, SystemParameters.WorkArea.Height * 0.95);

        WindowRules.Apply(this, defaultButton: null, cancelButton: CloseButton);
        // WindowRules は最初の入力欄へ移すので、その後で一覧に戻す
        Loaded += (_, _) => FocusList();
    }

    private void FocusList()
    {
        List.Focus();
        if (List.SelectedItem is not null && List.ItemContainerGenerator.ContainerFromItem(List.SelectedItem) is IInputElement row)
        {
            row.Focus();
        }
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.EditCommand.CanExecute(null))
        {
            e.Handled = true;
            _vm.EditCommand.Execute(null);
            Dispatcher.BeginInvoke(new Action(FocusList), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && FindItem(d) && _vm.EditCommand.CanExecute(null))
        {
            _vm.EditCommand.Execute(null);
        }
    }

    // クリックされたのが行の上かどうか（見出しや空きでは編集しない）
    private bool FindItem(DependencyObject d)
    {
        while (d is not null)
        {
            if (d is System.Windows.Controls.ListViewItem) return true;
            if (d == List) return false;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return false;
    }
}
