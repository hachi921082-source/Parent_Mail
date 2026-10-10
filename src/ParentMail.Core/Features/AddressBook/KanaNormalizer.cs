using System.Text;

namespace ParentMail.Core.Features.AddressBook;

// ふりがなの並べ替え・検索用キー。カタカナをひらがなに、全角半角と大文字小文字をそろえ、空白は無視する。
public static class KanaNormalizer
{
    public static string ToHiraganaKey(string? text)
    {
        var s = TextNormalizer.NormalizeKey(text);
        if (s.Length == 0) return "";

        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch)) continue;
            if (ch >= 'ァ' && ch <= 'ヶ') sb.Append((char)(ch - 0x60));      // ァ-ヶ → ぁ-ゖ
            else if (ch == 'ヽ' || ch == 'ヾ') sb.Append((char)(ch - 0x60)); // ヽヾ → ゝゞ
            else sb.Append(ch);
        }
        return sb.ToString();
    }
}
