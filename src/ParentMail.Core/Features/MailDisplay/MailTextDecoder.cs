using System.Text;
using MimeKit.Utils;

namespace ParentMail.Core.Features.MailDisplay;

/// <summary>
/// 表示専用: RFC 2047 の符号化語（=?UTF-8?B?...?=）を人が読める文字に戻す。
/// 保存データ（送信・返信に使う値）は変えない。復号できない文字列はそのまま返す。
/// </summary>
public static class MailTextDecoder
{
    public static string ForDisplay(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        // 符号化語が無い、または既に非 ASCII（復号済み）の文字列は触らない
        if (!text.Contains("=?", StringComparison.Ordinal) || text.Any(c => c > 127)) return text;
        try
        {
            var decoded = Rfc2047.DecodeText(Encoding.ASCII.GetBytes(text));
            return string.IsNullOrEmpty(decoded) ? text : decoded;
        }
        catch (Exception)
        {
            return text;
        }
    }
}
