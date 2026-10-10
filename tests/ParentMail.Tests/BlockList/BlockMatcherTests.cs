using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Models;

namespace ParentMail.Tests.BlockList;

// 受信拒否の判定（アドレス、ドメイン、下位ドメイン、大文字小文字）。
public class BlockMatcherTests
{
    private static BlockEntry Addr(string v) => new(1, "a", BlockKind.Address, v);
    private static BlockEntry Dom(string v) => new(2, "a", BlockKind.Domain, v);

    [Fact]
    public void Address_matches_exactly_and_ignores_case()
    {
        var e = new[] { Addr("taro@example.com") };
        Assert.True(BlockMatcher.Matches(e, "taro@example.com"));
        Assert.True(BlockMatcher.Matches(e, "TARO@Example.COM"));
        Assert.False(BlockMatcher.Matches(e, "jiro@example.com"));
        Assert.False(BlockMatcher.Matches(e, "taro@mail.example.com"));
    }

    [Fact]
    public void Domain_matches_domain_and_subdomains()
    {
        var e = new[] { Dom("example.com") };
        Assert.True(BlockMatcher.Matches(e, "a@example.com"));
        Assert.True(BlockMatcher.Matches(e, "a@mail.example.com"));
        Assert.True(BlockMatcher.Matches(e, "a@x.y.EXAMPLE.com"));
    }

    [Fact]
    public void Domain_does_not_match_look_alike_suffix()
    {
        var e = new[] { Dom("example.com") };
        Assert.False(BlockMatcher.Matches(e, "a@notexample.com"));
        Assert.False(BlockMatcher.Matches(e, "a@example.com.evil.jp"));
        Assert.False(BlockMatcher.Matches(e, "a@example.org"));
    }

    [Fact]
    public void Domain_entry_stored_with_at_or_uppercase_still_matches()
    {
        Assert.True(BlockMatcher.Matches(new[] { Dom("@Example.com") }, "a@example.com"));
    }

    [Fact]
    public void Empty_or_malformed_sender_never_matches()
    {
        var e = new[] { Addr("taro@example.com"), Dom("example.com") };
        Assert.False(BlockMatcher.Matches(e, ""));
        Assert.False(BlockMatcher.Matches(e, null));
        Assert.False(BlockMatcher.Matches(e, "example.com"));
        Assert.False(BlockMatcher.Matches(e, "山田太郎"));
    }

    [Fact]
    public void No_entries_never_matches()
        => Assert.False(BlockMatcher.Matches(Array.Empty<BlockEntry>(), "a@example.com"));

    [Fact]
    public void Full_width_input_is_folded()
    {
        Assert.True(BlockMatcher.Matches(new[] { Dom("example.com") }, "ａ＠ｅｘａｍｐｌｅ．ｃｏｍ"));
    }
}
