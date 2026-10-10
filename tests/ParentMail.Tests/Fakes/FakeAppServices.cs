using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.Tests.Fakes;

// アプリ共通サービス（状態・通知・ダイアログ・文字サイズ）の手書きフェイク。

public sealed class FakeAppState : IAppState
{
    private string? _account;
    private FolderKind _folder = FolderKind.Inbox;
    private long? _selected;

    public string? CurrentAccountId
    {
        get => _account;
        set { if (_account != value) { _account = value; CurrentAccountChanged?.Invoke(this, EventArgs.Empty); } }
    }

    public FolderKind CurrentFolder
    {
        get => _folder;
        set { if (_folder != value) { _folder = value; CurrentFolderChanged?.Invoke(this, EventArgs.Empty); } }
    }

    public long? SelectedMessageId
    {
        get => _selected;
        set { if (_selected != value) { _selected = value; SelectedMessageChanged?.Invoke(this, EventArgs.Empty); } }
    }

    public event EventHandler? CurrentAccountChanged;
    public event EventHandler? CurrentFolderChanged;
    public event EventHandler? SelectedMessageChanged;
}

public sealed class FakeNotifier : INotifier
{
    public Dictionary<BannerKind, (string Text, (string Label, Action Action)? Action)> Banners { get; } = new();
    public List<string> Notices { get; } = new();

    public void ShowBanner(BannerKind kind, string text, (string Label, Action Action)? action = null) => Banners[kind] = (text, action);
    public void HideBanner(BannerKind kind) => Banners.Remove(kind);
    public void ShowNotice(string text, string? actionLabel = null, Action? action = null, int seconds = Constants.TransientNoticeSeconds)
        => Notices.Add(text);
}

public sealed class FakeDialogService : IDialogService
{
    /// <summary>ShowNotice の呼び出し記録（題名と本文）。</summary>
    public List<(string Title, string Message)> Notices { get; } = new();
    public bool ConfirmAnswer { get; set; }
    public int ChooseAnswer { get; set; } = -1;

    public void ShowNotice(string title, string message) => Notices.Add((title, message));
    public bool Confirm(string title, string message, string yesText, string noText, bool safeIsDefault = true) => ConfirmAnswer;
    public int Choose(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int safeIndex) => ChooseAnswer;
}

public sealed class FakeTextScale : ITextScale
{
    public double TextSizePt { get; set; } = Constants.DefaultTextSizePt;
    public double FontSizeDip => TextSizePt * Constants.PointToDip;
    public double ChromeFontSizeDip => Math.Min(TextSizePt, Constants.ChromeTextMaxPt) * Constants.PointToDip;
    public double GetEffectivePreviewPt(double zoom) => Math.Min(TextSizePt * zoom, Constants.PreviewTextMaxPt);
    public double GetPlainTextSizeDip(double zoom) => GetEffectivePreviewPt(zoom) * Constants.PointToDip;

    public double GetWebViewZoom(double zoom)
    {
        var f = Math.Round(GetEffectivePreviewPt(zoom) / Constants.ZoomReferencePt, 2);
        return Math.Clamp(f, Constants.WebViewZoomMin, Constants.WebViewZoomMax);
    }

    public event EventHandler? Changed;
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
