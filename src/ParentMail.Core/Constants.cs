namespace ParentMail.Core;

// 共有定数（docs/plans/README.md 6 章）。マジックナンバーを各機能に散らさない。
public static class Constants
{
    public const int SyncIntervalMinutes = 5;
    public const int DraftAutoSaveSeconds = 30;
    public const int UndoNoticeSeconds = 10;
    public const int InitialBodyMonths = 12;

    public const double DefaultTextSizePt = 18;
    public const double MinTextSizePt = 12;
    public const double MaxTextSizePt = 48;

    public const double PreviewZoomMin = 1.00;
    public const double PreviewZoomMax = 3.00;
    public const double PreviewZoomStep = 0.25;
    public const double ZoomReferencePt = 12;
    public const double WebViewZoomMin = 0.25;
    public const double WebViewZoomMax = 5.0;

    public const int SendFailureNoticeThreshold = 3;
    public const long MaxEncodedMessageBytes = 25_000_000;
    public const long HeaderOverheadBytes = 10_240;

    public const double MinWindowWidth = 1024;
    public const double MinWindowHeight = 700;
    public const int MaxAccounts = 5;

    // アカウントボタン帯（Issue #8、A82・A86）
    public const double AccountButtonMaxWidthDip = 300;      // 1〜2 件のとき
    public const double AccountButtonMaxWidthManyDip = 220;  // 3〜5 件のとき
    public const double AccountButtonMinWidthDip = 120;
    public const double AccountScrollArrowDip = 56;

    /// <summary>pt から DIP への換算係数。</summary>
    public const double PointToDip = 96.0 / 72.0;

    /// <summary>上部バーと一覧下部の帯の文字サイズ上限。</summary>
    public const double ChromeTextMaxPt = 20;
    public const int TransientNoticeSeconds = 4;
    /// <summary>A70: 実効プレビュー文字サイズ = min(pt x 倍率, 60)。</summary>
    public const double PreviewTextMaxPt = 60;
    public const int MaxDeleteBatch = 200;
    public const int SearchResultCap = 500;
    public const double FirstLaunchListPaneRatio = 0.35;

    // A57: アイドル時の先読み
    public const int IdleBackfillIdleSeconds = 60;
    public const int IdleBackfillBodiesPerCycle = 20;
    public const long IdleBackfillInlinePartMaxBytes = 5L * 1024 * 1024;

    public const double MinButtonHeightDip = 56;
    public const double MinTargetDip = 48;
}
