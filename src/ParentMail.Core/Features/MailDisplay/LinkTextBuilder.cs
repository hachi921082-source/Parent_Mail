using System.Text.RegularExpressions;

namespace ParentMail.Core.Features.MailDisplay;

/// <summary>プレーン本文の 1 区切り。Url が入っていればリンク。</summary>
public sealed record TextSegment(string Text, string? Url);

/// <summary>
/// プレーン本文から URL を見つけて、文字とリンクの並びに分ける（A18）。
/// URL に使える文字は ASCII の範囲に限るので、日本語が URL の直後に続いてもそこで切れる。
/// </summary>
public static class LinkTextBuilder
{
    private static readonly Regex UrlPattern = new(
        @"(?<![A-Za-z0-9@.\-])(?:https?://|www\.|mailto:)[A-Za-z0-9\-._~:/?#\[\]@!$&'()*+,;=%]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly char[] TrailingMarks = { '.', ',', ';', ':', '!', '?', ')', ']', '\'' };

    public static IReadOnlyList<TextSegment> ToSegments(string? text)
    {
        var result = new List<TextSegment>();
        if (string.IsNullOrEmpty(text)) return result;

        var pos = 0;
        foreach (Match m in UrlPattern.Matches(text))
        {
            var shown = m.Value.TrimEnd(TrailingMarks);
            // 接頭辞だけ（"https://" など）はリンクにしない
            if (!HasBody(shown)) continue;

            if (m.Index > pos) result.Add(new TextSegment(text[pos..m.Index], null));
            result.Add(new TextSegment(shown, Normalize(shown)));
            pos = m.Index + shown.Length;
        }
        if (pos < text.Length) result.Add(new TextSegment(text[pos..], null));
        return result;
    }

    private static bool HasBody(string s)
    {
        foreach (var prefix in new[] { "https://", "http://", "www.", "mailto:" })
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return s.Length > prefix.Length;
        return false;
    }

    // www. で始まるものは https を補う
    private static string Normalize(string s)
        => s.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + s : s;
}
