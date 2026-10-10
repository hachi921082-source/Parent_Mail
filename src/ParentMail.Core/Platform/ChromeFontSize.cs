namespace ParentMail.Core.Platform;

/// <summary>上部バーと一覧下部の帯の文字サイズ（pt を上限 20 pt で頭打ちにして DIP へ換算）。</summary>
public static class ChromeFontSize
{
    public static double Get(double textPt) => Math.Min(textPt, Constants.ChromeTextMaxPt) * Constants.PointToDip;
}
