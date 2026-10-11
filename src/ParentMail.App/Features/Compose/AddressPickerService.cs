using System.Windows;
using MimeKit;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.Compose;

namespace ParentMail.App.Features.Compose;

/// <summary>アドレス帳から選ぶ画面を出して、選ばれた宛先を返す（作成画面の ViewModel が使う。試験では差し替える）。</summary>
public interface IAddressPickerService
{
    /// <summary>
    /// accountId（作成窓のアカウント）のアドレス帳から選んでもらう。existingAddresses はその欄にすでにあるアドレス。
    /// 「入れる」なら選ばれた宛先（一覧の順）、「やめる」やアドレス帳が空なら null。
    /// </summary>
    IReadOnlyList<MailboxAddress>? Pick(Window? owner, string accountId, IReadOnlyList<string> existingAddresses);
}

/// <summary>
/// 本物の実装。アドレス帳が空なら画面を開かずお知らせ [CP-22] を出す。
/// 画面の出し方は差し替えられる（試験では選択の代役を渡す）。
/// </summary>
public sealed class AddressPickerService : IAddressPickerService
{
    private readonly IContactRepository _contacts;
    private readonly IAccountRepository _accounts;
    private readonly IDialogService _dialogs;
    private readonly ITextScale _scale;
    private readonly Func<AddressPickerViewModel, Window?, bool> _show;

    public AddressPickerService(IContactRepository contacts, IAccountRepository accounts, IDialogService dialogs, ITextScale scale,
        Func<AddressPickerViewModel, Window?, bool>? show = null)
    {
        _contacts = contacts;
        _accounts = accounts;
        _dialogs = dialogs;
        _scale = scale;
        _show = show ?? ShowDialog;
    }

    public IReadOnlyList<MailboxAddress>? Pick(Window? owner, string accountId, IReadOnlyList<string> existingAddresses)
    {
        var rows = AddressPickerRows.Build(_contacts.GetAll(accountId), existingAddresses);
        if (rows.Count == 0)
        {
            _dialogs.ShowNotice(Strings.WD_10, Strings.CP_22);
            return null;
        }

        var accountName = _accounts.Get(accountId)?.DisplayName ?? "";
        var vm = new AddressPickerViewModel(rows, accountName);
        return _show(vm, owner) ? vm.Result : null;
    }

    private bool ShowDialog(AddressPickerViewModel vm, Window? owner)
        => new AddressPickerDialog(vm, _scale, owner).ShowForOwner();
}
