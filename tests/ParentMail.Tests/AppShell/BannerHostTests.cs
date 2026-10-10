using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.Tests.AppShell;

public class BannerHostTests
{
    private static BannerKind[] Kinds(BannerHost h) => h.Visible.Select(b => b.Kind).ToArray();

    [Fact]
    public void 優先順位は宣言順で最優先から並ぶ()
    {
        Assert.True(BannerKind.WrongPassword < BannerKind.Offline);
        Assert.True(BannerKind.Offline < BannerKind.OutboxFailure);
        Assert.True(BannerKind.OutboxFailure < BannerKind.InitialSync);
        Assert.True(BannerKind.InitialSync < BannerKind.Other);
    }

    [Fact]
    public void 三件のうち優先順位の高い二件だけ表示する()
    {
        var h = new BannerHost();
        h.Show(BannerKind.InitialSync, "sync");
        h.Show(BannerKind.OutboxFailure, "outbox");
        h.Show(BannerKind.Offline, "offline");

        Assert.Equal(new[] { BannerKind.Offline, BannerKind.OutboxFailure }, Kinds(h));
    }

    [Fact]
    public void 上位の帯が消えると下位の帯が現れる()
    {
        var h = new BannerHost();
        h.Show(BannerKind.InitialSync, "sync");
        h.Show(BannerKind.OutboxFailure, "outbox");
        h.Show(BannerKind.Offline, "offline");

        h.Hide(BannerKind.Offline);

        Assert.Equal(new[] { BannerKind.OutboxFailure, BannerKind.InitialSync }, Kinds(h));
    }

    [Fact]
    public void 後から優先度の高い帯が来ると低い帯が押し出される()
    {
        var h = new BannerHost();
        h.Show(BannerKind.Other, "other");
        h.Show(BannerKind.InitialSync, "sync");
        h.Show(BannerKind.WrongPassword, "pw");

        Assert.Equal(new[] { BannerKind.WrongPassword, BannerKind.InitialSync }, Kinds(h));
    }

    [Fact]
    public void 閉じられるのは送信失敗の帯だけ()
    {
        var h = new BannerHost();
        foreach (var kind in Enum.GetValues<BannerKind>()) h.Show(kind, kind.ToString());

        foreach (var kind in Enum.GetValues<BannerKind>().Where(k => k != BannerKind.OutboxFailure))
        {
            Assert.False(h.Dismiss(kind));
        }
        Assert.True(h.Dismiss(BannerKind.OutboxFailure));
        Assert.All(h.Visible, b => Assert.False(b.CanClose));
    }

    [Fact]
    public void 閉じた送信失敗の帯は同じ失敗では戻らず条件が終わると次回は出る()
    {
        var h = new BannerHost();
        h.Show(BannerKind.OutboxFailure, "outbox");
        h.Dismiss(BannerKind.OutboxFailure);

        h.Show(BannerKind.OutboxFailure, "outbox");   // 失敗が続いている間の再通知
        Assert.Empty(h.Visible);

        h.Hide(BannerKind.OutboxFailure);              // 失敗が解消
        h.Show(BannerKind.OutboxFailure, "outbox");    // 別の失敗
        Assert.Equal(new[] { BannerKind.OutboxFailure }, Kinds(h));
    }

    [Fact]
    public void 閉じた帯は表示枠を使わず下位の帯が出る()
    {
        var h = new BannerHost();
        h.Show(BannerKind.Offline, "offline");
        h.Show(BannerKind.OutboxFailure, "outbox");
        h.Show(BannerKind.InitialSync, "sync");

        h.Dismiss(BannerKind.OutboxFailure);

        Assert.Equal(new[] { BannerKind.Offline, BannerKind.InitialSync }, Kinds(h));
    }
}
