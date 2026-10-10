using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace ParentMail.Core.Features.MailDisplay;

/// <summary>cid 画像 1 件（本文中の cid:xxx の差し替え元）。ContentId は &lt;&gt; を除いた形。</summary>
public sealed record InlineImage(string ContentId, string ContentType, byte[] Data);

/// <summary>
/// WebView2 に渡す前に HTML を安全な形に整える（mail-display PLAN-1）。
/// cid 画像を data URI に置き換え、外部画像を CSP で止め、自動転送(meta refresh)と script・base を取り除く。
/// </summary>
public static class HtmlPreprocessor
{
    private const string CspBlocked = "default-src 'none'; img-src data:; style-src 'unsafe-inline'; font-src data:";
    private const string CspRemoteAllowed = "default-src 'none'; img-src data: https: http:; style-src 'unsafe-inline'; font-src data:";

    private static readonly Regex CidPattern = new(@"cid:([^""'\s)>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 外部の画像・背景を読みにいく指定（src/background の絶対 URL、url(...) の絶対 URL）
    private static readonly Regex RemoteResource = new(
        @"(?:\b(?:src|background|srcset)\s*=\s*[""']?\s*(?:https?:)?//)|(?:url\(\s*[""']?\s*(?:https?:)?//)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>外部の画像などを読みにいく指定が含まれるか（「画像を表示」の案内を出すかの判定）。</summary>
    public static bool HasRemoteImages(string? html)
        => !string.IsNullOrEmpty(html) && RemoteResource.IsMatch(html);

    public static string Prepare(string html, IReadOnlyList<InlineImage> inlineImages, bool allowRemote)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html ?? "");

        RemoveNodes(doc, "//script");
        RemoveNodes(doc, "//base");
        RemoveMetaRefresh(doc);

        var map = BuildMap(inlineImages);
        ReplaceCid(doc, map);
        InjectCsp(doc, allowRemote ? CspRemoteAllowed : CspBlocked);

        return doc.DocumentNode.OuterHtml;
    }

    private static void RemoveNodes(HtmlDocument doc, string xpath)
    {
        var nodes = doc.DocumentNode.SelectNodes(xpath);
        if (nodes == null) return;
        foreach (var n in nodes.ToList()) n.Remove();
    }

    private static void RemoveMetaRefresh(HtmlDocument doc)
    {
        var metas = doc.DocumentNode.SelectNodes("//meta[@http-equiv]");
        if (metas == null) return;
        foreach (var m in metas.ToList())
        {
            if (string.Equals(m.GetAttributeValue("http-equiv", "").Trim(), "refresh", StringComparison.OrdinalIgnoreCase))
                m.Remove();
        }
    }

    private static Dictionary<string, string> BuildMap(IReadOnlyList<InlineImage> images)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var img in images)
        {
            var id = NormalizeCid(img.ContentId);
            if (id.Length == 0 || img.Data.Length == 0) continue;
            // image/* 以外は埋め込まない
            var type = (img.ContentType ?? "").Trim();
            if (!type.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;
            if (type.Any(c => c is ';' or ',' or ' ' or '"' or '\'')) continue;
            map[id] = $"data:{type};base64,{Convert.ToBase64String(img.Data)}";
        }
        return map;
    }

    private static string NormalizeCid(string? cid)
    {
        var s = (cid ?? "").Trim().TrimStart('<').TrimEnd('>').Trim();
        try { return Uri.UnescapeDataString(s); } catch (UriFormatException) { return s; }
    }

    private static string Replace(string value, Dictionary<string, string> map)
        => CidPattern.Replace(value, m => map.TryGetValue(NormalizeCid(m.Groups[1].Value), out var uri) ? uri : m.Value);

    private static void ReplaceCid(HtmlDocument doc, Dictionary<string, string> map)
    {
        if (map.Count == 0) return;
        foreach (var node in doc.DocumentNode.Descendants().Where(n => n.NodeType == HtmlNodeType.Element).ToList())
        {
            foreach (var attr in node.Attributes.ToList())
            {
                var value = attr.Value;
                if (value != null && value.Contains("cid:", StringComparison.OrdinalIgnoreCase))
                    attr.Value = Replace(value, map);
            }
            // <style> の中の url(cid:...)
            if (node.Name == "style" && node.InnerHtml.Contains("cid:", StringComparison.OrdinalIgnoreCase))
                node.InnerHtml = Replace(node.InnerHtml, map);
        }
    }

    private static void InjectCsp(HtmlDocument doc, string csp)
    {
        var meta = doc.CreateElement("meta");
        meta.SetAttributeValue("http-equiv", "Content-Security-Policy");
        meta.SetAttributeValue("content", csp);

        var head = doc.DocumentNode.SelectSingleNode("//head");
        if (head != null)
        {
            // 先頭に置いて、後ろの記述より先に効かせる
            head.PrependChild(meta);
            return;
        }

        head = doc.CreateElement("head");
        head.AppendChild(meta);
        var html = doc.DocumentNode.SelectSingleNode("//html");
        if (html != null) html.PrependChild(head);
        else doc.DocumentNode.PrependChild(head);
    }
}
