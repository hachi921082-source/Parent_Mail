using NSubstitute;
using ParentMail.App.Features.AccountsAuth;
using ParentMail.Core;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AccountsAuth;

// アカウント切替ボタンと、もう一方のアカウントの未読バッジのテスト。
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

    [Fact]
    public void NoAccount_NoCurrent_EmptyLabel_SwitchDisabled()
    {
        var vm = Vm();
        Assert.Null(_state.CurrentAccountId);
        Assert.Equal("", vm.Label);
        Assert.False(vm.SwitchCommand.CanExecute(null));
    }

    [Fact]
    public void OneAccount_ShowsNameOnly_AndSwitchDoesNothing()
    {
        _unread["a1"] = 5;
        var vm = Vm(("a1", "お母さん"));
        Assert.Equal("お母さん", vm.Label);
        Assert.Equal(0, vm.OtherUnread);
        Assert.False(vm.HasBadge);
        Assert.False(vm.SwitchCommand.CanExecute(null));
        vm.SwitchCommand.Execute(null);
        Assert.Equal("a1", _state.CurrentAccountId);
    }

    [Fact]
    public void Initialize_UsesLastAccountId_ElseFirst()
    {
        _settings.Save(_settings.Get() with { LastAccountId = "a2" });
        var vm = Vm(("a1", "A"), ("a2", "B"));
        Assert.Equal("a2", _state.CurrentAccountId);
        Assert.Equal("B", vm.Label);
    }

    [Fact]
    public void Initialize_UnknownLastAccountId_FallsBackToFirst()
    {
        _settings.Save(_settings.Get() with { LastAccountId = "gone" });
        Vm(("a1", "A"), ("a2", "B"));
        Assert.Equal("a1", _state.CurrentAccountId);
    }

    [Fact]
    public void Badge_ShowsOtherAccountInboxUnread_OnlyWhenPositive()
    {
        _unread["a1"] = 7; _unread["a2"] = 3;
        var vm = Vm(("a1", "A"), ("a2", "B"));
        Assert.Equal(3, vm.OtherUnread);
        Assert.True(vm.HasBadge);
        Assert.Equal("3", vm.BadgeText);

        _unread["a2"] = 0;
        vm.Refresh();
        Assert.Equal(0, vm.OtherUnread);
        Assert.False(vm.HasBadge);
        Assert.Equal("", vm.BadgeText);
    }

    [Fact]
    public void Switch_ChangesCurrent_SavesLastId_AndSwapsBadge()
    {
        _unread["a1"] = 7; _unread["a2"] = 3;
        var vm = Vm(("a1", "A"), ("a2", "B"));

        vm.SwitchCommand.Execute(null);

        Assert.Equal("a2", _state.CurrentAccountId);
        Assert.Equal("a2", _settings.Get().LastAccountId);
        Assert.Equal("B", vm.Label);
        Assert.Equal(7, vm.OtherUnread);

        vm.SwitchCommand.Execute(null);
        Assert.Equal("a1", _state.CurrentAccountId);
        Assert.Equal(3, vm.OtherUnread);
    }

    [Fact]
    public async Task SyncCompleted_OfOtherAccount_RefreshesBadge_OfCurrentDoesNot()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"));
        Assert.Equal(0, vm.OtherUnread);

        _unread["a2"] = 4;
        await _sync.SyncNowAsync("a2", SyncTrigger.Timer);
        Assert.Equal(4, vm.OtherUnread);

        _mail.ClearReceivedCalls();
        await _sync.SyncNowAsync("a1", SyncTrigger.Timer);
        _mail.DidNotReceive().UnreadCount("a2", FolderKind.Inbox);
    }

    [Fact]
    public void AccountAdded_Refresh_ShowsNewOtherAccount()
    {
        _unread["a2"] = 2;
        var vm = Vm(("a1", "A"));
        Assert.False(vm.SwitchCommand.CanExecute(null));

        _repo.Add(new Account("a2", "a2@gmail.com", "B", 1));
        vm.Refresh();

        Assert.True(vm.SwitchCommand.CanExecute(null));
        Assert.Equal(2, vm.OtherUnread);
    }

    [Fact]
    public void Refresh_AfterCurrentDeleted_FallsBackToRemaining()
    {
        var vm = Vm(("a1", "A"), ("a2", "B"));
        _repo.Delete("a1");
        vm.Refresh();
        Assert.Equal("a2", _state.CurrentAccountId);
        Assert.Equal("B", vm.Label);
    }
}
