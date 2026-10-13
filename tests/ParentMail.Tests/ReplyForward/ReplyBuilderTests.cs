using ParentMail.App.Features.ReplyForward;
using ParentMail.Core.Models;

namespace ParentMail.Tests.ReplyForward;

// 返信・全員に返信の初期値（reply-forward PLAN 2 章）。
public class ReplyBuilderTests
{
    private const string Acc = "acc1";
    private const string Own = "me@example.com";

    private static MailMessage Mail(Action<MailMessageDraft>? edit = null)
    {
        var d = new MailMessageDraft();
        edit?.Invoke(d);
        return new MailMessage
        {
            GmailMsgId = 7, MessageIdHeader = d.MessageId, References = d.References, Subject = d.Subject,
            FromAddress = "taro@example.com", FromName = d.FromName,
            To = d.To, Cc = d.Cc, DateUtc = new DateTime(2026, 10, 5, 0, 3, 0, DateTimeKind.Utc),
            BodyText = d.BodyText, DerivedText = d.DerivedText,
        };
    }

    private sealed class MailMessageDraft
    {
        public string MessageId = "<orig@example.com>";
        public string? References;
        public string Subject = "会議のご案内";
        public string FromName = "山田 太郎";
        public IReadOnlyList<string> To = new[] { Own };
        public IReadOnlyList<string> Cc = Array.Empty<string>();
        public string? BodyText = "お世話になります。\n明日の会議です。";
        public string? DerivedText;
    }

    [Fact]
    public void Reply_goes_to_sender_with_prefix_and_empty_cc()
    {
        var init = ReplyBuilder.BuildReply(Acc, Mail(), Own, replyAll: false);

        Assert.Equal(Acc, init.AccountId);
        Assert.Equal(new[] { "山田 太郎 <taro@example.com>" }, init.To);
        Assert.Empty(init.Cc);
        Assert.Equal("Re: 会議のご案内", init.Subject);
        Assert.Empty(init.Attachments);
    }

    [Fact]
    public void Reply_to_header_wins_over_from()
    {
        var init = ReplyBuilder.BuildReply(Acc, Mail(), Own, replyAll: false, replyTo: "office@example.com");
        Assert.Equal(new[] { "office@example.com" }, init.To);
    }

    [Fact]
    public void Reply_with_no_sender_name_uses_bare_address()
    {
        var init = ReplyBuilder.BuildReply(Acc, Mail(d => d.FromName = ""), Own, replyAll: false);
        Assert.Equal(new[] { "taro@example.com" }, init.To);
    }

    [Fact]
    public void Reply_body_has_blank_lines_then_dated_quote_header_and_quoted_lines()
    {
        var m = Mail();
        var init = ReplyBuilder.BuildReply(Acc, m, Own, replyAll: false);

        var date = ReplyForwardText.FormatDate(m.DateUtc);
        Assert.Equal("\r\n\r\n" + date + "、山田 太郎 さんは書きました:\r\n> お世話になります。\r\n> 明日の会議です。", init.Body);
        Assert.Matches(@"^\d{4}年\d{1,2}月\d{1,2}日 \d{1,2}:\d{2}$", date);
    }

    [Fact]
    public void Reply_quote_header_falls_back_to_address_when_no_name()
    {
        var init = ReplyBuilder.BuildReply(Acc, Mail(d => d.FromName = ""), Own, replyAll: false);
        Assert.Contains("、taro@example.com さんは書きました:", init.Body);
    }

    [Fact]
    public void Reply_quotes_derived_text_when_body_text_is_missing()
    {
        var init = ReplyBuilder.BuildReply(Acc, Mail(d => { d.BodyText = null; d.DerivedText = "HTML由来の本文"; }), Own, replyAll: false);
        Assert.EndsWith("> HTML由来の本文", init.Body);
    }

    [Fact]
    public void Reply_all_removes_own_address_and_duplicates_case_insensitively()
    {
        var m = Mail(d =>
        {
            d.To = new[] { "自分 <ME@Example.com>", "hanako@example.com", "Hanako@example.com" };
            d.Cc = new[] { "Taro@Example.com", "jiro <jiro@example.com>", "hanako@example.com" };
        });

        var init = ReplyBuilder.BuildReply(Acc, m, Own, replyAll: true);

        // 自分・差出人（To に入る）・重複は除く
        Assert.Equal(new[] { "hanako@example.com", "jiro <jiro@example.com>" }, init.Cc);
        Assert.Single(init.To);
    }

    [Fact]
    public void Reply_all_does_not_copy_reply_to_address_again()
    {
        var m = Mail(d => d.Cc = new[] { "office@example.com", "x@example.com" });
        var init = ReplyBuilder.BuildReply(Acc, m, Own, replyAll: true, replyTo: "office@example.com");
        Assert.Equal(new[] { "x@example.com" }, init.Cc);
    }

    [Fact]
    public void Plain_reply_ignores_original_cc()
    {
        var init = ReplyBuilder.BuildReply(Acc, Mail(d => d.Cc = new[] { "x@example.com" }), Own, replyAll: false);
        Assert.Empty(init.Cc);
    }

    [Fact]
    public void Reply_headers_chain_references_and_message_id()
    {
        var init = ReplyBuilder.BuildReply(Acc, Mail(d => d.References = "<a@x> <b@x>"), Own, replyAll: false);
        Assert.Equal("<orig@example.com>", init.ReplyHeaders!.InReplyTo);
        Assert.Equal("<a@x> <b@x> <orig@example.com>", init.ReplyHeaders.References);
    }

    [Fact]
    public void Reply_headers_without_prior_references_use_message_id_only()
    {
        var init = ReplyBuilder.BuildReply(Acc, Mail(), Own, replyAll: false);
        Assert.Equal("<orig@example.com>", init.ReplyHeaders!.References);
    }

    [Fact]
    public void Reply_headers_are_empty_when_original_has_no_message_id()
    {
        var init = ReplyBuilder.BuildReply(Acc, Mail(d => d.MessageId = ""), Own, replyAll: false);
        Assert.Null(init.ReplyHeaders!.InReplyTo);
        Assert.Null(init.ReplyHeaders.References);
    }
}
