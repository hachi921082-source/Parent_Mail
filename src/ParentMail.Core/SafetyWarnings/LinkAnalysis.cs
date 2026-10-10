namespace ParentMail.Core.SafetyWarnings;

/// <summary>あやしいと判断した理由。</summary>
public enum LinkReason { Unparsable, TextMismatch, IpHost, Shortener, InsecureScheme, LookAlike }

/// <summary>あやしいと判断したリンク 1 件。Url は本文中の元の文字列。</summary>
public sealed record LinkFinding(string Url, string? AnchorText, IReadOnlyList<LinkReason> Reasons);

/// <summary>メール 1 通の解析結果。AnySuspicious が true なら赤い帯を出す。</summary>
public sealed class LinkAnalysis
{
    public static readonly LinkAnalysis None = new(Array.Empty<LinkFinding>());

    public LinkAnalysis(IReadOnlyList<LinkFinding> findings) => Findings = findings;

    public IReadOnlyList<LinkFinding> Findings { get; }
    public bool AnySuspicious => Findings.Count > 0;

    public static LinkAnalysis Merge(params LinkAnalysis[] parts)
        => new(parts.SelectMany(p => p.Findings).ToList());
}
