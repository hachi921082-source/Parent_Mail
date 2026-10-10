using NSubstitute;
using ParentMail.App.Features.MailList;
using ParentMail.Core;
using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Features.FilterRules;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.MailList;

// 一覧ペイン下部のボタン帯の入口（各機能の窓・作成窓の契約・差し込み口）のテスト。
public sealed class ListButtonStripViewModelTests
{
    private readonly FakeAppState _state = new() { CurrentAccountId = "acc1" };
    private readonly FakeTextScale _scale = new();
    private readonly FakeComposeWindowFactory _compose = new();
    private readonly IAddressBookService _book = Substitute.For<IAddressBookService>();
    private readonly IBlockListService _block = Substitute.For<IBlockListService>();
    private readonly IFilterRulesService _filter = Substitute.For<IFilterRulesService>();

    private ListButtonStripViewModel Vm()
        => new(_state, _scale, _compose, _book, _block, _filter);

    [Fact]
    public void AddressBook_BlockList_FilterRules_OpenTheirWindowsForCurrentAccount()
    {
        var vm = Vm();
        vm.AddressBookCommand.Execute(null);
        vm.BlockListCommand.Execute(null);
        vm.FilterRulesCommand.Execute(null);

        _book.Received(1).OpenBook("acc1");
        _block.Received(1).OpenWindow("acc1");
        _filter.Received(1).OpenWindow("acc1");
    }

    [Fact]
    public void Compose_OpensEmptyComposeForCurrentAccount()
    {
        Vm().ComposeCommand.Execute(null);
        var init = Assert.Single(_compose.Opened);
        Assert.Equal("acc1", init.AccountId);
        Assert.Empty(init.To);
        Assert.Equal("", init.Subject);
    }

    [Fact]
    public void Compose_WithoutFactory_DoesNothing()
    {
        var vm = new ListButtonStripViewModel(_state, _scale);
        vm.ComposeCommand.Execute(null); // 例外にならない
        Assert.Empty(_compose.Opened);
    }

    [Fact]
    public void SearchAndSettings_RaisePlugInEvents()
    {
        var vm = Vm();
        var search = 0;
        var settings = 0;
        vm.SearchRequested += (_, _) => search++;
        vm.SettingsRequested += (_, _) => settings++;
        vm.SearchCommand.Execute(null);
        vm.SettingsCommand.Execute(null);
        vm.SettingsCommand.Execute(null);
        Assert.Equal(1, search);
        Assert.Equal(2, settings);
    }

    [Fact]
    public void WithoutAccount_AccountDependentButtonsAreDisabled_AndFollowAccountChanges()
    {
        _state.CurrentAccountId = null;
        var vm = Vm();
        Assert.False(vm.ComposeCommand.CanExecute(null));
        Assert.False(vm.AddressBookCommand.CanExecute(null));
        Assert.False(vm.BlockListCommand.CanExecute(null));
        Assert.False(vm.FilterRulesCommand.CanExecute(null));
        vm.AddressBookCommand.Execute(null);
        _book.DidNotReceive().OpenBook(Arg.Any<string>());

        _state.CurrentAccountId = "acc2";
        Assert.True(vm.ComposeCommand.CanExecute(null));
        Assert.True(vm.AddressBookCommand.CanExecute(null));
    }

    [Fact]
    public void ChromeFontSize_FollowsTextScaleChromeSize()
    {
        var vm = Vm();
        Assert.Equal(_scale.ChromeFontSizeDip, vm.ChromeFontSize);
    }
}
