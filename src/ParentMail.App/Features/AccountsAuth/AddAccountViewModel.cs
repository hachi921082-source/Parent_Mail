using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.AccountsAuth;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>「やめる」操作の結果。</summary>
public enum CancelOutcome { Close, QuitApp, Stay }

/// <summary>アカウント追加画面の入力・状態・登録処理（画面を出さずにテストできる）。</summary>
public sealed partial class AddAccountViewModel : ObservableObject
{
    private readonly AccountService _service;
    private readonly IDialogService _dialog;
    private bool _nameEdited;
    private bool _settingNameFromEmail;

    public AddAccountViewModel(AccountService service, IDialogService dialog, bool firstLaunch)
    {
        _service = service;
        _dialog = dialog;
        FirstLaunch = firstLaunch;
        SubmitCommand = new AsyncRelayCommand(async () => await SubmitAsync(), () => !IsBusy);
    }

    /// <summary>アカウントが 1 つも無い初回起動での表示か（やめる = アプリ終了の確認が付く）。</summary>
    public bool FirstLaunch { get; }

    public string Notice => Strings.AC_01;
    public string EmailLabel => AccountStrings.EmailLabel;
    public string PasswordLabel => AccountStrings.PasswordLabel;
    public string NameLabel => AccountStrings.NameLabel;
    public string CancelLabel => AccountStrings.Cancel;
    public string SubmitLabel => IsBusy ? Strings.AC_08 : AccountStrings.Submit;

    [ObservableProperty] private string _email = string.Empty;
    // パスワードは画面の PasswordBox から渡される。保存・ログ出力はしない
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _errorText = string.Empty;

    [NotifyPropertyChangedFor(nameof(FieldsEnabled), nameof(SubmitLabel))]
    [ObservableProperty] private bool _isBusy;

    /// <summary>確認中は全欄を操作不可にする。</summary>
    public bool FieldsEnabled => !IsBusy;

    public IAsyncRelayCommand SubmitCommand { get; }

    /// <summary>登録に成功したとき、新しいアカウント ID と一緒に出る。</summary>
    public event EventHandler<string>? Added;

    partial void OnIsBusyChanged(bool value) => SubmitCommand.NotifyCanExecuteChanged();

    partial void OnEmailChanged(string value)
    {
        // 表示名を利用者が触るまでは、メールアドレスの @ より前を入れておく
        if (_nameEdited) return;
        _settingNameFromEmail = true;
        DisplayName = AccountInputValidator.DefaultDisplayName(value);
        _settingNameFromEmail = false;
    }

    partial void OnDisplayNameChanged(string value)
    {
        if (!_settingNameFromEmail) _nameEdited = true;
    }

    /// <summary>検査 → 重複確認 → ログイン確認 → 保存。成功なら true。失敗は ErrorText に出す。</summary>
    public async Task<bool> SubmitAsync()
    {
        if (IsBusy) return false;
        if (!AccountInputValidator.IsValidEmail(Email)) { ErrorText = Strings.AC_21; return false; }
        if (string.IsNullOrWhiteSpace(Password)) { ErrorText = Strings.AC_22; return false; }
        if (string.IsNullOrWhiteSpace(DisplayName)) { ErrorText = Strings.AC_23; return false; }

        ErrorText = string.Empty;
        IsBusy = true;
        string? addedId = null;
        void OnAdded(object? s, AccountEventArgs e) => addedId = e.AccountId;
        _service.AccountAdded += OnAdded;
        AddAccountResult result;
        try
        {
            result = await _service.ValidateAndAddAsync(Email, Password, DisplayName);
        }
        finally
        {
            _service.AccountAdded -= OnAdded;
            IsBusy = false;
        }

        switch (result)
        {
            case AddAccountResult.Ok:
                if (addedId is not null) Added?.Invoke(this, addedId);
                return true;
            case AddAccountResult.Duplicate: ErrorText = Strings.AC_05; return false;
            case AddAccountResult.AuthFailed: ErrorText = Strings.AC_06; return false;
            case AddAccountResult.NetworkError: ErrorText = Strings.AC_07; return false;
            default: ErrorText = Strings.AC_04; return false;
        }
    }

    /// <summary>やめる / Esc / 窓の ×。初回起動だけ「アプリを終わりますか」の確認を挟む（既定は戻る）。</summary>
    public CancelOutcome RequestCancel()
    {
        if (!FirstLaunch) return CancelOutcome.Close;
        var quit = _dialog.Confirm(Strings.WD_09, AccountStrings.QuitConfirmText,
            AccountStrings.QuitYes, AccountStrings.QuitNo, safeIsDefault: true);
        return quit ? CancelOutcome.QuitApp : CancelOutcome.Stay;
    }
}
