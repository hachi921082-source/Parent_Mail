using System.ComponentModel;
using System.Windows;
using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>パスワードの入れ直し窓（モーダル）。Enter = 保存する、Esc = やめる。最初のフォーカスはパスワード欄。</summary>
public partial class ReenterPasswordWindow : Window
{
    private readonly ReenterPasswordViewModel _vm;

    public ReenterPasswordWindow(ReenterPasswordViewModel vm, ITextScale scale)
    {
        _vm = vm;
        DataContext = vm;
        FontSize = scale.FontSizeDip;
        InitializeComponent();

        // 既定フォーカスは入力欄にしたいので defaultButton は渡さず自分で IsDefault にする
        SaveButton.IsDefault = true;
        WindowRules.Apply(this, defaultButton: null, cancelButton: CancelButton);
        Loaded += (_, _) => PasswordInput.Focus();

        PasswordInput.PasswordChanged += (_, _) => _vm.Password = PasswordInput.Password;
        _vm.Saved += (_, _) => DialogResult = true;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // 確認中は閉じられない
        if (!_vm.FieldsEnabled && DialogResult is null) e.Cancel = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        // 入力されたパスワードを画面に残さない
        PasswordInput.Clear();
        _vm.Password = string.Empty;
        base.OnClosed(e);
    }
}
