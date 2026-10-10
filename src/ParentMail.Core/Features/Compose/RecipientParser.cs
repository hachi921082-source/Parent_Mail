using System.Text;
using MimeKit;

namespace ParentMail.Core.Features.Compose;

/// <summary>宛先 1 件分。Address は正しく読めたときだけ入る。Text は送信に使う形（名前付きなら「名前 &lt;アドレス&gt;」）。</summary>
public sealed record RecipientToken(string Text, string? Address, string? Name, bool IsValid);

/// <summary>
/// 宛先欄の文字列の分解と整形（compose PLAN-1 2 章）。
/// 区切りは , ; 、 空白 改行。ただし「名前 &lt;アドレス&gt;」や引用符の中は割らない。
/// </summary>
public static class RecipientParser
{
    // 名前に空白を含められるように、空白で割るのは「&lt;&gt; を含まない区切り片」だけにする
    private static readonly char[] HardSeparators = { ',', ';', '、', '\n', '\r' };

    public static IReadOnlyList<RecipientToken> Parse(string? text)
    {
        var result = new List<RecipientToken>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var segment in SplitHard(text))
        {
            if (segment.Contains('<'))
            {
                result.Add(ParseOne(segment));
                continue;
            }
            foreach (var word in segment.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                result.Add(ParseOne(word));
        }
        return result;
    }

    /// <summary>送信に使うアドレス文字列の一覧（検証済みのものだけ）。</summary>
    public static IReadOnlyList<string> ValidTexts(IEnumerable<RecipientToken> tokens)
        => tokens.Where(t => t.IsValid).Select(t => t.Text).ToList();

    /// <summary>「名前 &lt;アドレス&gt;」の形に整える。名前が空ならアドレスだけ。区切りや記号を含む名前は引用符で囲む。</summary>
    public static string Format(string? name, string address)
    {
        if (string.IsNullOrWhiteSpace(name)) return address;
        var n = name.Trim();
        if (n.IndexOfAny(new[] { ',', ';', '、', '<', '>', '"', '\n', '\r' }) >= 0)
            n = "\"" + n.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        return $"{n} <{address}>";
    }

    /// <summary>キャレット位置にある（まだ確定していない）入力中の宛先の範囲。区切りは , ; 、 改行だけ（名前の空白は許す）。</summary>
    public static (int Start, int Length) CurrentToken(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        var start = 0;
        for (var i = caret - 1; i >= 0; i--)
        {
            if (Array.IndexOf(HardSeparators, text[i]) >= 0) { start = i + 1; break; }
        }
        while (start < caret && char.IsWhiteSpace(text[start])) start++;
        return (start, caret - start);
    }

    /// <summary>入力中の宛先を候補に置き換え、続けて入力できるように「, 」を付ける。</summary>
    public static string ReplaceCurrentToken(string text, int caret, string insert, out int newCaret)
    {
        var (start, length) = CurrentToken(text, caret);
        var tail = text[(start + length)..];
        var head = text[..start] + insert + ", ";
        newCaret = head.Length;
        return head + tail;
    }

    // 引用符と &lt;&gt; の外にある区切りで割る
    private static IEnumerable<string> SplitHard(string text)
    {
        var sb = new StringBuilder();
        var inQuote = false;
        var inAngle = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuote && c == '\\' && i + 1 < text.Length) { sb.Append(c).Append(text[++i]); continue; }
            if (c == '"') inQuote = !inQuote;
            else if (!inQuote && c == '<') inAngle = true;
            else if (!inQuote && c == '>') inAngle = false;

            if (!inQuote && !inAngle && Array.IndexOf(HardSeparators, c) >= 0)
            {
                var s = sb.ToString().Trim();
                if (s.Length > 0) yield return s;
                sb.Clear();
            }
            else sb.Append(c);
        }
        var last = sb.ToString().Trim();
        if (last.Length > 0) yield return last;
    }

    private static RecipientToken ParseOne(string token)
    {
        var lt = token.LastIndexOf('<');
        var gt = token.LastIndexOf('>');
        if (lt >= 0)
        {
            if (gt != token.Length - 1 || gt < lt) return Invalid(token);
            var addr = token[(lt + 1)..gt].Trim();
            if (!TryBare(addr, out var address)) return Invalid(token);
            var name = Unquote(token[..lt].Trim());
            return new RecipientToken(Format(name, address), address, string.IsNullOrEmpty(name) ? null : name, true);
        }
        return TryBare(token, out var bare)
            ? new RecipientToken(bare, bare, null, true)
            : Invalid(token);
    }

    private static RecipientToken Invalid(string token) => new(token, null, null, false);

    // アドレス単体として読めるか。@ とドメインの点を必須にする（家族向けの入力ミスを早く見つけるため）
    private static bool TryBare(string text, out string address)
    {
        address = "";
        if (text.Length == 0 || text.IndexOfAny(new[] { ' ', '　', '<', '>', '"', ',' }) >= 0) return false;
        if (!MailboxAddress.TryParse(text, out var mailbox)) return false;
        var a = mailbox.Address;
        var at = a.LastIndexOf('@');
        if (at <= 0 || at == a.Length - 1 || !a[(at + 1)..].Contains('.')) return false;
        address = a;
        return true;
    }

    private static string Unquote(string name)
    {
        if (name.Length >= 2 && name[0] == '"' && name[^1] == '"')
            return name[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
        return name;
    }
}
