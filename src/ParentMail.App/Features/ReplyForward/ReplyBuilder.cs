using ParentMail.Core;
using ParentMail.Core.Features.Compose;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.ReplyForward;

/// <summary>
/// 返信・全員に返信の初期値づくり（reply-forward PLAN 2 章）。窓には依存しない。
/// 元の本文が取得済みであることは呼ぶ側で確かめる。
/// </summary>
public static class ReplyBuilder
{
    /// <param name="replyTo">Reply-To ヘッダの値（保存していないので今は呼び出し側が渡せたときだけ）。空なら差出人へ返す。</param>
    public static ComposeInit BuildReply(string accountId, MailMessage source, string accountEmail, bool replyAll, string? replyTo = null)
    {
        var to = !string.IsNullOrWhiteSpace(replyTo) ? replyTo.Trim() : RecipientParser.Format(source.FromName, source.FromAddress);
        var toAddresses = new List<string> { to };

        var cc = new List<string>();
        if (replyAll)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (AddressOf(accountEmail) is { } own) seen.Add(own);
            foreach (var t in toAddresses)
            {
                if (AddressOf(t) is { } a) seen.Add(a);
            }
            foreach (var entry in source.To.Concat(source.Cc))
            {
                var address = AddressOf(entry);
                if (address is null || !seen.Add(address)) continue;
                cc.Add(entry.Trim());
            }
        }

        var text = !string.IsNullOrEmpty(source.BodyText) ? source.BodyText : source.DerivedText ?? "";
        // 先頭に空行を 2 つ（カーソルは 1 行目）。その下に引用を置く
        var nl = QuoteFormatter.NewLine;
        var body = nl + nl + ReplyForwardText.QuoteHeader(source) + nl + QuoteFormatter.Quote(text);

        var references = string.Join(" ", new[] { source.References, source.MessageIdHeader }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        return new ComposeInit
        {
            AccountId = accountId,
            To = toAddresses,
            Cc = cc,
            Subject = SubjectPrefixer.Add(PrefixKind.Reply, source.Subject),
            Body = body,
            ReplyHeaders = new ReplyHeaders(
                string.IsNullOrWhiteSpace(source.MessageIdHeader) ? null : source.MessageIdHeader.Trim(),
                references.Length == 0 ? null : references),
        };
    }

    /// <summary>「名前 &lt;アドレス&gt;」でも素のアドレスでも、比べるためのアドレス部分（無ければ null）。</summary>
    internal static string? AddressOf(string? entry)
    {
        var tokens = RecipientParser.Parse(entry);
        var first = tokens.FirstOrDefault(t => t.Address is not null);
        return first?.Address;
    }
}
