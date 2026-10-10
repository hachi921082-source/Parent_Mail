using ParentMail.Core.Features.Compose;

namespace ParentMail.Tests.Compose;

// 宛先文字列の分解と検証（compose PLAN-1 2 章）。日本語の表示名・全角の区切りを含む。
public class RecipientParserTests
{
    [Fact]
    public void Splits_on_all_separators()
    {
        var tokens = RecipientParser.Parse("a@example.com, b@example.com; c@example.com、d@example.com e@example.com");
        Assert.Equal(new[] { "a@example.com", "b@example.com", "c@example.com", "d@example.com", "e@example.com" },
            tokens.Select(t => t.Text));
        Assert.All(tokens, t => Assert.True(t.IsValid));
    }

    [Fact]
    public void Japanese_display_name_with_space_stays_one_token()
    {
        var tokens = RecipientParser.Parse("山田 太郎 <taro@example.com>、鈴木花子 <hanako@example.com>");
        Assert.Equal(2, tokens.Count);
        Assert.Equal("山田 太郎", tokens[0].Name);
        Assert.Equal("taro@example.com", tokens[0].Address);
        Assert.Equal("山田 太郎 <taro@example.com>", tokens[0].Text);
        Assert.Equal("鈴木花子", tokens[1].Name);
    }

    [Fact]
    public void Quoted_name_with_comma_is_kept()
    {
        var tokens = RecipientParser.Parse("\"山田, 太郎\" <taro@example.com>");
        Assert.Single(tokens);
        Assert.True(tokens[0].IsValid);
        Assert.Equal("山田, 太郎", tokens[0].Name);
    }

    [Fact]
    public void Invalid_tokens_are_marked()
    {
        var tokens = RecipientParser.Parse("abc, ok@example.com, 山田 <broken>");
        Assert.False(tokens[0].IsValid);
        Assert.True(tokens[1].IsValid);
        Assert.False(tokens[2].IsValid);
    }

    [Fact]
    public void Empty_and_blank_give_no_tokens()
    {
        Assert.Empty(RecipientParser.Parse(""));
        Assert.Empty(RecipientParser.Parse(" ,; 、 \n"));
    }

    [Fact]
    public void Format_quotes_names_with_separators()
    {
        Assert.Equal("山田 太郎 <a@example.com>", RecipientParser.Format("山田 太郎", "a@example.com"));
        Assert.Equal("\"山田, 太郎\" <a@example.com>", RecipientParser.Format("山田, 太郎", "a@example.com"));
        Assert.Equal("a@example.com", RecipientParser.Format("", "a@example.com"));
    }

    [Fact]
    public void Current_token_is_text_after_last_hard_separator()
    {
        var text = "a@example.com, 山田 太";
        var (start, length) = RecipientParser.CurrentToken(text, text.Length);
        Assert.Equal("山田 太", text.Substring(start, length));
    }

    [Fact]
    public void Replace_current_token_appends_separator_and_moves_caret()
    {
        var text = "a@example.com, やま";
        var result = RecipientParser.ReplaceCurrentToken(text, text.Length, "山田 太郎 <t@example.com>", out var caret);
        Assert.Equal("a@example.com, 山田 太郎 <t@example.com>, ", result);
        Assert.Equal(result.Length, caret);
    }
}
