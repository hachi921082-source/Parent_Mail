using ParentMail.Core;
using ParentMail.Core.Models;
using ParentMail.Core.Platform;

namespace ParentMail.App.Services;

// settings レーンが ISettingsStore / ITextScale を実装するまでの仮実装。
// TryAdd で登録するので、settings 側の登録が後に来れば置き換わる。契約（Core の Abstractions）にだけ従う。

internal sealed class InMemorySettingsStore : ISettingsStore
{
    private AppSettings _settings = new();
    public AppSettings Get() => _settings;
    public void Save(AppSettings settings) => _settings = settings;
}

internal sealed class DefaultTextScale : ITextScale
{
    private double _pt = Constants.DefaultTextSizePt;

    public double TextSizePt
    {
        get => _pt;
        set
        {
            _pt = Math.Clamp(value, Constants.MinTextSizePt, Constants.MaxTextSizePt);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public double FontSizeDip => _pt * Constants.PointToDip;
    public double ChromeFontSizeDip => ChromeFontSize.Get(_pt);

    public double GetEffectivePreviewPt(double zoom) => Math.Min(_pt * zoom, Constants.PreviewTextMaxPt);
    public double GetWebViewZoom(double zoom)
        => Math.Round(Math.Clamp(GetEffectivePreviewPt(zoom) / Constants.ZoomReferencePt,
            Constants.WebViewZoomMin, Constants.WebViewZoomMax), 2);
    public double GetPlainTextSizeDip(double zoom) => GetEffectivePreviewPt(zoom) * Constants.PointToDip;

    public event EventHandler? Changed;
}
