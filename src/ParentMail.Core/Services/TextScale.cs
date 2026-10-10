namespace ParentMail.Core.Services;

// 文字サイズ（pt）から各画面の大きさへの換算（settings PLAN 3, 4 章）。
// 倍率（zoom）はどこにも保存しない。
public sealed class TextScale : ITextScale
{
    private readonly ISettingsStore _store;
    private double _pt;

    public TextScale(ISettingsStore store)
    {
        _store = store;
        _pt = Clamp(store.Get().TextSizePt);
    }

    public event EventHandler? Changed;

    public double TextSizePt
    {
        get => _pt;
        set
        {
            var v = Clamp(value);
            if (v == _pt)
            {
                return;
            }
            _pt = v;
            // ウィンドウ位置など他の設定を壊さないよう、現在値を読んで文字サイズだけ差し替える
            _store.Save(_store.Get() with { TextSizePt = v });
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public double FontSizeDip => _pt * Constants.PointToDip;

    public double ChromeFontSizeDip => Math.Min(_pt, Constants.ChromeTextMaxPt) * Constants.PointToDip;

    public double GetEffectivePreviewPt(double zoom) => Math.Min(_pt * zoom, Constants.PreviewTextMaxPt);

    public double GetWebViewZoom(double zoom) =>
        Math.Round(
            Math.Clamp(GetEffectivePreviewPt(zoom) / Constants.ZoomReferencePt,
                Constants.WebViewZoomMin, Constants.WebViewZoomMax),
            2, MidpointRounding.AwayFromZero);

    public double GetPlainTextSizeDip(double zoom) => GetEffectivePreviewPt(zoom) * Constants.PointToDip;

    private static double Clamp(double pt) =>
        double.IsNaN(pt) ? Constants.DefaultTextSizePt : Math.Clamp(pt, Constants.MinTextSizePt, Constants.MaxTextSizePt);
}
