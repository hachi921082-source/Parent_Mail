using MimeKit;
using ParentMail.Core.Features.OutboxSend;
using ParentMail.Core.Models;

namespace ParentMail.Tests.OutboxSend;

public sealed class MessageBuilderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "親メール_構築_" + Guid.NewGuid().ToString("N"));

    public MessageBuilderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static OutboxItem Item(Action<OutboxItemBuilder>? tweak = null)
    {
        var b = new OutboxItemBuilder();
        tweak?.Invoke(b);
        return b.Item;
    }

    private sealed class OutboxItemBuilder
    {
        public OutboxItem Item { get; set; } = new()
        {
            Id = 1, AccountId = "a1", To = new[] { "to@example.com" }, Subject = "こんにちは", Body = "本文です。",
        };
    }

    private static string Wire(MimeMessage m)
    {
        // 送信時と同じく Bcc ヘッダは出力しない設定で直列化する
        var opt = FormatOptions.Default.Clone();
        opt.HiddenHeaders.Add(HeaderId.Bcc);
        opt.HiddenHeaders.Add(HeaderId.ResentBcc);
        using var ms = new MemoryStream();
        m.WriteTo(opt, ms);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact]
    public void From_has_no_display_name()
    {
        var m = MessageBuilder.Build(Item(), "me@gmail.com", _dir);
        var from = Assert.IsType<MailboxAddress>(Assert.Single(m.From));
        Assert.Equal("me@gmail.com", from.Address);
        Assert.True(string.IsNullOrEmpty(from.Name));
        Assert.Contains("From: me@gmail.com", Wire(m));
    }

    [Fact]
    public void Bcc_is_in_envelope_but_not_in_wire_headers()
    {
        var item = Item(b => b.Item = b.Item with { Bcc = new[] { "secret@example.com" }, Cc = new[] { "cc@example.com" } });
        var m = MessageBuilder.Build(item, "me@gmail.com", _dir);
        Assert.Contains(m.Bcc.Mailboxes, x => x.Address == "secret@example.com");
        var wire = Wire(m);
        Assert.DoesNotContain("secret@example.com", wire);
        Assert.Contains("cc@example.com", wire);
    }

    [Fact]
    public void Reply_headers_are_emitted()
    {
        var item = Item(b => b.Item = b.Item with { ReplyHeaders = new ReplyHeaders("<abc@mail.example>", "<first@mail.example> <abc@mail.example>") });
        var wire = Wire(MessageBuilder.Build(item, "me@gmail.com", _dir));
        Assert.Contains("In-Reply-To: <abc@mail.example>", wire);
        Assert.Contains("References: <first@mail.example> <abc@mail.example>", wire.Replace("\r\n ", " "));
    }

    [Fact]
    public void Reply_headers_without_angle_brackets_are_normalized()
    {
        var item = Item(b => b.Item = b.Item with { ReplyHeaders = new ReplyHeaders("abc@mail.example", null) });
        var wire = Wire(MessageBuilder.Build(item, "me@gmail.com", _dir));
        Assert.Contains("In-Reply-To: <abc@mail.example>", wire);
        Assert.DoesNotContain("References:", wire);
    }

    [Fact]
    public void Message_id_uses_account_domain_and_date_is_set()
    {
        var now = new DateTimeOffset(2026, 10, 10, 1, 2, 3, TimeSpan.Zero);
        var m = MessageBuilder.Build(Item(), "me@gmail.com", _dir, now);
        Assert.EndsWith("@gmail.com", m.MessageId);
        Assert.Equal(now, m.Date);
    }

    [Fact]
    public void Subject_and_body_are_utf8()
    {
        var m = MessageBuilder.Build(Item(), "me@gmail.com", _dir);
        Assert.Equal("こんにちは", m.Subject);
        Assert.Equal("本文です。", m.TextBody!.TrimEnd());
        Assert.Equal("utf-8", ((TextPart)m.Body!).ContentType.Charset?.ToLowerInvariant());
    }

    [Fact]
    public void Japanese_attachment_name_survives_wire_round_trip()
    {
        // 保存先も日本語を含むフォルダ（_dir）にする
        var rel = Path.Combine("outbox", "1", "0.bin");
        Directory.CreateDirectory(Path.Combine(_dir, "outbox", "1"));
        File.WriteAllBytes(Path.Combine(_dir, rel), new byte[] { 1, 2, 3, 4 });
        var item = Item(b => b.Item = b.Item with
        {
            Attachments = new[] { new AttachmentInfo { FileName = "請求書 令和8年.pdf", ContentType = "application/pdf", LocalPath = rel } },
        });
        var wire = Wire(MessageBuilder.Build(item, "me@gmail.com", _dir));
        Assert.DoesNotContain("請求書", wire.Replace("\r\n", "\n").Split('\n').First(l => l.StartsWith("Subject")) );
        var parsed = MimeMessage.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(wire)));
        var att = Assert.Single(parsed.Attachments.OfType<MimePart>());
        Assert.Equal("請求書 令和8年.pdf", att.FileName);
        Assert.Equal("application/pdf", att.ContentType.MimeType);
        using var ms = new MemoryStream();
        att.Content!.DecodeTo(ms);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, ms.ToArray());
        // 非 ASCII の名前は RFC 2231 か RFC 2047 のいずれかで符号化されていて、生の UTF-8 では出ない
        Assert.DoesNotContain("filename=\"請求書", wire);
    }

    [Fact]
    public void Absolute_attachment_path_is_accepted()
    {
        var abs = Path.Combine(_dir, "添付.txt");
        File.WriteAllText(abs, "abc");
        var item = Item(b => b.Item = b.Item with { Attachments = new[] { new AttachmentInfo { FileName = "添付.txt", LocalPath = abs } } });
        var m = MessageBuilder.Build(item, "me@gmail.com", _dir);
        Assert.Equal("添付.txt", Assert.Single(m.Attachments.OfType<MimePart>()).FileName);
    }

    [Fact]
    public void Missing_attachment_file_throws()
    {
        var item = Item(b => b.Item = b.Item with { Attachments = new[] { new AttachmentInfo { FileName = "x.txt", LocalPath = "outbox\\9\\0.bin" } } });
        Assert.Throws<FileNotFoundException>(() => MessageBuilder.Build(item, "me@gmail.com", _dir));
    }
}
