namespace ParentMail.Core.Features.Compose;

/// <summary>
/// 送信時の大きさの見積り（compose PLAN-1 3 章、PQ11/A42/A69）。
/// 添付は base64（3 バイト→4 文字、76 文字ごとに改行）で膨らむ前提で、ヘッダの余裕を足して判定する。
/// </summary>
public static class EncodedSizeEstimator
{
    /// <summary>実ファイルの大きさから送信後の大きさへの倍率（Gmail の上限は符号化後で数える）。</summary>
    private const double EncodedPerRaw = 1.37;

    public static long Estimate(IEnumerable<long> fileSizes, long bodyBytes)
    {
        long total = Constants.HeaderOverheadBytes + bodyBytes;
        foreach (var n in fileSizes)
        {
            var base64 = (long)Math.Ceiling(n / 3.0) * 4;
            total += (long)Math.Ceiling(base64 * 78.0 / 76.0);
        }
        return total;
    }

    public static bool IsAllowed(long estimate) => estimate <= Constants.MaxEncodedMessageBytes;

    /// <summary>減らす必要のある量（元のファイルの MB、0.1 単位で切り上げ）。上限内なら 0。</summary>
    public static double OverRawMegabytes(long estimate)
    {
        var over = estimate - Constants.MaxEncodedMessageBytes;
        if (over <= 0) return 0.0;
        return Math.Ceiling(over / EncodedPerRaw / 100_000.0) / 10.0;
    }

    /// <summary>「あと約X.XMB減らしてください」（CP-08）。</summary>
    public static string OverText(long estimate, string template)
        => template.Replace("2.3", OverRawMegabytes(estimate).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
}
