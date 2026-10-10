using MimeKit;
using MimeKit.Utils;
using ParentMail.Core.Models;

namespace ParentMail.Core.Features.OutboxSend;

/// <summary>
/// 送信箱の 1 件から MimeMessage を組み立てる。ネットワークには触れない（送信は ISmtpGateway が行う）。
/// From は表示名なしのアドレスだけ。Bcc は封筒用に Bcc に入れる（SMTP 送信時に MailKit がヘッダから外す）。
/// </summary>
public static class MessageBuilder
{
    /// <param name="accountDir">添付の LocalPath が相対パスのときの基準（アカウントフォルダ）。</param>
    /// <param name="now">Date に入れる時刻。省略時はいま。</param>
    public static MimeMessage Build(OutboxItem item, string accountEmail, string accountDir, DateTimeOffset? now = null)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress((string?)null, accountEmail));
        AddAll(message.To, item.To);
        AddAll(message.Cc, item.Cc);
        AddAll(message.Bcc, item.Bcc);
        message.Subject = item.Subject;
        message.Date = now ?? DateTimeOffset.Now;
        var at = accountEmail.LastIndexOf('@');
        message.MessageId = MimeUtils.GenerateMessageId(at >= 0 ? accountEmail[(at + 1)..] : "localhost");

        if (item.ReplyHeaders is { } rh)
        {
            var inReplyTo = Bracket(rh.InReplyTo);
            if (inReplyTo != null) message.Headers.Replace(HeaderId.InReplyTo, inReplyTo);
            var refs = (rh.References ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Bracket).Where(x => x != null).ToList();
            if (refs.Count > 0) message.Headers.Replace(HeaderId.References, string.Join(" ", refs));
        }

        var text = new TextPart("plain") { Text = item.Body ?? "" };
        text.ContentType.Charset = "utf-8";
        if (item.Attachments.Count == 0)
        {
            message.Body = text;
            return message;
        }

        var multipart = new Multipart("mixed") { text };
        foreach (var a in item.Attachments)
        {
            var path = ResolvePath(a, accountDir);
            if (!File.Exists(path)) throw new FileNotFoundException("添付ファイルが見つからない");
            var contentType = ContentType.TryParse(a.ContentType ?? "", out var ct) && !string.IsNullOrEmpty(a.ContentType)
                ? ct
                : ContentType.Parse(MimeTypes.GetMimeType(a.FileName));
            var part = new MimePart(contentType)
            {
                Content = new MimeContent(new MemoryStream(File.ReadAllBytes(path))),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
            };
            // 日本語のファイル名は RFC 2231 で符号化する
            part.ContentDisposition.Parameters.Add(System.Text.Encoding.UTF8, "filename", a.FileName);
            part.ContentType.Parameters.Add(System.Text.Encoding.UTF8, "name", a.FileName);
            multipart.Add(part);
        }
        message.Body = multipart;
        return message;
    }

    private static string ResolvePath(AttachmentInfo a, string accountDir)
    {
        if (string.IsNullOrEmpty(a.LocalPath)) throw new FileNotFoundException("添付ファイルの場所が未設定");
        return Path.IsPathRooted(a.LocalPath) ? a.LocalPath : Path.Combine(accountDir, a.LocalPath);
    }

    private static void AddAll(InternetAddressList list, IReadOnlyList<string> addresses)
    {
        foreach (var s in addresses)
            if (!string.IsNullOrWhiteSpace(s)) list.Add(MailboxAddress.Parse(s.Trim()));
    }

    /// <summary>Message-ID を &lt;...&gt; 形式にそろえる。空なら null。</summary>
    private static string? Bracket(string? id)
    {
        var t = id?.Trim().Trim('<', '>').Trim();
        return string.IsNullOrEmpty(t) ? null : "<" + t + ">";
    }
}
