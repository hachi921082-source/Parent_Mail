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

/// <summary>帯の種類。宣言順が優先順（先頭が最優先）。</summary>
public enum BannerKind { WrongPassword, Offline, OutboxFailure, InitialSync, Other }

public interface INotifier
{
    /// <summary>帯を表示する。同じ kind は置き換える。action があれば操作ボタン付き。</summary>
    void ShowBanner(BannerKind kind, string text, (string Label, Action Action)? action = null);
    void HideBanner(BannerKind kind);
    /// <summary>数秒で消える通知（transient）。action があれば操作ボタン付き（元に戻す等）。</summary>
    void ShowNotice(string text, string? actionLabel = null, Action? action = null, int seconds = Constants.TransientNoticeSeconds);
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

/// <summary>文字サイズ（pt）から画面上の大きさ（DIP）への変換（settings PLAN 4 章）。</summary>
public interface ITextScale
{
    double TextSizePt { get; set; }
    /// <summary>通常 UI の FontSize (DIP) = pt x 96/72。</summary>
    double FontSizeDip { get; }
    /// <summary>上部バー・下部帯の FontSize (DIP)。ChromeTextMaxPt で頭打ち。</summary>
    double ChromeFontSizeDip { get; }
    /// <summary>プレビューの実効 pt = min(pt x zoom, PreviewTextMaxPt)。</summary>
    double GetEffectivePreviewPt(double zoom);
    /// <summary>HTML 表示用 ZoomFactor。範囲に収め小数 2 桁に丸める。</summary>
    double GetWebViewZoom(double zoom);
    /// <summary>プレーンテキスト表示の FontSize (DIP)。</summary>
    double GetPlainTextSizeDip(double zoom);
    event EventHandler? Changed;
}
