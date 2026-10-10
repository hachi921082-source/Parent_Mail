using System.Text.RegularExpressions;

namespace ParentMail.Core.Platform;

/// <summary>ログに書く文字列から個人情報（メールアドレス・URL）を取り除く。</summary>
public static partial class LogScrubber
{
    public const string Replacement = "[removed]";

    // "@" を含む語（メールアドレス）と、scheme:// で始まる語（URL）を丸ごと置き換える。
    [GeneratedRegex(@"\S*@\S*|\S*://\S*")]
    private static partial Regex PersonalInfoPattern();

    public static string Scrub(string? text)
        => string.IsNullOrEmpty(text) ? string.Empty : PersonalInfoPattern().Replace(text, Replacement);
}
