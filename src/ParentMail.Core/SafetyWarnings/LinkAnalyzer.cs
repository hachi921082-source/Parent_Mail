using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace ParentMail.Core.SafetyWarnings;

// リンクのあやしさ判定（safety-warnings PLAN-1 2 章）。UI に依存しない。
public static class LinkAnalyzer
{
    private static readonly Regex TextUrl = new(@"(?:[a-z][a-z0-9+.\-]*://|www\.)[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HostLike = new(@"^(?:[a-z][a-z0-9+.\-]*://)?(?:[^\s/@]+@)?(?:[^\s./:]+\.)+[^\s./:]+(?::\d+)?(?:[/?#]\S*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>HTML 部分の &lt;a href&gt; をすべて判定する。</summary>
    public static LinkAnalysis AnalyzeHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return LinkAnalysis.None;
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchors == null) return LinkAnalysis.None;

        var findings = new List<LinkFinding>();
        foreach (var a in anchors)
        {
            var href = HtmlEntity.DeEntitize(a.GetAttributeValue("href", "")).Trim();
            // ページ内移動だけのリンクは対象外（外へ出ない）。
            if (href.Length == 0 || href.StartsWith('#')) continue;
            var text = Regex.Replace(HtmlEntity.DeEntitize(a.InnerText), @"\s+", " ").Trim();
            Evaluate(href, text.Length == 0 ? null : text, findings);
        }
        return new LinkAnalysis(findings);
    }

    /// <summary>テキスト部分の URL を判定する。アンカーがないので表示との不一致は見ない（規則 2〜5）。</summary>
    public static LinkAnalysis AnalyzeText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return LinkAnalysis.None;
        var findings = new List<LinkFinding>();
        foreach (Match m in TextUrl.Matches(text))
        {
            var url = CutAtFullWidth(m.Value).TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '\'', '"');
            if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
            Evaluate(url, null, findings);
        }
        return new LinkAnalysis(findings);
    }

    /// <summary>HTML 部分とテキスト部分をまとめて判定する。表示モードには依存しない。</summary>
    public static LinkAnalysis Analyze(string? html, string? text)
        => LinkAnalysis.Merge(AnalyzeHtml(html), AnalyzeText(text));

    /// <summary>1 件のリンクがあやしければ true。mailto と tel は対象外。</summary>
    public static bool IsSuspicious(Uri target, string? anchorText) => GetReasons(target, anchorText).Count > 0;

    public static IReadOnlyList<LinkReason> GetReasons(Uri target, string? anchorText)
    {
        var reasons = new List<LinkReason>();
        if (target.Scheme is "mailto" or "tel") return reasons;
        if (target.Scheme != Uri.UriSchemeHttps) reasons.Add(LinkReason.InsecureScheme);

        var host = target.IdnHost; // punycode 化されたホスト
        if (HostChecks.IsIpHost(host) || target.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
            reasons.Add(LinkReason.IpHost);
        if (HostChecks.IsShortener(host)) reasons.Add(LinkReason.Shortener);
        if (HostChecks.IsLookAlike(host) || HostChecks.IsLookAlike(target.Host)) reasons.Add(LinkReason.LookAlike);
        if (HasTextMismatch(target, anchorText)) reasons.Add(LinkReason.TextMismatch);
        return reasons;
    }

    private static void Evaluate(string url, string? anchorText, List<LinkFinding> findings)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            findings.Add(new LinkFinding(url, anchorText, new[] { LinkReason.Unparsable }));
            return;
        }
        var reasons = GetReasons(uri, anchorText);
        if (reasons.Count > 0) findings.Add(new LinkFinding(url, anchorText, reasons));
    }

    private static bool HasTextMismatch(Uri target, string? anchorText)
    {
        if (string.IsNullOrWhiteSpace(anchorText)) return false;
        var text = anchorText.Trim();
        var looksLikeUrl = text.Contains("://") || text.Contains("www.", StringComparison.OrdinalIgnoreCase) || HostLike.IsMatch(text);
        if (!looksLikeUrl) return false;

        var textHost = ExtractHost(text);
        if (textHost == null) return true; // URL らしいのにホストが読めない
        return StripWww(textHost) != StripWww(target.IdnHost.ToLowerInvariant());
    }

    private static string? ExtractHost(string text)
    {
        var candidate = text;
        if (!candidate.Contains("://")) candidate = "https://" + candidate;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) return null;
        return string.IsNullOrEmpty(uri.IdnHost) ? null : uri.IdnHost.ToLowerInvariant();
    }

    private static string StripWww(string host) => host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;

    // 日本語の文中で URL の直後に全角の句読点や括弧が続く場合に、そこで切る。
    private static string CutAtFullWidth(string s)
    {
        for (var i = 0; i < s.Length; i++)
            if (s[i] is >= '　' and <= '〿' or >= '＀' and <= '￯') return s[..i];
        return s;
    }
}
