using ParentMail.App.Features.AccountsAuth;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AccountsAuth;

// パスワード不正への対応（赤い帯 + 入れ直す、自動で一度だけ窓を開く、2 つ失敗しても重ねない）のテスト。
public sealed class AuthFailureCoordinatorTests
{
    private sealed class SetProbe(FakeSyncService sync) : IAuthFailureProbe
    {
        public bool IsAuthFailed(string accountId) => sync.AuthFailed.Contains(accountId);
    }

    private sealed class RecordingDialogs : IAccountDialogs
    {
        public List<string> Opened { get; } = new();
        public int Depth { get; private set; }
        public int MaxDepth { get; private set; }
        public Action<ReenterPasswordViewModel>? During { get; set; }

        public bool ShowRename(RenameViewModel vm) => false;
        public bool ShowAdd(bool firstLaunch) => false;

        public bool ShowReenter(ReenterPasswordViewModel vm)
        {
            Opened.Add(vm.AccountId);
            Depth++;
            MaxDepth = Math.Max(MaxDepth, Depth);
            try { During?.Invoke(vm); } finally { Depth--; }
            return true;
        }
    }

    private readonly InMemoryAccountRepository _repo = new();
    private readonly FakeCredentialStore _cred = new();
    private readonly FakeSyncService _sync = new();
    private readonly FakeNotifier _notifier = new();
    private readonly RecordingDialogs _dialogs = new();
    private readonly FakeImapGatewayFactory _gateways = new();
    private readonly AuthFailureCoordinator _coord;

    public AuthFailureCoordinatorTests()
    {
        _repo.Add(new Account("a1", "one@gmail.com", "お母さん", 0));
        _repo.Add(new Account("a2", "two@gmail.com", "お父さん", 1));
        _cred.Save("a1", "pw1");
        _cred.Save("a2", "pw2");
        var svc = new AccountService(_repo, _cred, _gateways, _sync);
        // 窓を開く処理は同じスレッドでそのまま実行する
        _coord = new AuthFailureCoordinator(_sync, new SetProbe(_sync), _repo, svc, _notifier, _dialogs, a => a());
        _coord.Attach();
    }

    private void Fail(string id)
    {
        _sync.MarkAuthFailed(id);
        _sync.RaiseStateChanged(id, SyncState.Error);
    }

    [Fact]
    public void NoFailure_NoBanner_NoWindow()
    {
        Assert.Empty(_notifier.Banners);
        Assert.Empty(_dialogs.Opened);
    }

    [Fact]
    public void AuthFailure_ShowsRedBannerWithReenterAction_AndOpensWindowOnce()
    {
        Fail("a1");

        var banner = _notifier.Banners[BannerKind.WrongPassword];
        Assert.Equal(Strings.AC_16, banner.Text);
        Assert.Equal(Strings.AC_17, banner.Action!.Value.Label);
        Assert.Equal(new[] { "a1" }, _dialogs.Opened);

        // 同じアカウントが再び失敗しても、自動では二度と開かない
        _sync.RaiseStateChanged("a1", SyncState.Error);
        Assert.Equal(new[] { "a1" }, _dialogs.Opened);
    }

    [Fact]
    public void BannerButton_ReopensWindow_WithoutWarningDialog()
    {
        Fail("a1");
        _dialogs.Opened.Clear();

        _notifier.Banners[BannerKind.WrongPassword].Action!.Value.Action();

        Assert.Equal(new[] { "a1" }, _dialogs.Opened);
    }

    [Fact]
    public void Success_ClearsFlag_AndHidesBanner()
    {
        _dialogs.During = vm =>
        {
            vm.Password = "good-pass";
            vm.SaveAsync().GetAwaiter().GetResult();
        };

        Fail("a1");

        Assert.DoesNotContain("a1", _sync.AuthFailed);
        Assert.Contains(("a1", SyncTrigger.Reconnect), _sync.Requests);
        // ClearAuthFailed の通知を受けて帯が消える（フェイクは通知しないので明示的に）
        _sync.RaiseStateChanged("a1", SyncState.Idle);
        Assert.False(_notifier.Banners.ContainsKey(BannerKind.WrongPassword));
    }

    [Fact]
    public void BothFail_WindowsOpenOneAfterAnother_NeverStacked()
    {
        // 1 つ目の窓が開いている間に 2 つ目が失敗しても、重ねずに 1 つ目を閉じてから開く
        var raised = false;
        _dialogs.During = _ =>
        {
            if (raised) return;
            raised = true;
            Fail("a2");
        };

        Fail("a1");

        Assert.Equal(new[] { "a1", "a2" }, _dialogs.Opened);
        Assert.Equal(1, _dialogs.MaxDepth);
    }

    [Fact]
    public void OtherAccountStillFailed_BannerStaysAfterOneIsFixed()
    {
        Fail("a1");
        Fail("a2");
        _sync.ClearAuthFailed("a1");
        _sync.RaiseStateChanged("a1", SyncState.Idle);

        Assert.True(_notifier.Banners.ContainsKey(BannerKind.WrongPassword));
        _dialogs.Opened.Clear();
        _notifier.Banners[BannerKind.WrongPassword].Action!.Value.Action();
        Assert.Equal(new[] { "a2" }, _dialogs.Opened);
    }
}
