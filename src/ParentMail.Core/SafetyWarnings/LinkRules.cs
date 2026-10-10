namespace ParentMail.Core.SafetyWarnings;

// あやしいリンク判定で使う固定リスト（A29）。
public static class LinkRules
{
    /// <summary>短縮 URL サービスのホスト。完全一致とサブドメインを対象にする。</summary>
    public static readonly IReadOnlyList<string> Shorteners = new[]
    {
        "bit.ly", "t.co", "tinyurl.com", "goo.gl", "ow.ly", "is.gd", "buff.ly", "cutt.ly",
        "rebrand.ly", "t.ly", "lnkd.in", "amzn.to", "ift.tt", "shorturl.at", "rb.gy",
        "bit.do", "j.mp", "x.gd", "tiny.cc", "s.id",
    };
}
