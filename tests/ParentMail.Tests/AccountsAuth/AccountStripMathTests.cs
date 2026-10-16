using ParentMail.App.Features.AccountsAuth;

namespace ParentMail.Tests.AccountsAuth;

// アカウントボタン帯のスクロール計算（隠れた未読の合計、矢印の移動先、表示位置の合わせ方）のテスト。
public sealed class AccountStripMathTests
{
    // 幅 220 のボタンを 16 の間隔で並べる。未読は引数のとおり
    private static StripButtonExtent[] Buttons(params int[] unread)
        => unread.Select((u, i) => new StripButtonExtent(i * 236, 220, u)).ToArray();

    [Fact]
    public void HiddenUnread_AllVisible_IsZero()
    {
        var b = Buttons(1, 2);
        Assert.Equal((0, 0), AccountStripMath.HiddenUnread(b, 0, 500));
    }

    [Fact]
    public void HiddenUnread_SumsOnlyHiddenOnTheSide()
    {
        // 5 件、見える幅 500: 0 番と 1 番が見え、2〜4 番が右に隠れる
        var b = Buttons(0, 4, 3, 5, 7);
        Assert.Equal((0, 15), AccountStripMath.HiddenUnread(b, 0, 500));
    }

    [Fact]
    public void HiddenUnread_ScrolledToEnd_HiddenOnLeft()
    {
        var b = Buttons(2, 4, 0, 0, 0); // 内容幅 1100
        Assert.Equal((6, 0), AccountStripMath.HiddenUnread(b, 600, 500));
    }

    [Fact]
    public void HiddenUnread_PartlyClippedCountsAsHidden()
    {
        var b = Buttons(0, 0, 9, 0, 0); // 2 番は 472..692。見える幅 600 では 92 欠ける
        Assert.Equal((0, 9), AccountStripMath.HiddenUnread(b, 0, 600));
    }

    [Fact]
    public void HiddenUnread_ZeroUnread_MeansNoBadge()
    {
        Assert.Equal((0, 0), AccountStripMath.HiddenUnread(Buttons(0, 0, 0, 0, 0), 0, 300));
    }

    [Fact]
    public void HiddenUnread_BothSides()
    {
        var b = Buttons(1, 2, 0, 4, 8); // 見える 472..692 は 2 番だけ
        Assert.Equal((3, 12), AccountStripMath.HiddenUnread(b, 472, 220));
    }

    [Fact]
    public void OffsetToReveal_AlreadyVisible_KeepsOffset()
    {
        var b = Buttons(0, 0, 0)[1];
        Assert.Equal(100, AccountStripMath.OffsetToReveal(b, 100, 500, 700));
    }

    [Fact]
    public void OffsetToReveal_ClippedRight_ScrollsRightJustEnough()
    {
        var b = Buttons(0, 0, 0, 0, 0)[3]; // 708..928
        Assert.Equal(928 - 500, AccountStripMath.OffsetToReveal(b, 0, 500, 1180));
    }

    [Fact]
    public void OffsetToReveal_ClippedLeft_AlignsLeftEdge()
    {
        var b = Buttons(0, 0, 0, 0, 0)[1]; // 236..456
        Assert.Equal(236, AccountStripMath.OffsetToReveal(b, 400, 500, 1180));
    }

    [Fact]
    public void OffsetToReveal_WiderThanViewport_AlignsLeftAndClamps()
    {
        var b = new StripButtonExtent(0, 300, 0);
        Assert.Equal(0, AccountStripMath.OffsetToReveal(b, 50, 200, 300));
        // 余白つきでも先頭・末尾を越えない
        var last = new StripButtonExtent(236, 220, 0);
        Assert.Equal(0, AccountStripMath.OffsetToReveal(last, 0, 500, 456, margin: 4));
    }

    [Fact]
    public void OffsetToReveal_WithMargin_KeepsFocusRectangleInside()
    {
        var b = Buttons(0, 0, 0, 0, 0)[3]; // 708..928
        Assert.Equal(928 + 4 - 500, AccountStripMath.OffsetToReveal(b, 0, 500, 1180, margin: 4));
    }

    [Fact]
    public void OffsetForArrow_Right_ShowsNextHiddenButtonCompletely()
    {
        var b = Buttons(0, 0, 0, 0, 0); // 見える幅 500、内容幅 1180
        Assert.Equal(692 - 500, AccountStripMath.OffsetForArrow(b, +1, 0, 500, 1180)); // 2 番（472..692）
        Assert.Equal(928 - 500, AccountStripMath.OffsetForArrow(b, +1, 192, 500, 1180)); // 次は 3 番
    }

    [Fact]
    public void OffsetForArrow_Right_AtEnd_StaysPut()
    {
        var b = Buttons(0, 0, 0, 0, 0);
        Assert.Equal(680, AccountStripMath.OffsetForArrow(b, +1, 680, 500, 1180));
    }

    [Fact]
    public void OffsetForArrow_Left_ShowsPreviousHiddenButtonCompletely()
    {
        var b = Buttons(0, 0, 0, 0, 0);
        Assert.Equal(472, AccountStripMath.OffsetForArrow(b, -1, 680, 500, 1180)); // 680 では 2 番が欠けている
        Assert.Equal(236, AccountStripMath.OffsetForArrow(b, -1, 472, 500, 1180));
        Assert.Equal(0, AccountStripMath.OffsetForArrow(b, -1, 236, 500, 1180));
    }

    [Fact]
    public void OffsetForArrow_Left_AtStart_StaysPut()
    {
        Assert.Equal(0, AccountStripMath.OffsetForArrow(Buttons(0, 0, 0), -1, 0, 500, 700));
    }

    [Fact]
    public void CanScroll_OnlyTowardSideWithPartlyOrFullyHiddenButtons()
    {
        var b = Buttons(0, 0, 0, 0, 0); // 最後のボタンの右端は 1164
        Assert.False(AccountStripMath.CanScroll(b, -1, 0, 500));
        Assert.True(AccountStripMath.CanScroll(b, +1, 0, 500));
        Assert.True(AccountStripMath.CanScroll(b, -1, 300, 500));
        Assert.True(AccountStripMath.CanScroll(b, +1, 300, 500));
        // 最後のボタンが全部見えれば、右はこれ以上動かせない（末尾の間隔だけ残っていても）
        Assert.False(AccountStripMath.CanScroll(b, +1, 1164 - 500, 500));
        Assert.True(AccountStripMath.CanScroll(b, -1, 1164 - 500, 500));
    }
}
