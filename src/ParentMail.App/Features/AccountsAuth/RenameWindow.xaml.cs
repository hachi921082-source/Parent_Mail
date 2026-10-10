using System.Windows;
using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>名前を変える窓（モーダル）。Enter = 保存、Esc = やめる。最初に表示名を選択状態でフォーカスする。</summary>
public partial class RenameWindow : Window
{
    private readonly RenameViewModel _vm;

    public RenameWindow(RenameViewModel vm, ITextScale scale)
    {
        _vm = vm;
        DataContext = vm;
        FontSize = scale.FontSizeDip;
        InitializeComponent();

        // 既定フォーカスは入力欄（全選択）にしたいので、defaultButton は渡さず自分で IsDefault にする
        SaveButton.IsDefault = true;
        WindowRules.Apply(this, defaultButton: null, cancelButton: CancelButton);
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_vm.TrySave()) DialogResult = true;
    }
}
