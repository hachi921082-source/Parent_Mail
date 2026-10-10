using System.Windows;
using System.Windows.Input;
using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.App.Features.FilterRules;

// 振り分けルール一覧ウィンドウ（モーダル、900 x 600 DIP at 18 pt を文字サイズに合わせて拡大）。
// 最初のフォーカス = 一覧、行で Enter = 編集、Space = 有効・無効の切り替え、Esc = 閉じる。
public partial class FilterRulesWindow : Window
{
    private readonly FilterRuleListViewModel _vm;

    public FilterRulesWindow(FilterRuleListViewModel vm, ITextScale scale, Window? owner)
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
        else if (e.Key == Key.Space)
        {
            e.Handled = true;
            _vm.ToggleSelected();
        }
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && IsOnRow(d) && _vm.EditCommand.CanExecute(null))
        {
            _vm.EditCommand.Execute(null);
        }
    }

    // クリックされたのが行の上かどうか（見出しや空き、チェックでは編集しない）
    private bool IsOnRow(DependencyObject d)
    {
        while (d is not null)
        {
            if (d is System.Windows.Controls.CheckBox) return false;
            if (d is System.Windows.Controls.ListViewItem) return true;
            if (d == List) return false;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return false;
    }
}
