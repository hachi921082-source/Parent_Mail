using ParentMail.Core.Features.FilterRules;
using ParentMail.Core.Models;

namespace ParentMail.Tests.FilterRules;

// ルールの判定（AND、最初の一致、無効のスキップ、本文未取得、HTML のみ、差出人のアドレス・表示名）。
public class FilterEvaluatorTests
{
    private static MailMessage Mail(string subject = "お支払いのお願い", string from = "pay@example.com", string name = "ＡＢＣ商事",
        string? text = "ご請求の件です", string? derived = null, BodyState state = BodyState.Fetched)
        => new()
        {
            GmailMsgId = 1, FromAddress = from, FromName = name, Subject = subject,
            BodyState = state, BodyText = text, DerivedText = derived,
        };

    private static FilterRule Rule(long id, int order, string? sender = null, string? subject = null, string? body = null,
        FilterAction action = FilterAction.MoveToTrash, bool enabled = true)
        => new() { Id = id, AccountId = "a", SortOrder = order, Enabled = enabled, SenderContains = sender,
                   SubjectContains = subject, BodyContains = body, Action = action };

    [Fact]
    public void All_given_conditions_must_match()
    {
        var m = Mail();
        Assert.NotNull(FilterEvaluator.Evaluate(new[] { Rule(1, 0, sender: "pay", subject: "お支払い") }, m));
        Assert.Null(FilterEvaluator.Evaluate(new[] { Rule(1, 0, sender: "pay", subject: "請求書") }, m));
        Assert.Null(FilterEvaluator.Evaluate(new[] { Rule(1, 0, sender: "nobody", subject: "お支払い") }, m));
        Assert.NotNull(FilterEvaluator.Evaluate(new[] { Rule(1, 0, sender: "pay", subject: "お支払い", body: "ご請求") }, m));
        Assert.Null(FilterEvaluator.Evaluate(new[] { Rule(1, 0, sender: "pay", subject: "お支払い", body: "ご注文") }, m));
    }

    [Fact]
    public void Rule_without_any_condition_never_matches()
    {
        Assert.Null(FilterEvaluator.Evaluate(new[] { Rule(1, 0), Rule(2, 1, sender: "  ") }, Mail()));
    }

    [Fact]
    public void First_match_by_sort_order_wins_regardless_of_list_order()
    {
        var rules = new[]
        {
            Rule(1, 2, subject: "お支払い", action: FilterAction.MoveToTrash),
            Rule(2, 0, subject: "お願い", action: FilterAction.MoveToSpam),
            Rule(3, 1, subject: "お支払い", action: FilterAction.MarkRead),
        };
        Assert.Equal(2, FilterEvaluator.Evaluate(rules, Mail())!.Id);
    }

    [Fact]
    public void Disabled_rules_are_skipped()
    {
        var rules = new[]
        {
            Rule(1, 0, subject: "お支払い", enabled: false),
            Rule(2, 1, subject: "お支払い", action: FilterAction.MarkRead),
        };
        Assert.Equal(2, FilterEvaluator.Evaluate(rules, Mail())!.Id);
        Assert.Null(FilterEvaluator.Evaluate(new[] { rules[0] }, Mail()));
    }

    [Fact]
    public void Matching_ignores_case_and_width()
    {
        Assert.NotNull(FilterEvaluator.Evaluate(new[] { Rule(1, 0, subject: "ｏｋ") }, Mail(subject: "Re: OK です")));
        Assert.NotNull(FilterEvaluator.Evaluate(new[] { Rule(1, 0, subject: "ＡＢＣ") }, Mail(subject: "abc 株式会社")));
        Assert.NotNull(FilterEvaluator.Evaluate(new[] { Rule(1, 0, subject: "ﾊﾟｰﾃｨｰ") }, Mail(subject: "パーティーのご案内")));
    }

    [Fact]
    public void Sender_condition_matches_address_or_display_name()
    {
        var m = Mail(from: "info@shop.example.com", name: "山田ストア");
        Assert.NotNull(FilterEvaluator.Evaluate(new[] { Rule(1, 0, sender: "SHOP.example") }, m));   // アドレス
        Assert.NotNull(FilterEvaluator.Evaluate(new[] { Rule(1, 0, sender: "山田") }, m));            // 表示名
        Assert.Null(FilterEvaluator.Evaluate(new[] { Rule(1, 0, sender: "佐藤") }, m));
    }

    [Fact]
    public void Undownloaded_body_never_matches_body_condition()
    {
        var m = Mail(text: null, state: BodyState.None);
        Assert.Null(FilterEvaluator.Evaluate(new[] { Rule(1, 0, body: "ご請求") }, m));
        // 本文以外の条件だけのルールは本文がなくても当たる
        Assert.NotNull(FilterEvaluator.Evaluate(new[] { Rule(1, 0, subject: "お支払い") }, m));
        // 本文の条件が加わると、ほかの条件が当たっていても当たらない
        Assert.Null(FilterEvaluator.Evaluate(new[] { Rule(1, 0, subject: "お支払い", body: "請求") }, m));
    }

    [Fact]
    public void Html_only_mail_is_judged_on_derived_text()
    {
        var m = Mail(text: null, derived: "セールのお知らせ 全品半額");
        Assert.NotNull(FilterEvaluator.Evaluate(new[] { Rule(1, 0, body: "半額") }, m));
        Assert.Null(FilterEvaluator.Evaluate(new[] { Rule(1, 0, body: "無料") }, m));
    }

    [Fact]
    public void Plain_text_is_preferred_over_derived_text()
    {
        var m = Mail(text: "プレーン本文", derived: "派生の本文");
        Assert.Null(FilterEvaluator.Evaluate(new[] { Rule(1, 0, body: "派生") }, m));
        Assert.NotNull(FilterEvaluator.Evaluate(new[] { Rule(1, 0, body: "プレーン") }, m));
    }

    [Fact]
    public void Fetched_mail_with_no_text_at_all_does_not_match_body()
    {
        Assert.Null(FilterEvaluator.Evaluate(new[] { Rule(1, 0, body: "請求") }, Mail(text: null, derived: null)));
    }
}
