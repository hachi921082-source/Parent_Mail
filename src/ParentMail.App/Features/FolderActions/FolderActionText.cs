using ParentMail.App.Resources;

namespace ParentMail.App.Features.FolderActions;

/// <summary>
/// folder-actions の文言。ui-strings.md の文言（Strings）を、表示の場所ごとに切り分ける。
/// 「A / B / C」の形で 1 つにまとめられた文言は区切って使う。
/// </summary>
public static class FolderActionText
{
    private static readonly string[] Reply3 = Strings.FA_01.Split(" / ");
    private static readonly string[] Read2 = Strings.FA_02.Split(" / ");
    private static readonly string[] Block2 = Strings.FA_10.Split(" / ");

    public static string Reply => Reply3[0];
    public static string ReplyAll => Reply3[1];
    public static string Forward => Reply3[2];
    public static string MarkRead => Read2[0];
    public static string MarkUnread => Read2[1];
    public static string MoveToSpam => Strings.FA_03;
    public static string Delete => Strings.FA_04;
    public static string AddToAddressBook => Strings.FA_05;
    public static string AddToBlockList => Strings.FA_06;
    public static string Edit => Strings.FA_07;
    public static string NotSpam => Strings.FA_08;
    public static string Restore => Strings.FA_09;
    public static string SendNow => Strings.OB_03;
    public static string ReturnToDrafts => Strings.OB_04;

    /// <summary>複数選択中の削除ボタン・メニューの文言「選んだN通を削除」。</summary>
    public static string DeleteChecked(int count) => Strings.TB_23.Replace("N", count.ToString());

    // ブロックの選択肢（ダイアログの「このアドレスだけ」「このドメイン全体」）
    public static string BlockAddressOnly => Block2[0];
    public static string BlockWholeDomain => Block2[1];

    /// <summary>受信拒否の追加ダイアログ WD-16 の「やめる」（ui-strings の WD-16 ボタン欄「追加*, やめる」）。</summary>
    public const string Cancel = "やめる";

    /// <summary>ごみ箱へ移したあとの通知の本文（末尾の「[元に戻す]」は除く）。1 通のとき BN-07、複数のとき BN-11。</summary>
    public static string MovedToTrash(int count)
        => TextBeforeButton(count == 1 ? Strings.BN_07 : Strings.BN_11.Replace("N", count.ToString()));

    /// <summary>通知の操作ボタン「元に戻す」。</summary>
    public static string UndoLabel { get; } = ButtonOf(Strings.BN_07);

    /// <summary>一部だけ移せなかったときの文言 BN-21。total 通のうち failed 通。</summary>
    public static string PartialFailure(int total, int failed)
        => Strings.BN_21.Replace("N", total.ToString()).Replace("M", failed.ToString());

    private static string TextBeforeButton(string s)
    {
        var i = s.LastIndexOf(" [", StringComparison.Ordinal);
        return i < 0 ? s : s[..i];
    }

    private static string ButtonOf(string s)
    {
        var i = s.LastIndexOf('[');
        var j = s.LastIndexOf(']');
        return i >= 0 && j > i ? s[(i + 1)..j] : s;
    }
}
