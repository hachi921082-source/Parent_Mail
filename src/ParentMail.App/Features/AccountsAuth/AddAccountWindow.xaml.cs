using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>アカウント追加の窓（モーダル）。Enter = 登録する、Esc / やめる / × = やめる。</summary>
public partial class AddAccountWindow : Window
{
    private readonly AddAccountViewModel _vm;
    private bool _allowClose;

    public AddAccountWindow(AddAccountViewModel vm, ITextScale scale)
    {
        _vm = vm;
        DataContext = vm;
        FontSize = scale.FontSizeDip;
        InitializeComponent();

        // Owner が無い（初回起動）ときは画面の中央、あるときは持ち主の中央に出る。初期フォーカスは先頭の入力欄。
        WindowRules.Apply(this, SubmitButton, cancelButton: null);
        Loaded += (_, _) => EmailBox.Focus();

        PasswordInput.PasswordChanged += (_, _) => _vm.Password = PasswordInput.Password;
        CancelButton.Click += (_, _) => Close();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _vm.FieldsEnabled)
            {
                e.Handled = true;
                Close();
            }
        };
        Closing += OnClosing;
        _vm.Added += (_, _) =>
        {
            _allowClose = true;
            DialogResult = true;
        };
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        // 確認中は閉じられない
        if (!_vm.FieldsEnabled) { e.Cancel = true; return; }

        switch (_vm.RequestCancel())
        {
            case CancelOutcome.Close:
                _allowClose = true;
                break;
            case CancelOutcome.QuitApp:
                _allowClose = true;
                Application.Current.Shutdown();
                break;
            default:
                e.Cancel = true;
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // 入力されたパスワードを画面に残さない
        PasswordInput.Clear();
        _vm.Password = string.Empty;
        base.OnClosed(e);
    }
}
