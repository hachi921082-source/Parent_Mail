using System.Windows;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.AddressBook;

namespace ParentMail.App.Features.AddressBook;

// アドレス帳の窓を開く実装。ウィンドウの所有者は、いま操作中の窓（なければメイン窓）。
public sealed class AddressBookService : IAddressBookService, IContactEditPresenter
{
    private readonly IContactRepository _repo;
    private readonly IAccountRepository _accounts;
    private readonly IDialogService _dialogs;
    private readonly ITextScale _scale;

    public AddressBookService(IContactRepository repo, IAccountRepository accounts, IDialogService dialogs, ITextScale scale)
    {
        _repo = repo;
        _accounts = accounts;
        _dialogs = dialogs;
        _scale = scale;
    }

    public void OpenBook(string accountId)
    {
        var name = _accounts.Get(accountId)?.DisplayName ?? "";
        var vm = new AddressBookViewModel(accountId, name, _repo, _dialogs, this);
        new AddressBookWindow(vm, _scale, FindOwner()).ShowDialog();
    }

    public bool AddFromSender(string accountId, string name, string address)
    {
        // 登録済みなら重ねて登録させず、その旨を知らせる
        if (_repo.FindByEmail(accountId, address) is not null)
        {
            _dialogs.ShowNotice(AddressBookText.AddTitle, Strings.AB_06);
            return false;
        }
        var vm = new ContactEditViewModel(_repo, accountId, null, name, address);
        return ShowEdit(vm);
    }

    public bool ShowEdit(ContactEditViewModel vm)
        => new ContactEditWindow(vm, _scale, FindOwner()).ShowDialog() == true;

    private static Window? FindOwner()
    {
        var windows = Application.Current.Windows.OfType<Window>().Where(w => w.IsVisible).ToList();
        return windows.FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow ?? windows.FirstOrDefault();
    }
}
