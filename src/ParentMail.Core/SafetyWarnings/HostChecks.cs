using System.Net;
using System.Text.RegularExpressions;

namespace ParentMail.Core.SafetyWarnings;

// ホスト名単体の判定。URL 全体の判断は LinkAnalyzer が行う。
public static class HostChecks
{
    // 10 進・16 進・8 進の整数表記（ドット区切りでも可）。TLD に数字だけのものはない。
    private static readonly Regex NumericLabel = new(@"^(0x[0-9a-f]+|[0-9]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>IPv4・IPv6・整数表記（10/16/8 進）のホストなら true。</summary>
    public static bool IsIpHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        host = host.Trim().TrimEnd('.');
        if (host.StartsWith('[') && host.EndsWith(']')) host = host[1..^1];
        if (host.Contains(':') && IPAddress.TryParse(host, out _)) return true;
        var labels = host.Split('.');
        return labels.Length > 0 && labels.All(l => NumericLabel.IsMatch(l));
    }

    /// <summary>短縮 URL サービスの完全一致またはサブドメインなら true。</summary>
    public static bool IsShortener(string host)
    {
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        foreach (var s in LinkRules.Shorteners)
            if (host == s || host.EndsWith("." + s, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>punycode ラベル、非 ASCII 文字、複数文字体系の混在のいずれかなら true（ブランド類似は見ない）。</summary>
    public static bool IsLookAlike(string host)
    {
        if (string.IsNullOrEmpty(host)) return false;
        foreach (var label in host.Split('.'))
            if (label.StartsWith("xn--", StringComparison.OrdinalIgnoreCase)) return true;
        if (host.Any(c => c > 0x7F)) return true;
        return HasMixedScript(host);
    }

    private enum Script { None, Latin, Cyrillic, Greek, Other }

    private static Script ScriptOf(char c)
    {
        if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z') return Script.Latin;
        if (c < 0x80) return Script.None; // 数字・ハイフンなど
        if (c is >= 'À' and <= 'ɏ' or >= 'Ḁ' and <= 'ỿ') return Script.Latin;
        if (c is >= 'Ѐ' and <= 'ԯ' or >= 'ᲀ' and <= '᲏' or >= 'ⷠ' and <= 'ⷿ' or >= 'Ꙁ' and <= 'ꚟ')
            return Script.Cyrillic;
        if (c is >= 'Ͱ' and <= 'Ͽ' or >= 'ἀ' and <= '῿') return Script.Greek;
        return Script.Other;
    }

    /// <summary>1 つのラベルに、ラテン・キリル・ギリシャなど複数の文字体系が混ざっていれば true。</summary>
    public static bool HasMixedScript(string host)
    {
        foreach (var label in host.Split('.'))
        {
            var seen = new HashSet<Script>();
            foreach (var c in label)
            {
                var s = ScriptOf(c);
                if (s != Script.None) seen.Add(s);
            }
            if (seen.Count > 1) return true;
        }
        return false;
    }
}
