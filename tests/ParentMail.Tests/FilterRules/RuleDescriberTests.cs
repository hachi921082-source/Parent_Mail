using ParentMail.App.Features.FilterRules;
using ParentMail.App.Resources;
using ParentMail.Core.Models;

namespace ParentMail.Tests.FilterRules;

// 一覧の「条件の説明」列の文章。
public class RuleDescriberTests
{
    [Fact]
    public void Sample_from_ui_strings_FR04()
    {
        var rule = new FilterRule { SenderContains = "abc", SubjectContains = "xyz", Action = FilterAction.MoveToTrash };
        Assert.Equal(Strings.FR_04, RuleDescriber.Describe(rule));
    }

    [Theory]
    [InlineData("山田", null, null, FilterAction.MoveToSpam, "差出人に『山田』を含む → 迷惑メールへ")]
    [InlineData(null, "請求書", null, FilterAction.MarkRead, "件名に『請求書』を含む → 既読に")]
    [InlineData(null, null, "お振込み", FilterAction.MoveToTrash, "本文に『お振込み』を含む → ごみ箱へ")]
    [InlineData("a", "b", "c", FilterAction.MoveToSpam, "差出人に『a』を含む かつ 件名に『b』を含む かつ 本文に『c』を含む → 迷惑メールへ")]
    public void Describes_each_condition_in_order(string? sender, string? subject, string? body, FilterAction action, string expected)
    {
        var rule = new FilterRule { SenderContains = sender, SubjectContains = subject, BodyContains = body, Action = action };
        Assert.Equal(expected, RuleDescriber.Describe(rule));
    }

    [Fact]
    public void Blank_conditions_are_left_out_and_text_is_trimmed()
    {
        var rule = new FilterRule { SenderContains = "  ", SubjectContains = " 件名 ", Action = FilterAction.MarkRead };
        Assert.Equal("件名に『件名』を含む → 既読に", RuleDescriber.Describe(rule));
    }

    [Fact]
    public void Action_texts_follow_FR02()
    {
        Assert.Equal("迷惑メールへ移動", FilterText.ActionText(FilterAction.MoveToSpam));
        Assert.Equal("ごみ箱へ移動", FilterText.ActionText(FilterAction.MoveToTrash));
        Assert.Equal("既読にする", FilterText.ActionText(FilterAction.MarkRead));
    }
}
