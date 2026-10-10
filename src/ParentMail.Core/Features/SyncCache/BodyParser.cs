using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using MimeKit;
using ParentMail.Core.Models;

namespace ParentMail.Core.Features.SyncCache;

/// <summary>本文の解析結果。</summary>
public sealed record ParsedBody(string? Text, string? Html, string? DerivedText, IReadOnlyList<AttachmentInfo> Attachments);

/// <summary>
/// MIME メッセージから本文（text/plain と text/html）と添付情報を取り出す。
/// HTML から作る「派生テキスト」は検索とプレーン表示の代替に使う（スクリプト・スタイルは捨て、ブロック要素は改行にする）。
/// </summary>
public static class BodyParser
{
    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "br", "li", "ul", "ol", "tr", "table", "h1", "h2", "h3", "h4", "h5", "h6",
        "blockquote", "pre", "hr", "section", "article", "header", "footer",
    };

    /// <summary>MIME 全体を解析する。PartId は IMAP の BODYSTRUCTURE と同じ番号（"1", "2.1" ...）で付ける。</summary>
    public static ParsedBody Parse(MimeMessage message)
    {
        string? text = null, html = null;
        var attachments = new List<AttachmentInfo>();
        Walk(message.Body, "", ref text, ref html, attachments);
        text = NormalizeNewlines(text);
        var derived = html is null ? null : HtmlToText(html);
        return new ParsedBody(text, html, derived, attachments);
    }

    private static void Walk(MimeEntity? entity, string prefix, ref string? text, ref string? html,
        List<AttachmentInfo> attachments)
    {
        if (entity is null) return;
        if (entity is Multipart mp)
        {
            // multipart 自体には番号を付けず、子に付ける（トップが単一パートなら "1"）
            for (var i = 0; i < mp.Count; i++)
            {
                var childId = prefix.Length == 0 ? (i + 1).ToString() : $"{prefix}.{i + 1}";
                Walk(mp[i], childId, ref text, ref html, attachments);
            }
            return;
        }

        var partId = prefix.Length == 0 ? "1" : prefix;
        if (entity is TextPart tp && !IsAttachment(tp))
        {
            // 2 つ目以降の本文候補（同じ種類）は捨てる
            if (tp.IsHtml) { html ??= tp.Text; return; }
            if (tp.IsPlain) { text ??= tp.Text; return; }
        }
        {
            var part = entity as MimePart;
            var disposition = entity.ContentDisposition?.Disposition;
            var name = part?.FileName ?? entity.ContentType.Name ?? "";
            attachments.Add(new AttachmentInfo
            {
                PartId = partId,
                FileName = name,
                ContentType = entity.ContentType.MimeType,
                Size = part?.Content?.Stream is { CanSeek: true } s ? s.Length : 0,
                IsInline = string.Equals(disposition, "inline", StringComparison.OrdinalIgnoreCase)
                           || (disposition is null && entity.ContentId is not null),
                ContentId = entity.ContentId,
            });
        }
    }

    /// <summary>改行を LF にそろえる（メールの本文は CRLF で届くため。表示・検索の揺れを避ける）。</summary>
    public static string? NormalizeNewlines(string? s) => s?.Replace("\r\n", "\n").Replace('\r', '\n');

    private static bool IsAttachment(TextPart tp)
        => tp.ContentDisposition is { } d && string.Equals(d.Disposition, "attachment", StringComparison.OrdinalIgnoreCase);

    /// <summary>HTML を読みやすいプレーンテキストにする。</summary>
    public static string HtmlToText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        foreach (var n in doc.DocumentNode.Descendants().Where(n =>
                     n.Name is "script" or "style" or "head" or "title" or "#comment").ToList())
            n.Remove();

        var sb = new StringBuilder();
        Append(doc.DocumentNode, sb);
        var s = HtmlEntity.DeEntitize(sb.ToString()).Replace(' ', ' ');
        s = Regex.Replace(s, @"[ \t\f\v]+", " ");
        s = Regex.Replace(s, @" ?\r?\n ?", "\n");
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        return s.Trim();
    }

    private static void Append(HtmlNode node, StringBuilder sb)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            // HTML の途中の改行・連続空白は 1 つの空白として扱う
            sb.Append(Regex.Replace(node.InnerText, @"\s+", " "));
            return;
        }
        var block = node.NodeType == HtmlNodeType.Element && BlockTags.Contains(node.Name);
        if (block) sb.Append('\n');
        foreach (var child in node.ChildNodes) Append(child, sb);
        if (block) sb.Append('\n');
    }
}
