namespace ParentMail.App.Services;

/// <summary>
/// 確認ダイアログのボタン配置の判断（単体テストできる純粋関数）。
/// ボタンの自然幅の合計が利用可能幅を超えるときは縦積みにして、文字が切れないようにする。
/// </summary>
public static class DialogButtonLayout
{
    /// <summary>窓の最大幅 = 作業領域の 90%。</summary>
    public static double MaxWindowWidth(double workAreaWidth) => workAreaWidth * 0.9;

    /// <summary>ボタン行に使える幅 = 窓の最大幅 − 左右の余白。</summary>
    public static double AvailableWidth(double maxWindowWidth, double margin)
        => Math.Max(0, maxWindowWidth - margin * 2);

    /// <summary>横並びで収まらないとき true（縦積みにする）。</summary>
    public static bool ShouldStack(IReadOnlyList<double> naturalWidths, double gap, double availableWidth)
    {
        if (naturalWidths.Count == 0) return false;
        var total = naturalWidths.Sum() + gap * (naturalWidths.Count - 1);
        return total > availableWidth;
    }
}
