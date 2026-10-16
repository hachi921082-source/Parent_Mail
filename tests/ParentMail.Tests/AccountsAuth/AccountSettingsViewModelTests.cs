using ParentMail.App.Features.AccountsAuth;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AccountsAuth;

// 設定窓のアカウント欄（注意ダイアログの扱い、削除の流れ、名前変更・入れ直しの窓）のテスト。窓は出さず差し替える。
public sealed class AccountSettingsViewModelTests
{
    private sealed class FakeDialogs : IAccountDialogs
    {
        public List<string> Calls { get; } = new();
        public Action<RenameViewModel>? OnRename { get; set; }
        public Action<ReenterPasswordViewModel>? OnReenter { get; set; }
        public Action<bool>? OnAdd { get; set; }

        public bool ShowRename(RenameViewModel vm) { Calls.Add("rename"); OnRename?.Invoke(vm); return true; }
        public bool ShowReenter(ReenterPasswordViewModel vm) { Calls.Add("reenter"); OnReenter?.Invoke(vm); return true; }
        public bool ShowAdd(bool firstLaunch) { Calls.Add(firstLaunch ? "add-first" : "add"); OnAdd?.Invoke(firstLaunch); return true; }
    }

    // 注意ダイアログ（WD-06）と最終確認（WD-07）を見分けて答えるダイアログ
    private sealed class ScriptedDialog : IDialogService
    {
        public bool WarnAnswer { get; set; } = true;
        public bool ConfirmAnswer { get; set; } = true;
        public List<string> Titles { get; } = new();
        public List<(string Title, string Message)> Notices { get; } = new();
        public List<(string Title, string Message, string Yes, string No, bool Safe)> Confirms { get; } = new();

        public void ShowNotice(string title, string message) { Notices.Add((title, message)); Titles.Add("notice:" + title); }

        public bool Confirm(string title, string message, string yesText, string noText, bool safeIsDefault = true)
        {
            Confirms.Add((title, message, yesText, noText, safeIsDefault));
            Titles.Add("confirm:" + title);
            return title == Strings.WD_06 ? WarnAnswer : ConfirmAnswer;
        }

        public int Choose(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int safeIndex) => -1;
    }

    private readonly InMemoryAccountRepository _repo = new();
    private readonly FakeCredentialStore _cred = new();
    private readonly FakeSyncService _sync = new();
    private readonly FakeOutboxService _outbox = new();
    private readonly FakeImapGatewayFactory _gateways = new();
    private readonly ScriptedDialog _dialog = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly AccountService _svc;
    private readonly AccountSettingsViewModel _vm;

    public AccountSettingsViewModelTests()
    {
        _repo.Add(new Account("a1", "one@gmail.com", "お母さん", 0));
        _repo.Add(new Account("a2", "two@gmail.com", "お父さん", 1));
        _cred.Save("a1", "pw1");
        _cred.Save("a2", "pw2");
        _svc = new AccountService(_repo, _cred, _gateways, _sync, _outbox);
        _vm = new AccountSettingsViewModel(_svc, _repo, new WarningGate(_dialog), _dialog, _dialogs, dispatch: a => a());
    }

    [Fact]
    public async Task AccountEvent_FromBackgroundThread_ReloadsOnUiDispatch()
    {
        // UI スレッドに見立てた実行キュー。バックグラウンドで Reload が直接走らないことを確かめる
        var queue = new List<Action>();
        var vm = new AccountSettingsViewModel(_svc, _repo, new WarningGate(_dialog), _dialog, _dialogs,
            dispatch: a => { lock (queue) queue.Add(a); });
        _repo.Rename("a1", "おかあさん");

        // 背景スレッドから名前変更イベントを出す（ValidateAndAddAsync の ConfigureAwait(false) 後と同じ状況）
        var ex = await Record.ExceptionAsync(() => Task.Run(() => _svc.Rename("a2", "ちち")));

        Assert.Null(ex);
        Assert.Equal("お母さん", vm.Accounts[0].DisplayName); // まだ UI 側で実行されていない
        Action[] pending;
        lock (queue) pending = queue.ToArray();
        Assert.NotEmpty(pending);
        foreach (var a in pending) a();
        Assert.Equal(new[] { "おかあさん", "ちち" }, vm.Accounts.Select(x => x.DisplayName));
    }

    // 上限（5 件）まで足す
    private void FillToLimit()
    {
        for (var i = 3; i <= Constants.MaxAccounts; i++) _repo.Add(new Account($"a{i}", $"u{i}@gmail.com", $"名前{i}", i - 1));
        _vm.Reload();
    }

    [Fact]
    public void Accounts_ListNamesAndAddresses_AddEnabledBelowLimit()
    {
        Assert.Equal(new[] { "お母さん", "お父さん" }, _vm.Accounts.Select(a => a.DisplayName));
        Assert.Equal("two@gmail.com", _vm.Accounts[1].Email);
        Assert.True(_vm.AddCommand.CanExecute(null));
        Assert.Equal(string.Empty, _vm.AddNote);
    }

    [Fact]
    public void Accounts_AtFive_AddDisabledWithNote()
    {
        FillToLimit();
        Assert.Equal(5, _vm.Accounts.Count);
        Assert.False(_vm.AddCommand.CanExecute(null));
        Assert.Equal(Strings.AC_04, _vm.AddNote);
        Assert.Equal("アカウントは5つまでです", Strings.AC_04);
    }

    [Fact]
    public async Task WarningGate_Cancel_Aborts_AndDefaultIsCancel()
    {
        _dialog.WarnAnswer = false;
        Assert.False(await new WarningGate(_dialog).ConfirmAsync());

        var c = Assert.Single(_dialog.Confirms);
        Assert.Equal(Strings.WD_06, c.Title);
        Assert.Equal(Strings.AC_13, c.Message);
        Assert.Equal("続ける", c.Yes);
        Assert.Equal("キャンセル", c.No);
        Assert.True(c.Safe);
    }

    [Fact]
    public async Task Rename_OpensWithoutWarning_AndSavesFromWindow()
    {
        _dialogs.OnRename = vm =>
        {
            Assert.Equal("お母さん", vm.DisplayName);
            vm.DisplayName = "おかあさん";
            Assert.True(vm.TrySave());
        };

        await _vm.RenameAsync("a1");

        Assert.Equal(new[] { "rename" }, _dialogs.Calls);
        Assert.Empty(_dialog.Confirms);
        Assert.Equal("おかあさん", _repo.Get("a1")!.DisplayName);
        Assert.Equal("おかあさん", _vm.Accounts[0].DisplayName);
    }

    [Fact]
    public void RenameViewModel_EmptyName_ShowsError_AndKeepsOldName()
    {
        var vm = new RenameViewModel(_svc, "a1", "お母さん") { DisplayName = "  " };
        Assert.False(vm.TrySave());
        Assert.Equal(Strings.AC_23, vm.ErrorText);
        Assert.Equal("お母さん", _repo.Get("a1")!.DisplayName);
    }

    [Fact]
    public async Task Reenter_CancelOnWarning_DoesNotOpenWindow()
    {
        _dialog.WarnAnswer = false;
        await _vm.ReenterAsync("a1");
        Assert.Empty(_dialogs.Calls);
    }

    [Fact]
    public async Task Reenter_Continue_OpensWindowForThatAccount()
    {
        await _vm.ReenterAsync("a2");
        Assert.Equal(new[] { "reenter" }, _dialogs.Calls);
        Assert.Equal(new[] { "confirm:" + Strings.WD_06 }, _dialog.Titles);
    }

    [Fact]
    public async Task Delete_WithPendingOutbox_ShowsNotice_NoWarningNoConfirm()
    {
        _outbox.Pending["a1"] = 2;

        await _vm.DeleteAsync("a1");

        var n = Assert.Single(_dialog.Notices);
        Assert.Equal(Strings.WD_08, n.Title);
        Assert.Equal(Strings.AC_20, n.Message);
        Assert.Empty(_dialog.Confirms);
        Assert.NotNull(_repo.Get("a1"));
    }

    [Fact]
    public async Task Delete_CancelOnWarning_KeepsAccount_NoFinalConfirm()
    {
        _dialog.WarnAnswer = false;
        await _vm.DeleteAsync("a1");
        Assert.NotNull(_repo.Get("a1"));
        Assert.Equal(new[] { "confirm:" + Strings.WD_06 }, _dialog.Titles);
    }

    [Fact]
    public async Task Delete_CancelOnFinalConfirm_KeepsAccount()
    {
        _dialog.ConfirmAnswer = false;
        await _vm.DeleteAsync("a1");
        Assert.NotNull(_repo.Get("a1"));
        Assert.Equal(new[] { "confirm:" + Strings.WD_06, "confirm:" + Strings.WD_07 }, _dialog.Titles);
        var final = _dialog.Confirms[1];
        Assert.Equal("削除する", final.Yes);
        Assert.Equal("やめる", final.No);
        Assert.True(final.Safe);
        Assert.Equal(Strings.AC_27[..Strings.AC_27.IndexOf(" [", StringComparison.Ordinal)], final.Message);
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesAccountAndRefreshesList_OtherKept()
    {
        await _vm.DeleteAsync("a1");

        Assert.Null(_repo.Get("a1"));
        Assert.Null(_cred.Get("a1"));
        Assert.Equal(new[] { "a2" }, _vm.Accounts.Select(a => a.Id));
        Assert.True(_vm.AddCommand.CanExecute(null));
        Assert.Equal(string.Empty, _vm.AddNote);
        Assert.DoesNotContain("add-first", _dialogs.Calls);
    }

    [Fact]
    public async Task Delete_LastAccount_OpensAddWindowAsFirstLaunch_WithoutWarning()
    {
        _repo.Delete("a2");
        _vm.Reload();

        await _vm.DeleteAsync("a1");

        Assert.Empty(_repo.GetAll());
        Assert.Equal(new[] { "add-first" }, _dialogs.Calls);
        // 注意ダイアログは削除の前の 1 回だけ（追加の窓の前には出さない）
        Assert.Equal(1, _dialog.Confirms.Count(c => c.Title == Strings.WD_06));
    }

    [Fact]
    public async Task Add_WithExistingAccount_GoesThroughWarning()
    {
        _repo.Delete("a2");
        _vm.Reload();

        _dialog.WarnAnswer = false;
        await _vm.AddAsync();
        Assert.Empty(_dialogs.Calls);

        _dialog.WarnAnswer = true;
        await _vm.AddAsync();
        Assert.Equal(new[] { "add" }, _dialogs.Calls);
    }

    [Fact]
    public async Task Add_AtLimit_DoesNothing()
    {
        FillToLimit();
        await _vm.AddAsync();
        Assert.Empty(_dialogs.Calls);
        Assert.Empty(_dialog.Confirms);
    }

    [Fact]
    public async Task ReenterViewModel_Success_ClearsAuthFailed_AndRaisesSaved()
    {
        _sync.MarkAuthFailed("a1");
        var vm = new ReenterPasswordViewModel(_svc, _repo.Get("a1")!) { Password = "abcd efgh" };
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        Assert.True(await vm.SaveAsync());

        Assert.True(saved);
        Assert.Equal("abcdefgh", _cred.Get("a1"));
        Assert.DoesNotContain("a1", _sync.AuthFailed);
        Assert.Equal("お母さん (one@gmail.com)", vm.AccountText);
        Assert.False(vm.IsBusy);
    }

    [Theory]
    [InlineData(LoginResult.AuthFailed, "AC_06")]
    [InlineData(LoginResult.NetworkError, "AC_07")]
    public async Task ReenterViewModel_Failure_ShowsError_KeepsFlagAndOldPassword(LoginResult login, string id)
    {
        _sync.MarkAuthFailed("a1");
        _gateways.Create(_repo.Get("a1")!);
        _gateways.ByAccountId["a1"].LoginResult = login;
        var vm = new ReenterPasswordViewModel(_svc, _repo.Get("a1")!) { Password = "wrong" };

        Assert.False(await vm.SaveAsync());

        Assert.Equal(id == "AC_06" ? Strings.AC_06 : Strings.AC_07, vm.ErrorText);
        Assert.Equal("pw1", _cred.Get("a1"));
        Assert.Contains("a1", _sync.AuthFailed);
    }

    [Fact]
    public async Task ReenterViewModel_EmptyPassword_ShowsAc22()
    {
        var vm = new ReenterPasswordViewModel(_svc, _repo.Get("a1")!);
        Assert.False(await vm.SaveAsync());
        Assert.Equal(Strings.AC_22, vm.ErrorText);
    }
}
