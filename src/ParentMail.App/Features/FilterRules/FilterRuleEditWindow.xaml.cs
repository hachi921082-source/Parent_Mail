using System.Windows;
using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.App.Features.FilterRules;

// ルールの追加・編集ダイアログ（モーダル）。Enter = 保存、Esc = やめる、最初のフォーカス = 差出人の欄。
public partial class FilterRuleEditWindow : Window
{
    private readonly FilterRuleEditViewModel _vm;

    public FilterRuleEditWindow(FilterRuleEditViewModel vm, ITextScale scale, Window? owner)
    {
        _vm = vm;
        DataContext = vm;
        Owner = owner;
        InitializeComponent();
        Title = vm.Title;
        FontSize = scale.FontSizeDip;
        Width = Math.Min(Width * Math.Max(1.0, scale.TextSizePt / 18.0), SystemParameters.WorkArea.Width * 0.95);
        MaxHeight = SystemParameters.WorkArea.Height * 0.95;

        // 既定ボタン（Enter）は保存。既定フォーカスは差出人の欄にしたいので defaultButton は渡さず自分で IsDefault にする。
        SaveButton.IsDefault = true;
        WindowRules.Apply(this, defaultButton: null, cancelButton: CancelButton);
        Loaded += (_, _) => SenderBox.Focus();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_vm.TrySave()) DialogResult = true;
    }
}
