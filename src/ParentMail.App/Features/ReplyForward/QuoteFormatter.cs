using System.Text;

namespace ParentMail.App.Features.ReplyForward;

/// <summary>返信の引用づくり（reply-forward PLAN 2 章）。元の各行の頭に「> 」を付ける。</summary>
public static class QuoteFormatter
{
    /// <summary>作成窓の本文欄に合わせて、改行は CRLF にそろえる。</summary>
    public const string NewLine = "\r\n";

    /// <summary>空行は「>」だけにする（行末の余計な空白を避ける）。</summary>
    public static string Quote(string? text)
    {
        var lines = SplitLines(text ?? "");
        var sb = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0) sb.Append(NewLine);
            sb.Append(lines[i].Length == 0 ? ">" : "> " + lines[i]);
        }
        return sb.ToString();
    }

    /// <summary>CRLF / LF / CR のどれでも行に割る。末尾の改行 1 つぶんは空行として数えない。</summary>
    public static List<string> SplitLines(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (normalized.EndsWith('\n')) normalized = normalized[..^1];
        return normalized.Split('\n').ToList();
    }
}
