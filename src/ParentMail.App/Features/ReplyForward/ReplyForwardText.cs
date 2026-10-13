using System.Globalization;
using ParentMail.App.Resources;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.ReplyForward;

/// <summary>返信・転送の本文や通知に出す文言の組み立て（RF-03〜06、BN-23）。</summary>
public static class ReplyForwardText
{
    // RF-04: 転送の見出しと、続く 4 行の項目名（ui-strings の行は 1 つの定数にまとまっているので、ここで分ける）
    public const string ForwardRule = "---------- 転送メッセージ ----------";
    public const string LabelFrom = "差出人: ";
    public const string LabelDate = "日時: ";
    public const string LabelSubject = "件名: ";
    public const string LabelTo = "宛先: ";

    /// <summary>「2026年10月5日 9:03」（受信日時を端末の時刻で）。</summary>
    public static string FormatDate(DateTime utc)
    {
        var local = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        return local.ToString("yyyy年M月d日 H:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>送信者名。無ければアドレス。</summary>
    public static string SenderName(MailMessage m)
        => !string.IsNullOrWhiteSpace(m.FromName) ? m.FromName.Trim() : m.FromAddress;

    /// <summary>RF-03: 引用の先頭行。</summary>
    public static string QuoteHeader(MailMessage m) => $"{FormatDate(m.DateUtc)}、{SenderName(m)} さんは書きました:";

    /// <summary>RF-05: 付けられなかったファイル名の一覧つき。</summary>
    public static string RejectedFiles(IEnumerable<string> names)
        => Strings.RF_05.Replace("<file names>", string.Join("、", names));
}
