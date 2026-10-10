using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.App.Resources;
using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>パスワードの入れ直し窓の入力・確認・保存（画面を出さずにテストできる）。</summary>
public sealed partial class ReenterPasswordViewModel : ObservableObject
{
    private readonly AccountService _service;
    private readonly Account _account;

    public ReenterPasswordViewModel(AccountService service, Account account)
    {
        _service = service;
        _account = account;
        SaveCommand = new AsyncRelayCommand(async () => await SaveAsync(), () => !IsBusy);
    }

    public string AccountId => _account.Id;
    public string Title => Strings.WD_04;
    /// <summary>「母のメール (mother@gmail.com)」の形。</summary>
    public string AccountText => $"{_account.DisplayName} ({_account.Email})";
    public string PasswordLabel => AccountStrings.ReenterPasswordLabel;
    public string SaveLabel => IsBusy ? Strings.AC_08 : AccountStrings.ReenterSave;
    public string CancelLabel => AccountStrings.ReenterCancel;

    // パスワードは画面の PasswordBox から渡される。保存・ログ出力はしない
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _errorText = string.Empty;

    [NotifyPropertyChangedFor(nameof(FieldsEnabled), nameof(SaveLabel))]
    [ObservableProperty] private bool _isBusy;

    /// <summary>確認中は欄を操作不可にする。</summary>
    public bool FieldsEnabled => !IsBusy;

    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>保存に成功したときに出る（窓を閉じる合図）。</summary>
    public event EventHandler? Saved;

    partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    /// <summary>ログインを確かめてから置き換える。成功なら true。失敗は ErrorText に出す。</summary>
    public async Task<bool> SaveAsync()
    {
        if (IsBusy) return false;
        if (string.IsNullOrWhiteSpace(Password)) { ErrorText = Strings.AC_22; return false; }

        ErrorText = string.Empty;
        IsBusy = true;
        ReplacePasswordResult result;
        try
        {
            result = await _service.ReplacePasswordAsync(_account.Id, Password);
        }
        finally
        {
            IsBusy = false;
        }

        switch (result)
        {
            case ReplacePasswordResult.Ok:
                Saved?.Invoke(this, EventArgs.Empty);
                return true;
            case ReplacePasswordResult.AuthFailed: ErrorText = Strings.AC_06; return false;
            case ReplacePasswordResult.NetworkError: ErrorText = Strings.AC_07; return false;
            case ReplacePasswordResult.EmptyPassword: ErrorText = Strings.AC_22; return false;
            default: ErrorText = Strings.AC_07; return false;
        }
    }
}
