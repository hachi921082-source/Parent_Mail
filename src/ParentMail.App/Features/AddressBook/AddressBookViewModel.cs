using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.AddressBook;

/// <summary>一覧の 1 行。</summary>
public sealed class ContactRowViewModel
{
    public ContactRowViewModel(Contact contact) => Contact = contact;

    public Contact Contact { get; }
    public long Id => Contact.Id;
    public string Name => Contact.Name;
    public string Reading => Contact.Reading;
    public string Memo => Contact.Memo;
    public string EmailsText => string.Join(", ", Contact.Emails);
}

// アドレス帳ウィンドウの状態。一覧は常にふりがな順（リポジトリが整列して返す）。
public sealed partial class AddressBookViewModel : ObservableObject
{
    private readonly string _accountId;
    private readonly IContactRepository _repo;
    private readonly IDialogService _dialogs;
    private readonly IContactEditPresenter _presenter;

    public AddressBookViewModel(string accountId, string accountName, IContactRepository repo,
        IDialogService dialogs, IContactEditPresenter presenter)
    {
        _accountId = accountId;
        _repo = repo;
        _dialogs = dialogs;
        _presenter = presenter;
        Title = AddressBookText.BookTitle(accountName);
        Reload();
    }

    public string Title { get; }

    public ObservableCollection<ContactRowViewModel> Items { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private ContactRowViewModel? _selectedItem;

    /// <summary>登録が 0 件のとき true（「まだ登録されていません」を出す）。</summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>一覧を読み直す。selectId があればその人を選ぶ。</summary>
    public void Reload(long? selectId = null)
    {
        var keep = selectId ?? SelectedItem?.Id;
        Items.Clear();
        foreach (var c in ContactSorter.Sort(_repo.GetAll(_accountId))) Items.Add(new ContactRowViewModel(c));
        IsEmpty = Items.Count == 0;
        SelectedItem = Items.FirstOrDefault(i => i.Id == keep) ?? Items.FirstOrDefault();
    }

    [RelayCommand]
    private void Add()
    {
        var vm = new ContactEditViewModel(_repo, _accountId);
        if (_presenter.ShowEdit(vm)) Reload(vm.Result?.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Edit()
    {
        if (SelectedItem is null) return;
        var vm = new ContactEditViewModel(_repo, _accountId, SelectedItem.Contact);
        if (_presenter.ShowEdit(vm)) Reload(vm.Result?.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (SelectedItem is null) return;
        // 既定ボタンは「やめる」（安全側）
        var ok = _dialogs.Confirm(Strings.WD_13, AddressBookText.DeleteQuestion,
            AddressBookText.DeleteYes, AddressBookText.DeleteNo, safeIsDefault: true);
        if (!ok) return;

        var index = Items.IndexOf(SelectedItem);
        _repo.Delete(SelectedItem.Id);
        Reload();
        // 消した位置の次の人を選んでおく（キーボードだけで続けて操作できる）
        if (Items.Count > 0) SelectedItem = Items[Math.Min(index, Items.Count - 1)];
    }

    private bool HasSelection() => SelectedItem is not null;
}
