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

    // 再取得でボタンを作り直すと、押したボタンが消えてキーボードフォーカスを失う（Issue #1 の指摘）。同じ項目を更新し続ける。
    [Fact]
    public void Switch_KeepsSameItemInstances_AndUpdatesInPlace()
    {
        _unread["a1"] = 7; _unread["a2"] = 3;
        var vm = Vm(("a1", "A"), ("a2", "B"));
        var first = vm.Items[0];
        var second = vm.Items[1];
        var changes = 0;
        vm.Items.CollectionChanged += (_, _) => changes++;
        var raised = new List<string?>();
        first.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        second.SelectCommand.Execute(null);

        Assert.Same(first, vm.Items[0]);
        Assert.Same(second, vm.Items[1]);
        Assert.Equal(0, changes);
        Assert.False(first.IsSelected);
        Assert.Equal(7, first.Unread);
        Assert.Contains(nameof(AccountButtonItem.IsSelected), raised);
        Assert.Contains(nameof(AccountButtonItem.ShowBadge), raised);
        Assert.Contains(nameof(AccountButtonItem.Unread), raised);
    }

    [Fact]
    public void Refresh_UnreadAndRename_UpdateExistingItems_WithoutCollectionChange()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"));
        var second = vm.Items[1];
        var changes = 0;
        vm.Items.CollectionChanged += (_, _) => changes++;

        _unread["a2"] = 6;
        _repo.Rename("a2", "新しい名前");
        vm.Refresh();

        Assert.Same(second, vm.Items[1]);
        Assert.Equal(6, second.Unread);
        Assert.Equal("新しい名前", second.Name);
        Assert.Equal("新しい名前", second.ToolTip);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Refresh_AddAndDelete_OnlyAddsOrRemovesChangedItems()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"), ("a3", "C"));
        var a1 = vm.Items[0];
        var a3 = vm.Items[2];

        _repo.Delete("a2");
        vm.Refresh();
        Assert.Equal(new[] { "a1", "a3" }, vm.Items.Select(i => i.AccountId));
        Assert.Same(a1, vm.Items[0]);
        Assert.Same(a3, vm.Items[1]);

        _repo.Add(new Account("a4", "a4@gmail.com", "D", 3));
        vm.Refresh();
        Assert.Equal(new[] { "a1", "a3", "a4" }, vm.Items.Select(i => i.AccountId));
        Assert.Same(a1, vm.Items[0]);
        Assert.Same(a3, vm.Items[1]);
    }

    [Fact]
    public void CurrentDeleted_SelectionMovesToRemaining_KeepingItsItem()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"));
        var second = vm.Items[1];
        _repo.Delete("a1");
        vm.Refresh();
        Assert.Same(second, Assert.Single(vm.Items));
        Assert.True(second.IsSelected);
        Assert.Equal(0, second.Unread);
    }

    [Fact]
    public void FiveAccounts_AllShown_InRegistrationOrder_WithSingleSelected()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"), ("a3", "C"), ("a4", "D"), ("a5", "E"));
        Assert.Equal(new[] { "a1", "a2", "a3", "a4", "a5" }, vm.Items.Select(i => i.AccountId));
        Assert.Single(vm.Items, i => i.IsSelected);
    }

    [Theory]
    [InlineData(1, 300)]
    [InlineData(2, 300)]
    [InlineData(3, 220)]
    [InlineData(5, 220)]
    public void ButtonMaxWidth_DependsOnAccountCount(int count, double expected)
    {
        var vm = Vm(Enumerable.Range(1, count).Select(i => ($"a{i}", $"N{i}")).ToArray());
        Assert.Equal(expected, vm.ButtonMaxWidthDip);
    }

    [Fact]
    public void ButtonMaxWidth_ChangesWhenCountCrossesTwo_AndRaisesPropertyChanged()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"));
        var raised = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AccountSwitchViewModel.ButtonMaxWidthDip)) raised++; };
        _repo.Add(new Account("a3", "a3@gmail.com", "C", 2));
        vm.Refresh();
        Assert.Equal(220, vm.ButtonMaxWidthDip);
        Assert.Equal(1, raised);
    }
}
