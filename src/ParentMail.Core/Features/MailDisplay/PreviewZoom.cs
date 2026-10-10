namespace ParentMail.Core.Features.MailDisplay;

/// <summary>
/// プレビューの一時的な拡大率（mail-display PLAN-2）。範囲 100% から 300%、刻み 25%。
/// 浮動小数点の誤差を避けるため、内部は整数のパーセントで持つ。保存はしない。
/// </summary>
public sealed class PreviewZoom
{
    private const int MinPercent = (int)(Constants.PreviewZoomMin * 100);
    private const int MaxPercent = (int)(Constants.PreviewZoomMax * 100);
    private const int StepPercent = (int)(Constants.PreviewZoomStep * 100);

    public int Percent { get; private set; } = MinPercent;

    /// <summary>倍率（1.0 から 3.0）。</summary>
    public double Value => Percent / 100.0;

    public bool CanStepUp => Percent < MaxPercent;
    public bool CanStepDown => Percent > MinPercent;

    /// <summary>1 段階拡大する。変わったとき true。</summary>
    public bool StepUp()
    {
        if (!CanStepUp) return false;
        Percent = Math.Min(MaxPercent, Percent + StepPercent);
        return true;
    }

    /// <summary>1 段階縮小する。変わったとき true。</summary>
    public bool StepDown()
    {
        if (!CanStepDown) return false;
        Percent = Math.Max(MinPercent, Percent - StepPercent);
        return true;
    }

    /// <summary>100% に戻す。変わったとき true。</summary>
    public bool Reset()
    {
        if (Percent == MinPercent) return false;
        Percent = MinPercent;
        return true;
    }
}
