using ParentMail.Core.Models;

namespace ParentMail.Core.Features.FilterRules;

// 振り分けルールの判定。どのルールが当たるかだけを返し、動作は FilterRuleHandler が行う。
public static class FilterEvaluator
{
    /// <summary>
    /// 並び順の小さい有効なルールから調べ、最初に当たったものを返す（当たらなければ null）。
    /// 条件は全部入れたものすべてを満たすこと（AND）。条件が 1 つもないルールは当たらない。
    /// </summary>
    public static FilterRule? Evaluate(IEnumerable<FilterRule> rules, MailMessage message)
    {
        foreach (var rule in rules.Where(r => r.Enabled).OrderBy(r => r.SortOrder).ThenBy(r => r.Id))
        {
            if (Matches(rule, message)) return rule;
        }
        return null;
    }

    public static bool Matches(FilterRule rule, MailMessage message)
    {
        var sender = Key(rule.SenderContains);
        var subject = Key(rule.SubjectContains);
        var body = Key(rule.BodyContains);
        if (sender.Length == 0 && subject.Length == 0 && body.Length == 0) return false;

        // 差出人はアドレスと表示名のどちらに含まれてもよい
        if (sender.Length > 0 &&
            !Contains(message.FromAddress, sender) && !Contains(message.FromName, sender)) return false;
        if (subject.Length > 0 && !Contains(message.Subject, subject)) return false;
        if (body.Length > 0 && !Contains(BodyOf(message), body)) return false;
        return true;
    }

    // 本文が未取得のメールは本文条件に当たらない（ルールのために取得はしない）。
    // HTML だけのメールは、そこから作った本文テキスト（DerivedText）で判定する。
    private static string? BodyOf(MailMessage m)
    {
        if (m.BodyState != BodyState.Fetched) return null;
        return string.IsNullOrEmpty(m.BodyText) ? m.DerivedText : m.BodyText;
    }

    private static string Key(string? condition) => TextNormalizer.NormalizeKey(condition?.Trim());

    private static bool Contains(string? text, string key)
        => !string.IsNullOrEmpty(text) && TextNormalizer.NormalizeKey(text).Contains(key, StringComparison.Ordinal);
}
