using ParentMail.App.Features.SyncCache;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.SyncCache;

public class SyncUiBinderTests
{
    private static (SyncUiBinder Binder, SyncHarness H, FakeNotifier N, FakeAppState S) Make()
    {
        var h = new SyncHarness();
        var n = new FakeNotifier();
        var s = new FakeAppState { CurrentAccountId = h.AccountId };
        var coord = new SyncCoordinator(h.Sync, h.Accounts, h.Log);
        var binder = new SyncUiBinder(h.Sync, coord, h.Store, n, s, a => a());
        binder.Attach();
        return (binder, h, n, s);
    }

    [Fact]
    public async Task Offline_ShowsOfflineBanner_ThenClearsOnSuccess()
    {
        var (_, h, n, _) = Make();
        using var _h = h;
        h.Gateway.Offline = true;
        await h.RunAsync();
        Assert.Equal(Strings.BN_01, n.Banners[BannerKind.Offline].Text);

        h.Gateway.Offline = false;
        await h.RunAsync();
        Assert.False(n.Banners.ContainsKey(BannerKind.Offline));
    }

    [Fact]
    public async Task InitialSync_ShowsLoadingBannerWhileSyncing_NotAfterwards()
    {
        var (_, h, n, _) = Make();
        using var _h = h;
        var shown = false;
        h.Sync.StateChanged += (_, _) => shown |= n.Banners.TryGetValue(BannerKind.InitialSync, out var b) && b.Text == Strings.BN_02;
        h.Put(SyncHarness.Inbox, 1, 101);

        await h.RunAsync();

        Assert.True(shown);
        Assert.False(n.Banners.ContainsKey(BannerKind.InitialSync));
        // 2 回目以降の同期（初回同期は済み）では出さない
        shown = false;
        await h.RunAsync();
        Assert.False(shown);
    }

    [Fact]
    public async Task OtherFailure_ShowsReceiveFailedBanner_ButAuthFailureDoesNot()
    {
        var inner = new FakeImapGateway();
        using var h = new SyncHarness(new DelegateGatewayFactory(_ => new ThrowingGateway(inner)));
        inner.Folders.AddRange(h.Gateway.Folders);
        var n = new FakeNotifier();
        var s = new FakeAppState { CurrentAccountId = h.AccountId };
        var binder = new SyncUiBinder(h.Sync, new SyncCoordinator(h.Sync, h.Accounts, h.Log), h.Store, n, s, a => a());
        binder.Attach();

        await h.RunAsync();
        Assert.Equal(Strings.BN_19, n.Banners[BannerKind.Other].Text);

        h.Sync.MarkAuthFailed(h.AccountId);       // パスワード不正は赤い帯（accounts-auth）の担当で、ここでは出さない
        Assert.False(n.Banners.ContainsKey(BannerKind.Other));
    }

    [Fact]
    public async Task BannerFollowsDisplayedAccount()
    {
        var (_, h, n, s) = Make();
        using var _h = h;
        h.Gateway.Offline = true;
        await h.RunAsync();
        Assert.True(n.Banners.ContainsKey(BannerKind.Offline));

        s.CurrentAccountId = "another-account";     // 表示アカウントを切り替えると、そちらの状態に合わせて消える
        Assert.False(n.Banners.ContainsKey(BannerKind.Offline));
    }

    [Fact]
    public void RequestRefresh_TriggersManualSyncOfDisplayedAccount()
    {
        var (binder, h, _, _) = Make();
        using var _h = h;
        h.Put(SyncHarness.Inbox, 1, 101);

        binder.RequestRefresh();
        SpinWait.SpinUntil(() => h.Gateway.Calls.Contains("TestLogin"), 3000);

        Assert.Contains("TestLogin", h.Gateway.Calls);
    }
}
