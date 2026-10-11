using ParentMail.App.Resources;

namespace ParentMail.App.Features.MailDisplay;

// ui-strings.md では 1 行に複数の語を " / " でまとめた項目があるので、ここで分けて名前を付ける。
public static class PreviewText
{
    private static readonly string[] HeaderLabels = Strings.PV_01.Split(" / ");
    private static readonly string[] ZoomLabels = Strings.TB_21.Split(" / ");
    private static readonly string[] AttachmentButtons = Strings.PV_07.Split(" / ");
    private static readonly string[] SaveConfirmButtons = Strings.PV_10.Split(" / ");

    /// <summary>差出人</summary>
    public static string From => HeaderLabels[0];
    /// <summary>宛先</summary>
    public static string To => HeaderLabels[1];
    /// <summary>CC</summary>
    public static string Cc => HeaderLabels[2];
    /// <summary>件名</summary>
    public static string Subject => HeaderLabels[3];
    /// <summary>日時</summary>
    public static string Date => HeaderLabels[4];

    /// <summary>拡大</summary>
    public static string ZoomIn => ZoomLabels[0];
    /// <summary>縮小</summary>
    public static string ZoomOut => ZoomLabels[1];
    /// <summary>100%に戻す</summary>
    public static string ZoomReset => ZoomLabels[2];

    /// <summary>開く（添付の行）</summary>
    public static string Open => AttachmentButtons[0];
    /// <summary>保存（添付の行）</summary>
    public static string Save => AttachmentButtons[1];
    /// <summary>保存する（危険な添付の確認）</summary>
    public static string SaveConfirmYes => SaveConfirmButtons[0];
    /// <summary>キャンセル（危険な添付の確認。既定）</summary>
    public static string SaveConfirmNo => SaveConfirmButtons[1];

    /// <summary>複数選択の案内（N を実際の数に置き換える）。</summary>
    public static string MultiSelect(int count) => Strings.PV_13.Replace("N通", $"{count}通");
}
