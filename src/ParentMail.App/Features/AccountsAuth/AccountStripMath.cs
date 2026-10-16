namespace ParentMail.App.Features.AccountsAuth;

/// <summary>アカウントボタン帯でのボタン 1 つ分の位置（帯の内容座標。Left は先頭からの距離）と未読数。</summary>
public readonly record struct StripButtonExtent(double Left, double Width, int Unread)
{
    public double Right => Left + Width;
}

/// <summary>
/// アカウントボタン帯のスクロール計算（画面部品に依存しない純関数。Issue #8、A82・A83・A85）。
/// offset は現在の横スクロール位置、viewport は見えている幅。
/// </summary>
public static class AccountStripMath
{
    /// <summary>端の判定で許す誤差（DIP）。</summary>
    public const double Epsilon = 0.5;

    /// <summary>ボタンが全部見えている（はみ出しも欠けもない）か。</summary>
    public static bool IsFullyVisible(StripButtonExtent b, double offset, double viewport)
        => b.Left >= offset - Epsilon && b.Right <= offset + viewport + Epsilon;

    /// <summary>
    /// 左右の矢印に出す未読数。全部は見えていないボタン（欠けているものも含む）の未読を、
    /// 左側・右側ごとに合計する。0 ならバッジなし。
    /// </summary>
    public static (int Left, int Right) HiddenUnread(IReadOnlyList<StripButtonExtent> buttons, double offset, double viewport)
    {
        int left = 0, right = 0;
        foreach (var b in buttons)
        {
            if (IsFullyVisible(b, offset, viewport)) continue;
            // 中心が見える範囲より左なら左側、そうでなければ右側とみなす
            if (b.Left + b.Width / 2 < offset + viewport / 2) left += b.Unread;
            else right += b.Unread;
        }
        return (left, right);
    }

    /// <summary>
    /// ボタンが全部見えるようにするスクロール位置。すでに見えていれば現在位置のまま。
    /// ボタンが帯より広いときは左端を合わせる。maxOffset = 内容幅 - 見える幅（0 以上）。
    /// </summary>
    public static double OffsetToReveal(StripButtonExtent b, double offset, double viewport, double extent, double margin = 0)
    {
        var maxOffset = Math.Max(0, extent - viewport);
        double target;
        if (b.Width + 2 * margin >= viewport) target = b.Left - margin;
        else if (b.Left - margin < offset) target = b.Left - margin;
        else if (b.Right + margin > offset + viewport) target = b.Right + margin - viewport;
        else target = offset;
        return Math.Clamp(target, 0, maxOffset);
    }

    /// <summary>
    /// 矢印 1 回分のスクロール先。direction = -1 で左、+1 で右。
    /// その側で全部は見えていない最初のボタンが、全部見える位置まで動く。無ければ現在位置のまま。
    /// </summary>
    public static double OffsetForArrow(IReadOnlyList<StripButtonExtent> buttons, int direction, double offset, double viewport, double extent)
    {
        if (direction < 0)
        {
            for (var i = buttons.Count - 1; i >= 0; i--)
            {
                if (buttons[i].Left < offset - Epsilon) return OffsetToReveal(buttons[i], offset, viewport, extent);
            }
        }
        else
        {
            foreach (var b in buttons)
            {
                if (b.Right > offset + viewport + Epsilon) return OffsetToReveal(b, offset, viewport, extent);
            }
        }
        return offset;
    }
}
