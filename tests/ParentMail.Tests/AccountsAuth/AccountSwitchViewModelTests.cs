using NSubstitute;
using ParentMail.App.Features.AccountsAuth;
using ParentMail.Core;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AccountsAuth;

// アカウント切替の 2 つ並びボタン（選択中 / 非選択）と、非選択側だけの未読バッジのテスト。
public sealed class AccountSwitchViewModelTests
{
    private readonly InMemoryAccountRepository _repo = new();
    private readonly FakeAppState _state = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeSyncService _sync = new();
    private readonly IMailRepository _mail = Substitute.For<IMailRepository>();
    private readonly Dictionary<string, int> _unread = new();

    public AccountSwitchViewModelTests()
    {
        _mail.UnreadCount(Arg.Any<string>(), FolderKind.Inbox).Returns(c => _unread.GetValueOrDefault(c.Arg<string>()));
    }

    private AccountSwitchViewModel Vm(params (string Id, string Name)[] accounts)
    {
        var i = 0;
        foreach (var (id, name) in accounts) _repo.Add(new Account(id, id + "@gmail.com", name, i++));
        var vm = new AccountSwitchViewModel(_repo, _state, _mail, _settings, _sync);
        vm.Initialize();
        return vm;
    }

    private static AccountButtonItem Item(AccountSwitchViewModel vm, string id) => vm.Items.Single(i => i.AccountId == id);

    [Fact]
    public void NoAccount_NoCurrent_NoItems()
    {
        var vm = Vm();
        Assert.Null(_state.CurrentAccountId);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public void OneAccount_ShowsSelectedButtonOnly_NoBadge_PressDoesNothing()
    {
        _unread["a1"] = 5;
        var vm = Vm(("a1", "お母さん"));
        var item = Assert.Single(vm.Items);
        Assert.Equal("お母さん", item.Name);
        Assert.Equal("お母さん", item.ToolTip);
        Assert.True(item.IsSelected);
        Assert.Equal(0, item.Unread);
        Assert.False(item.ShowBadge);
        item.SelectCommand.Execute(null);
        Assert.Equal("a1", _state.CurrentAccountId);
    }

    [Fact]
    public void TwoAccounts_RepositoryOrder_OneSelected()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"));
        Assert.Equal(new[] { "a1", "a2" }, vm.Items.Select(i => i.AccountId));
        Assert.True(Item(vm, "a1").IsSelected);
        Assert.False(Item(vm, "a2").IsSelected);
    }

    [Fact]
    public void Initialize_UsesLastAccountId_ElseFirst()
    {
        _settings.Save(_settings.Get() with { LastAccountId = "a2" });
        var vm = Vm(("a1", "A"), ("a2", "B"));
        Assert.Equal("a2", _state.CurrentAccountId);
        Assert.True(Item(vm, "a2").IsSelected);
    }

    [Fact]
    public void Initialize_UnknownLastAccountId_FallsBackToFirst()
    {
        _settings.Save(_settings.Get() with { LastAccountId = "gone" });
        Vm(("a1", "A"), ("a2", "B"));
        Assert.Equal("a1", _state.CurrentAccountId);
    }

    [Fact]
    public void Badge_OnlyOnNonSelected_OnlyWhenPositive()
    {
        _unread["a1"] = 7; _unread["a2"] = 3;
        var vm = Vm(("a1", "A"), ("a2", "B"));
        Assert.False(Item(vm, "a1").ShowBadge);
        Assert.Equal(0, Item(vm, "a1").Unread);
        Assert.True(Item(vm, "a2").ShowBadge);
        Assert.Equal(3, Item(vm, "a2").Unread);

        _unread["a2"] = 0;
        vm.Refresh();
        Assert.False(Item(vm, "a2").ShowBadge);
    }

    [Fact]
    public void SelectNonSelected_Switches_SavesLastId_AndBadgeMoves()
    {
        _unread["a1"] = 7; _unread["a2"] = 3;
        var vm = Vm(("a1", "A"), ("a2", "B"));

        Item(vm, "a2").SelectCommand.Execute(null);

        Assert.Equal("a2", _state.CurrentAccountId);
        Assert.Equal("a2", _settings.Get().LastAccountId);
        Assert.True(Item(vm, "a2").IsSelected);
        Assert.False(Item(vm, "a2").ShowBadge);
        Assert.True(Item(vm, "a1").ShowBadge);
        Assert.Equal(7, Item(vm, "a1").Unread);

        Item(vm, "a1").SelectCommand.Execute(null);
        Assert.Equal("a1", _state.CurrentAccountId);
        Assert.Equal(3, Item(vm, "a2").Unread);
    }

    [Fact]
    public void SelectSelected_DoesNothing()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"));
        var item = Item(vm, "a1");
        Assert.True(item.SelectCommand.CanExecute(null)); // 無効にするとフォーカスを受けられない
        item.SelectCommand.Execute(null);
        Assert.Equal("a1", _state.CurrentAccountId);
        Assert.Equal("a1", _settings.Get().LastAccountId ?? "a1");
    }

    [Fact]
    public async Task SyncCompleted_OfOtherAccount_RefreshesBadge_OfCurrentDoesNot()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"));
        Assert.Equal(0, Item(vm, "a2").Unread);

        _unread["a2"] = 4;
        await _sync.SyncNowAsync("a2", SyncTrigger.Timer);
        Assert.Equal(4, Item(vm, "a2").Unread);

        _mail.ClearReceivedCalls();
        await _sync.SyncNowAsync("a1", SyncTrigger.Timer);
        _mail.DidNotReceive().UnreadCount("a2", FolderKind.Inbox);
    }

    [Fact]
    public void AccountAdded_Refresh_ShowsSecondButton()
    {
        _unread["a2"] = 2;
        var vm = Vm(("a1", "A"));
        Assert.Single(vm.Items);

        _repo.Add(new Account("a2", "a2@gmail.com", "B", 1));
        vm.Refresh();

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(2, Item(vm, "a2").Unread);
        Assert.True(Item(vm, "a2").ShowBadge);
    }

    [Fact]
    public void Rename_Refresh_UpdatesNameAndToolTip()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"));
        _repo.Rename("a2", "とても長い名前");
        vm.Refresh();
        Assert.Equal("とても長い名前", Item(vm, "a2").Name);
        Assert.Equal("とても長い名前", Item(vm, "a2").ToolTip);
    }

    [Fact]
    public void Refresh_AfterCurrentDeleted_FallsBackToRemaining_SingleButton()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"));
        _repo.Delete("a1");
        vm.Refresh();
        Assert.Equal("a2", _state.CurrentAccountId);
        var item = Assert.Single(vm.Items);
        Assert.True(item.IsSelected);
    }

    [Fact]
    public void ItemsChange_RaisesPropertyChanged()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"));
        var raised = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AccountSwitchViewModel.Items)) raised++; };
        Item(vm, "a2").SelectCommand.Execute(null);
        Assert.True(raised > 0);
    }
}
