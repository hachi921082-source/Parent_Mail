using CommunityToolkit.Mvvm.ComponentModel;
using ParentMail.App.Resources;
using ParentMail.Core.Features.AccountsAuth;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>名前を変える窓の入力と保存（画面を出さずにテストできる）。確認ダイアログは付けない。</summary>
public sealed partial class RenameViewModel : ObservableObject
{
    private readonly AccountService _service;
    private readonly string _accountId;

    public RenameViewModel(AccountService service, string accountId, string currentName)
    {
        _service = service;
        _accountId = accountId;
        _displayName = currentName;
    }

    public string Title => Strings.WD_05;
    public string NameLabel => Strings.AC_24;
    public string SaveLabel => AccountStrings.RenameSave;
    public string CancelLabel => AccountStrings.RenameCancel;

    [ObservableProperty] private string _displayName;
    [ObservableProperty] private string _errorText = string.Empty;

    /// <summary>保存する。空のときは窓を閉じずに赤い行を出す。成功なら true。</summary>
    public bool TrySave()
    {
        var result = _service.Rename(_accountId, DisplayName);
        ErrorText = result == RenameResult.Ok ? string.Empty : Strings.AC_23;
        return result == RenameResult.Ok;
    }
}
