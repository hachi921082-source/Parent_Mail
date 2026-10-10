using ParentMail.Core.Models;

namespace ParentMail.Core.Features.BlockList;

// 差出人アドレスが受信拒否の項目に当たるかの判定。大文字小文字・全角半角は区別しない。
public static class BlockMatcher
{
    public static bool Matches(IEnumerable<BlockEntry> entries, string? fromAddress)
    {
        var address = TextNormalizer.NormalizeKey(fromAddress?.Trim());
        var domain = DomainOf(address);
        if (address.Length == 0) return false;

        foreach (var e in entries)
        {
            var value = TextNormalizer.NormalizeKey(e.Value.Trim().TrimStart('@'));
            if (value.Length == 0) continue;
            if (e.Kind == BlockKind.Address)
            {
                if (address == TextNormalizer.NormalizeKey(e.Value.Trim())) return true;
            }
            else if (domain is not null)
            {
                // 同じドメイン、またはその下位ドメイン。notexample.com は example.com に当たらない
                if (domain == value || domain.EndsWith("." + value, StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }

    /// <summary>アドレスの @ より後ろ。@ がなければ null。</summary>
    public static string? DomainOf(string normalizedAddress)
    {
        var at = normalizedAddress.LastIndexOf('@');
        if (at <= 0 || at == normalizedAddress.Length - 1) return null;
        return normalizedAddress[(at + 1)..].TrimEnd('.');
    }
}
