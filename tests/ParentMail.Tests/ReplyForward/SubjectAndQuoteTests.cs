using ParentMail.App.Features.ReplyForward;

namespace ParentMail.Tests.ReplyForward;

// 題名の接頭辞と引用の整形（reply-forward PLAN 2 章）。
public class SubjectAndQuoteTests
{
    [Theory]
    [InlineData("会議のご案内", "Re: 会議のご案内")]
    [InlineData("", "Re: ")]
    [InlineData("Re: 会議", "Re: 会議")]
    [InlineData("RE: 会議", "RE: 会議")]
    [InlineData("re: 会議", "re: 会議")]
    [InlineData("返信: 会議", "返信: 会議")]
    [InlineData("  Re: 会議", "  Re: 会議")]
    [InlineData("Fwd: 会議", "Re: Fwd: 会議")]
    public void Reply_prefix_is_added_once(string subject, string expected)
        => Assert.Equal(expected, SubjectPrefixer.Add(PrefixKind.Reply, subject));

    [Theory]
    [InlineData("会議のご案内", "Fwd: 会議のご案内")]
    [InlineData("Fwd: 会議", "Fwd: 会議")]
    [InlineData("FWD: 会議", "FWD: 会議")]
    [InlineData("Fw: 会議", "Fw: 会議")]
    [InlineData("転送: 会議", "転送: 会議")]
    [InlineData("Re: 会議", "Fwd: Re: 会議")]
    public void Forward_prefix_is_added_once(string subject, string expected)
        => Assert.Equal(expected, SubjectPrefixer.Add(PrefixKind.Forward, subject));

    [Fact]
    public void Quote_prefixes_each_line_with_Japanese_text()
        => Assert.Equal("> こんにちは\r\n> お元気ですか", QuoteFormatter.Quote("こんにちは\nお元気ですか"));

    [Fact]
    public void Quote_handles_crlf_and_blank_lines_without_trailing_space()
        => Assert.Equal("> 一行目\r\n>\r\n> 三行目", QuoteFormatter.Quote("一行目\r\n\r\n三行目\r\n"));

    [Fact]
    public void Quote_of_nothing_is_a_single_marker_line()
        => Assert.Equal(">", QuoteFormatter.Quote(""));
}
