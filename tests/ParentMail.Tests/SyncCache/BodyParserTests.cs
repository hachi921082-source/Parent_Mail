using MimeKit;
using ParentMail.Core.Features.SyncCache;

namespace ParentMail.Tests.SyncCache;

public class BodyParserTests
{
    [Fact]
    public void Parse_AlternativeWithAttachment_ReturnsBothBodiesAndAttachmentMetadata()
    {
        var alt = new MultipartAlternative
        {
            new TextPart("plain") { Text = "こんにちは\n母より" },
            new TextPart("html") { Text = "<p>こんにちは</p><p>母より</p>" },
        };
        var pdf = new MimePart("application", "pdf")
        {
            Content = new MimeContent(new MemoryStream(new byte[1234])),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = "請求書.pdf" },
            ContentTransferEncoding = ContentEncoding.Binary,
        };
        var msg = new MimeMessage { Body = new Multipart("mixed") { alt, pdf } };

        var r = BodyParser.Parse(msg);

        Assert.Equal("こんにちは\n母より", r.Text);
        Assert.Contains("<p>こんにちは</p>", r.Html);
        Assert.Equal("こんにちは\n\n母より", r.DerivedText);   // 段落の間は空行
        var a = Assert.Single(r.Attachments);
        Assert.Equal("請求書.pdf", a.FileName);
        Assert.Equal("application/pdf", a.ContentType);
        Assert.Equal("2", a.PartId);              // IMAP の BODYSTRUCTURE と同じ番号
        Assert.False(a.IsInline);
    }

    [Fact]
    public void Parse_InlineImage_IsInlineAttachmentWithContentId()
    {
        var img = new MimePart("image", "png")
        {
            Content = new MimeContent(new MemoryStream(new byte[10])),
            ContentId = "logo@example",
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
            ContentTransferEncoding = ContentEncoding.Binary,
        };
        var related = new Multipart("related") { new TextPart("html") { Text = "<img src=\"cid:logo@example\">" }, img };
        var r = BodyParser.Parse(new MimeMessage { Body = related });

        Assert.Null(r.Text);
        var a = Assert.Single(r.Attachments);
        Assert.True(a.IsInline);
        Assert.Equal("logo@example", a.ContentId);
    }

    [Fact]
    public void Parse_SinglePlainTextPart_HasNoAttachments()
    {
        var r = BodyParser.Parse(new MimeMessage { Body = new TextPart("plain") { Text = "本文のみ" } });
        Assert.Equal("本文のみ", r.Text);
        Assert.Null(r.Html);
        Assert.Null(r.DerivedText);
        Assert.Empty(r.Attachments);
    }

    [Fact]
    public void HtmlToText_StripsScriptAndStyle_BlocksBecomeNewlines_DecodesEntities()
    {
        var html = "<html><head><title>t</title><style>p{color:red}</style></head><body>" +
                   "<script>alert(1)</script><div>一行目</div><p>二行目 &amp; &lt;三&gt;&nbsp;です</p>四行目<br>五行目</body></html>";

        var text = BodyParser.HtmlToText(html);

        Assert.DoesNotContain("alert", text);
        Assert.DoesNotContain("color", text);
        Assert.Equal("一行目\n二行目 & <三> です\n四行目\n五行目", text.Replace("\n\n", "\n"));
    }

    [Fact]
    public void HtmlToText_CollapsesWhitespaceInSourceFormatting()
    {
        Assert.Equal("a b", BodyParser.HtmlToText("<p>a\n   b</p>"));
    }
}
