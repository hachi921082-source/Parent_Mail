using ParentMail.App.Resources;

namespace ParentMail.App.Features.MailList;

/// <summary>
/// 送信済み・下書き・送信箱の行で、差出人の位置に出す宛先の要約。
/// 1 人なら「宛先: 先頭」、2 人以上なら「宛先: 先頭 ほか N 人」（N は残りの人数、A67）。
/// </summary>
public static class RecipientSummary
{
    private const string Prefix = "宛先: ";

    public static string Format(IReadOnlyList<string>? recipients)
    {
        var list = recipients?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList()
                   ?? new List<string>();
        // 宛先が 1 件も無いときは、差出人なしと同じ文言で埋める（専用の文言は無い）
        if (list.Count == 0) return Strings.ML_12;
        return list.Count == 1
            ? Prefix + list[0]
            : Prefix + list[0] + " ほか " + (list.Count - 1) + " 人";
    }
}
