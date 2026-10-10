using ParentMail.App.Features.AddressBook;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AddressBook;

// 編集ダイアログの検証と、アドレス帳一覧の操作のテスト（画面は使わない）。
public class ViewModelTests
{
    private const string Acc = "a1";

    private static ContactEditViewModel NewEdit(InMemoryContactRepository repo, Contact? existing = null)
        => new(repo, Acc, existing);

    private static void Fill(ContactEditViewModel vm, string name, params string[] emails)
    {
        vm.Name = name;
        while (vm.Emails.Count < emails.Length) vm.AddEmail();
        for (var i = 0; i < emails.Length; i++) vm.Emails[i].Text = emails[i];
    }

    [Fact]
    public void New_dialog_starts_with_one_empty_address_row_and_add_title()
    {
        var vm = NewEdit(new InMemoryContactRepository());
        Assert.Single(vm.Emails);
        Assert.Equal("", vm.Emails[0].Text);
        Assert.Equal("人を追加", vm.Title);
        Assert.False(vm.IsEdit);
    }

    [Fact]
    public void Empty_name_shows_AB07_and_does_not_save()
    {
        var repo = new InMemoryContactRepository();
        var vm = NewEdit(repo);
        Fill(vm, "  ", "a@example.com");
        Assert.False(vm.TrySave());
        Assert.Equal(Strings.AB_07, vm.ErrorText);
        Assert.Empty(repo.GetAll(Acc));
    }

    [Fact]
    public void No_address_shows_AB10()
    {
        var vm = NewEdit(new InMemoryContactRepository());
        Fill(vm, "山田", "", "  ");
        Assert.False(vm.TrySave());
        Assert.Equal(Strings.AB_10, vm.ErrorText);
    }

    [Fact]
    public void Bad_address_shows_AB09()
    {
        var vm = NewEdit(new InMemoryContactRepository());
        Fill(vm, "山田", "a@example.com", "bad-address");
        Assert.False(vm.TrySave());
        Assert.Equal(Strings.AB_09, vm.ErrorText);
    }

    [Fact]
    public void Duplicate_address_in_account_shows_AB06()
    {
        var repo = new InMemoryContactRepository();
        repo.Add(new Contact(0, Acc, "既存", "", new[] { "taro@example.com" }, ""));
        var vm = NewEdit(repo);
        Fill(vm, "新規", "TARO@example.com");
        Assert.False(vm.TrySave());
        Assert.Equal(Strings.AB_06, vm.ErrorText);
        Assert.Single(repo.GetAll(Acc));
    }

    [Fact]
    public void Save_trims_ignores_blank_rows_and_dedupes_same_address()
    {
        var repo = new InMemoryContactRepository();
        var vm = NewEdit(repo);
        vm.Reading = " やまだ ";
        Fill(vm, " 山田 太郎 ", " a@example.com ", "", "A@Example.com", "b@example.com");
        vm.Memo = "弟";
        Assert.True(vm.TrySave());
        Assert.Equal("", vm.ErrorText);
        var saved = Assert.Single(repo.GetAll(Acc));
        Assert.Equal("山田 太郎", saved.Name);
        Assert.Equal("やまだ", saved.Reading);
        Assert.Equal(new[] { "a@example.com", "b@example.com" }, saved.Emails);
        Assert.Equal("弟", saved.Memo);
        Assert.Equal(saved.Id, vm.Result!.Id);
    }

    [Fact]
    public void Editing_existing_may_keep_own_address_and_updates_in_place()
    {
        var repo = new InMemoryContactRepository();
        var c = repo.Add(new Contact(0, Acc, "山田", "やまだ", new[] { "taro@example.com" }, ""));
        var vm = NewEdit(repo, c);
        Assert.True(vm.IsEdit);
        Assert.Equal("人を編集", vm.Title);
        Assert.Equal("taro@example.com", vm.Emails[0].Text);
        vm.Name = "山田 太郎";
        Assert.True(vm.TrySave());
        var saved = Assert.Single(repo.GetAll(Acc));
        Assert.Equal("山田 太郎", saved.Name);
    }

    [Fact]
    public void Remove_row_keeps_at_least_one_row()
    {
        var vm = NewEdit(new InMemoryContactRepository());
        vm.AddEmail();
        Assert.Equal(2, vm.Emails.Count);
        vm.RemoveEmail(vm.Emails[1]);
        Assert.Single(vm.Emails);
        vm.Emails[0].Text = "x@example.com";
        vm.RemoveEmail(vm.Emails[0]);
        Assert.Single(vm.Emails);
        Assert.Equal("", vm.Emails[0].Text);
    }

    [Fact]
    public void Prefilled_from_sender()
    {
        var vm = new ContactEditViewModel(new InMemoryContactRepository(), Acc, null, "送信者", "s@example.com");
        Assert.Equal("送信者", vm.Name);
        Assert.Equal("s@example.com", vm.Emails[0].Text);
        Assert.False(vm.IsEdit);
    }

    // ---- アドレス帳一覧 ----

    private sealed class RecordingDialogs : IDialogService
    {
        public List<(string Title, string Message, string Yes, string No, bool SafeIsDefault)> Confirms { get; } = new();
        public bool ConfirmAnswer { get; set; }
        public void ShowNotice(string title, string message) { }
        public bool Confirm(string title, string message, string yesText, string noText, bool safeIsDefault = true)
        {
            Confirms.Add((title, message, yesText, noText, safeIsDefault));
            return ConfirmAnswer;
        }
        public int Choose(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int safeIndex) => -1;
    }

    private sealed class FakePresenter : IContactEditPresenter
    {
        public Action<ContactEditViewModel>? Fill { get; set; }
        public int Shown { get; private set; }
        public ContactEditViewModel? Last { get; private set; }
        public bool ShowEdit(ContactEditViewModel vm)
        {
            Shown++;
            Last = vm;
            if (Fill is null) return false; // やめる
            Fill(vm);
            return vm.TrySave();
        }
    }

    private static (AddressBookViewModel Vm, InMemoryContactRepository Repo, FakePresenter Presenter, RecordingDialogs Dialogs) NewBook()
    {
        var repo = new InMemoryContactRepository();
        var presenter = new FakePresenter();
        var dialogs = new RecordingDialogs();
        return (new AddressBookViewModel(Acc, "母のメール", repo, dialogs, presenter), repo, presenter, dialogs);
    }

    [Fact]
    public void Book_title_and_empty_state()
    {
        var (vm, _, _, _) = NewBook();
        Assert.Equal("アドレス帳 - 母のメール", vm.Title);
        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public void Add_saves_and_reloads_sorted_and_selects_new_row()
    {
        var (vm, repo, presenter, _) = NewBook();
        repo.Add(new Contact(0, Acc, "山田", "やまだ", new[] { "y@example.com" }, ""));
        vm.Reload();
        presenter.Fill = e => { e.Name = "阿部"; e.Reading = "あべ"; e.Emails[0].Text = "a@example.com"; };
        vm.AddCommand.Execute(null);
        Assert.Equal(new[] { "阿部", "山田" }, vm.Items.Select(i => i.Name).ToArray());
        Assert.Equal("阿部", vm.SelectedItem!.Name);
        Assert.False(vm.IsEmpty);
        Assert.False(presenter.Last!.IsEdit);
    }

    [Fact]
    public void Add_cancelled_leaves_list_unchanged()
    {
        var (vm, repo, presenter, _) = NewBook();
        vm.AddCommand.Execute(null);
        Assert.Equal(1, presenter.Shown);
        Assert.Empty(repo.GetAll(Acc));
    }

    [Fact]
    public void Edit_requires_selection_and_updates()
    {
        var (vm, repo, presenter, _) = NewBook();
        var c = repo.Add(new Contact(0, Acc, "山田", "やまだ", new[] { "y@example.com" }, ""));
        Assert.False(vm.EditCommand.CanExecute(null)); // 空のうちは選べない
        vm.Reload();
        Assert.NotNull(vm.SelectedItem); // 読み込み後は先頭の人が選ばれる
        Assert.True(vm.EditCommand.CanExecute(null));
        presenter.Fill = e => e.Memo = "兄";
        vm.EditCommand.Execute(null);
        Assert.True(presenter.Last!.IsEdit);
        Assert.Equal("兄", repo.GetAll(Acc).Single(x => x.Id == c.Id).Memo);
        Assert.Equal("兄", vm.Items[0].Memo);
    }

    [Fact]
    public void Delete_confirms_with_safe_default_and_removes()
    {
        var (vm, repo, _, dialogs) = NewBook();
        repo.Add(new Contact(0, Acc, "山田", "やまだ", new[] { "y@example.com" }, ""));
        vm.Reload();
        vm.SelectedItem = vm.Items[0];
        dialogs.ConfirmAnswer = false;
        vm.DeleteCommand.Execute(null);
        Assert.Single(repo.GetAll(Acc));

        dialogs.ConfirmAnswer = true;
        vm.DeleteCommand.Execute(null);
        Assert.Empty(repo.GetAll(Acc));
        Assert.True(vm.IsEmpty);
        var last = dialogs.Confirms.Last();
        Assert.Equal(Strings.WD_13, last.Title);
        Assert.Equal("この人を削除しますか?", last.Message);
        Assert.Equal("削除", last.Yes);
        Assert.Equal("やめる", last.No);
        Assert.True(last.SafeIsDefault);
    }

    [Fact]
    public void Text_helpers_split_combined_strings()
    {
        Assert.Equal(new[] { "名前", "ふりがな", "メールアドレス", "メモ" }, AddressBookText.Columns);
        Assert.Equal(new[] { "追加", "編集", "削除", "閉じる" }, AddressBookText.Buttons);
        Assert.Equal("アドレスを増やす", AddressBookText.AddAddress);
        Assert.Equal("この行をはずす", AddressBookText.RemoveAddress);
        Assert.Equal("保存", AddressBookText.Save);
        Assert.Equal("やめる", AddressBookText.Cancel);
        Assert.Equal("人を追加", AddressBookText.AddTitle);
        Assert.Equal("人を編集", AddressBookText.EditTitle);
    }
}
