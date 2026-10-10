using ParentMail.Core.Models;

namespace ParentMail.Core;

// アプリ全体の状態・通知・ダイアログ・文字サイズの契約。

/// <summary>現在のアカウント・フォルダ・選択メール。変更時にイベントを出す。</summary>
public interface IAppState
{
    string? CurrentAccountId { get; set; }
    FolderKind CurrentFolder { get; set; }
    long? SelectedMessageId { get; set; }
    event EventHandler? CurrentAccountChanged;
    event EventHandler? CurrentFolderChanged;
    event EventHandler? SelectedMessageChanged;
}

/// <summary>帯の優先度。値が大きいほど優先して表示する。</summary>
public enum BannerPriority { Low = 0, Normal = 1, High = 2 }

public interface INotifier
{
    /// <summary>帯を表示する。同じ key は置き換える。</summary>
    void ShowBanner(string key, string text, BannerPriority priority);
    void HideBanner(string key);
    /// <summary>数秒で消える通知。action があれば操作ボタン付き（元に戻す等）。</summary>
    void ShowTransient(string text, string? actionText = null, Action? action = null);
}

public interface IDialogService
{
    /// <summary>「OK」ボタン 1 つの通知。</summary>
    void ShowNotice(string title, string message);
    /// <summary>確認。safeIsDefault が true のとき既定ボタンは安全側（キャンセル）。</summary>
    bool Confirm(string title, string message, string yesText, string noText, bool safeIsDefault = true);
    /// <summary>複数の選択肢から選ぶ。戻り値は選択位置、キャンセルは -1。</summary>
    int Choose(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int safeIndex);
}

/// <summary>文字サイズ（pt）から画面上の大きさ（DIP）への変換。</summary>
public interface ITextScale
{
    double TextSizePt { get; set; }
    /// <summary>通常 UI の FontSize (DIP)。</summary>
    double BodyDip { get; }
    /// <summary>上部バー・下部帯の FontSize (DIP)。ChromeTextMaxPt で頭打ち。</summary>
    double ChromeDip { get; }
    /// <summary>プレビューの実効 pt = min(pt x zoom, PreviewTextMaxPt)。</summary>
    double EffectivePreviewPt(double zoom);
    double PreviewDip(double zoom);
    /// <summary>HTML 表示用 ZoomFactor。範囲に収め小数 2 桁に丸める。</summary>
    double WebViewZoomFactor(double zoom);
    event EventHandler? Changed;
}
