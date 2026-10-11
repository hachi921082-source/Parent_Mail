using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.App.Features.Compose;

/// <summary>
/// アドレス帳から選ぶ画面（compose PLAN-3, WD-31）。作成窓にだけモーダル。
/// WPF の ShowDialog は同じアプリの全部の窓を止めてしまうので、持ち主の作成窓だけを使えなくして閉じるまで待つ。
/// Enter = 入れる、Esc = やめる、最初のフォーカスは一覧。
/// </summary>
public partial class AddressPickerDialog : Window
{
    private readonly AddressPickerViewModel _vm;

    public AddressPickerDialog(AddressPickerViewModel vm, ITextScale scale, Window? owner)
    {
        _vm = vm;
        DataContext = vm;
        Owner = owner;
        InitializeComponent();
        Title = vm.Title;
        FontSize = scale.FontSizeDip;

        // 720 x 560 は 18pt のときの大きさ。文字を大きくした分だけ広げ、画面からはみ出さない
        var factor = Math.Max(1.0, scale.TextSizePt / 18.0);
        Width = Math.Min(Width * factor, SystemParameters.WorkArea.Width * 0.95);
        Height = Math.Min(Height * factor, SystemParameters.WorkArea.Height * 0.95);

        WindowRules.Apply(this, ConfirmButton, CancelButton);
        // 既定ボタンへのフォーカスより後に動き、最初のフォーカスを一覧にする
        Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(FocusList), DispatcherPriority.Loaded);
    }

    /// <summary>「入れる」で閉じたとき true。</summary>
    public bool Confirmed { get; private set; }

    public ListBox ListField => List;
    public Button ConfirmField => ConfirmButton;
    public Button CancelField => CancelButton;

    private void FocusList()
    {
        if (List.Items.Count == 0) { List.Focus(); return; }
        List.SelectedIndex = 0;
        if (List.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem first) first.Focus();
        else List.Focus();
    }

    /// <summary>
    /// 持ち主の作成窓だけを使えなくして表示し、閉じるまで待つ。「入れる」で閉じたとき true。
    /// ほかの作成窓や主窓は、そのまま使える。
    /// </summary>
    public bool ShowForOwner()
    {
        var owner = Owner;
        var frame = new DispatcherFrame();
        Closed += (_, _) =>
        {
            // 持ち主を先に使える状態へ戻してから待ちを終える（戻す前だと前面に出せない）
            if (owner is not null) owner.IsEnabled = true;
            frame.Continue = false;
        };
        if (owner is not null) owner.IsEnabled = false;
        Show();
        Dispatcher.PushFrame(frame);
        owner?.Activate();
        return Confirmed;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!_vm.TryConfirm()) return;
        Confirmed = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    // Space で今の行を切り替える（矢印で動く）
    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space) return;
        if (List.SelectedItem is ContactAddressRow row) row.Toggle();
        e.Handled = true;
    }

    // 行のどこを押しても切り替える
    private void OnRowClick(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ContactAddressRow row)
        {
            row.Toggle();
            List.SelectedItem = row;
            e.Handled = true;
        }
    }
}
