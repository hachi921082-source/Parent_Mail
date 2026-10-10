using ParentMail.Core.Models;

namespace ParentMail.App.Features.FilterRules;

// ルールの条件と動作を 1 行の日本語にする（一覧の「条件の説明」列）。
// 例: 差出人に『abc』を含む かつ 件名に『xyz』を含む → ごみ箱へ
public static class RuleDescriber
{
    public static string Describe(FilterRule rule)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(rule.SenderContains)) parts.Add($"差出人に『{rule.SenderContains.Trim()}』を含む");
        if (!string.IsNullOrWhiteSpace(rule.SubjectContains)) parts.Add($"件名に『{rule.SubjectContains.Trim()}』を含む");
        if (!string.IsNullOrWhiteSpace(rule.BodyContains)) parts.Add($"本文に『{rule.BodyContains.Trim()}』を含む");
        return string.Join(" かつ ", parts) + " → " + FilterText.ActionShort(rule.Action);
    }
}
