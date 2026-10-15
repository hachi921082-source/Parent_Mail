using System.Globalization;

namespace ParentMail.Core.Features.MailDisplay;

/// <summary>プレビューのヘッダ表示用の文字列を作る（宛先の並び、差出人、日時）。</summary>
public static class PreviewHeaderFormatter
{
    /// <summary>差出人。名前があれば「名前 &lt;アドレス&gt;」、なければアドレスだけ（なりすましに気づけるよう両方見せる）。</summary>
    public static string FormatFrom(string name, string address)
    {
        name = MailTextDecoder.ForDisplay(name).Trim();
        address = MailTextDecoder.ForDisplay(address).Trim();
        if (name.Length == 0 || string.Equals(name, address, StringComparison.OrdinalIgnoreCase)) return address;
        if (address.Length == 0) return name;
        return $"{name} <{address}>";
    }

    /// <summary>宛先・CC の並び。読点でつなぐ。</summary>
    public static string FormatList(IEnumerable<string> addresses)
        => string.Join("、", addresses.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => MailTextDecoder.ForDisplay(a.Trim())));

    /// <summary>日時（ローカル時刻）。「2026年10月8日 9:05」の形。</summary>
    public static string FormatDate(DateTime utc)
    {
        if (utc == default) return "";
        var asUtc = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return asUtc.ToLocalTime().ToString("yyyy年M月d日 H:mm", CultureInfo.InvariantCulture);
    }
}
