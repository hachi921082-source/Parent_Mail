using NSubstitute;
using ParentMail.App.Features.AccountsAuth;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AccountsAuth;

// アカウント追加画面の入力検証・状態・やめる操作のテスト（画面そのものは出さない）。
public sealed class AddAccountViewModelTests
{
    private readonly InMemoryAccountRepository _repo = new();
    private readonly FakeCredentialStore _cred = new();
    private readonly FakeDialogService _dialog = new();
    private readonly IImapGatewayFactory _factory = Substitute.For<IImapGatewayFactory>();
    private LoginResult _login = LoginResult.Ok;
    private TaskCompletionSource<LoginResult>? _gate;

    private AddAccountViewModel Vm(bool firstLaunch = false)
    {
        _factory.Create(Arg.Any<Account>()).Returns(_ =>
        {
            var gw = Substitute.For<IImapGateway>();
            gw.TestLoginAsync(Arg.Any<CancellationToken>()).Returns(_ => _gate?.Task ?? Task.FromResult(_login));
            return gw;
        });
        var svc = new AccountService(_repo, _cred, _factory);
        return new AddAccountViewModel(svc, _dialog, firstLaunch);
    }

    [Fact]
    public void DisplayName_DefaultsToLocalPart_UntilUserEdits()
    {
        var vm = Vm();
        vm.Email = "taro@gmail.com";
        Assert.Equal("taro", vm.DisplayName);
        vm.Email = "jiro@gmail.com";
        Assert.Equal("jiro", vm.DisplayName);
        vm.DisplayName = "おじいちゃん";
        vm.Email = "saburo@gmail.com";
        Assert.Equal("おじいちゃん", vm.DisplayName);
    }

    [Theory]
    [InlineData("", "p", "n", "AC_21")]
    [InlineData("bad", "p", "n", "AC_21")]
    [InlineData("a@gmail.com", "", "n", "AC_22")]
    [InlineData("a@gmail.com", "p", "  ", "AC_23")]
    public async Task FieldErrors_ShownInPlace_NothingSaved(string email, string pw, string name, string expectedId)
    {
        var vm = Vm();
        vm.Email = email; vm.Password = pw; vm.DisplayName = name;

        var ok = await vm.SubmitAsync();

        Assert.False(ok);
        Assert.Equal(StringById(expectedId), vm.ErrorText);
        Assert.Empty(_repo.GetAll());
        _factory.DidNotReceive().Create(Arg.Any<Account>());
    }

    private static string StringById(string id) => id switch
    {
        "AC_21" => Strings.AC_21,
        "AC_22" => Strings.AC_22,
        "AC_23" => Strings.AC_23,
        _ => throw new ArgumentException(id),
    };

    [Fact]
    public async Task Success_AddsAccount_ClearsError_AndRaisesAdded()
    {
        var vm = Vm();
        string? addedId = null;
        vm.Added += (_, id) => addedId = id;
        vm.Email = "a@gmail.com"; vm.Password = "dummy";

        var ok = await vm.SubmitAsync();

        Assert.True(ok);
        Assert.Equal("", vm.ErrorText);
        Assert.Equal(_repo.GetAll()[0].Id, addedId);
        Assert.False(vm.IsBusy);
    }

    [Theory]
    [InlineData(LoginResult.AuthFailed)]
    [InlineData(LoginResult.NetworkError)]
    public async Task LoginFailure_ShowsMatchingError_AndSavesNothing(LoginResult login)
    {
        _login = login;
        var vm = Vm();
        vm.Email = "a@gmail.com"; vm.Password = "bad";

        Assert.False(await vm.SubmitAsync());

        Assert.Equal(login == LoginResult.AuthFailed ? Strings.AC_06 : Strings.AC_07, vm.ErrorText);
        Assert.Empty(_repo.GetAll());
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Duplicate_ShowsAc05()
    {
        var vm = Vm();
        _repo.Add(new Account("x", "A@gmail.com", "x", 0));
        vm.Email = "a@gmail.com"; vm.Password = "p";

        Assert.False(await vm.SubmitAsync());
        Assert.Equal(Strings.AC_05, vm.ErrorText);
    }

    [Fact]
    public async Task WhileTesting_FieldsDisabled_ButtonShowsBusyText()
    {
        _gate = new TaskCompletionSource<LoginResult>();
        var vm = Vm();
        vm.Email = "a@gmail.com"; vm.Password = "p";

        var task = vm.SubmitAsync();

        Assert.True(vm.IsBusy);
        Assert.False(vm.FieldsEnabled);
        Assert.Equal(Strings.AC_08, vm.SubmitLabel);
        Assert.False(vm.SubmitCommand.CanExecute(null));

        _gate.SetResult(LoginResult.Ok);
        Assert.True(await task);
        Assert.True(vm.FieldsEnabled);
        Assert.Equal("登録する", vm.SubmitLabel);
    }

    [Fact]
    public void Cancel_NotFirstLaunch_JustCloses()
    {
        Assert.Equal(CancelOutcome.Close, Vm(firstLaunch: false).RequestCancel());
    }

    [Theory]
    [InlineData(true, CancelOutcome.QuitApp)]
    [InlineData(false, CancelOutcome.Stay)]
    public void Cancel_FirstLaunch_AsksConfirm(bool answerQuit, CancelOutcome expected)
    {
        _dialog.ConfirmAnswer = answerQuit;
        Assert.Equal(expected, Vm(firstLaunch: true).RequestCancel());
    }

    [Fact]
    public void Labels_AreSplitFromUiStrings()
    {
        var vm = Vm();
        Assert.Equal("メールアドレス", vm.EmailLabel);
        Assert.Equal("アプリパスワード", vm.PasswordLabel);
        Assert.Equal("表示名", vm.NameLabel);
        Assert.Equal("登録する", vm.SubmitLabel);
        Assert.Equal("やめる", vm.CancelLabel);
        Assert.Equal("このまま閉じるとアプリは終わります。よろしいですか?", AccountStrings.QuitConfirmText);
        Assert.Equal("終わる", AccountStrings.QuitYes);
        Assert.Equal("戻る", AccountStrings.QuitNo);
    }
}
