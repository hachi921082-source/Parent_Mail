using System.Globalization;

namespace ParentMail.App.Features.MailList;

/// <summary>
/// 一覧の日時表示（Q44）。今日は「HH:mm」、今年は「M月d日 H:mm」、それより前は「yyyy年M月d日 H:mm」。
/// 受け取った UTC の時刻を、now の UTC オフセット（端末のローカル時刻）に直してから比べる。
/// </summary>
public static class DateFormatter
{
    public static string Format(DateTimeOffset utc, DateTimeOffset now)
    {
        var local = utc.ToOffset(now.Offset);
        var ci = CultureInfo.InvariantCulture;
        if (local.Date == now.Date) return local.ToString("HH:mm", ci);
        if (local.Year == now.Year) return local.ToString("M月d日 H:mm", ci);
        return local.ToString("yyyy年M月d日 H:mm", ci);
    }

    /// <summary>DateTime（UTC として保存された値）を使う入口。Kind が未指定でも UTC として扱う。</summary>
    public static string Format(DateTime utc, DateTime localNow)
    {
        var u = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        var n = localNow.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(localNow).ToLocalTime()
            : new DateTimeOffset(DateTime.SpecifyKind(localNow, DateTimeKind.Local));
        return Format(u, n);
    }
}
