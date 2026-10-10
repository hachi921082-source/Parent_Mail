using ParentMail.App.Resources;
using ParentMail.Core;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>
/// 注意ダイアログ（Q31）。アカウントの追加・削除・パスワードの入れ直しの前に必ず通す。
/// 既定ボタンはキャンセル（Enter = キャンセル）。暗証番号などは付けない。名前変更では使わない。
/// </summary>
public sealed class WarningGate
{
    private readonly IDialogService _dialog;

    public WarningGate(IDialogService dialog) => _dialog = dialog;

    /// <summary>「続ける」が押されたら true。キャンセル・Esc・× は false。</summary>
    public Task<bool> ConfirmAsync()
        => Task.FromResult(_dialog.Confirm(Strings.WD_06, Strings.AC_13,
            AccountStrings.WarnContinue, AccountStrings.WarnCancel, safeIsDefault: true));
}
