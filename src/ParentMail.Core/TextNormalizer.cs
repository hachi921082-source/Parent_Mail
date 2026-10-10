using System.Text;

namespace ParentMail.Core;

// 検索・振り分け・アドレス帳で共通の正規化キー。
public static class TextNormalizer
{
    /// <summary>全角半角と大文字小文字の差を吸収したキーを返す（FormKC + 小文字化）。</summary>
    public static string NormalizeKey(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
    }
}
