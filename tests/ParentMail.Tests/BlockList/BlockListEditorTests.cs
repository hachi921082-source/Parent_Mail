using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.BlockList;

// 入力の正規化・検証、重複の拒否、差出人からの追加。
public class BlockListEditorTests
{
    private const string Acc = "a1";

    [Theory]
    [InlineData(BlockKind.Address, "  Taro@Example.COM ", "taro@example.com")]
    [InlineData(BlockKind.Domain, "Example.com", "example.com")]
    [InlineData(BlockKind.Domain, "@example.com", "example.com")]
    [InlineData(BlockKind.Domain, " @Mail.Example.JP ", "mail.example.jp")]
    public void TryNormalize_accepts_and_lowercases(BlockKind kind, string input, string expected)
    {
        Assert.True(BlockListEditor.TryNormalize(kind, input, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData(BlockKind.Address, "")]
    [InlineData(BlockKind.Address, "   ")]
    [InlineData(BlockKind.Address, "taro")]
    [InlineData(BlockKind.Address, "@example.com")]
    [InlineData(BlockKind.Address, "山田@example.com")]
    [InlineData(BlockKind.Domain, "")]
    [InlineData(BlockKind.Domain, "com")]
    [InlineData(BlockKind.Domain, "a@example.com")]
    [InlineData(BlockKind.Domain, "exa mple.com")]
    [InlineData(BlockKind.Domain, "迷惑.jp")]
    public void TryNormalize_rejects_bad_shapes(BlockKind kind, string input)
        => Assert.False(BlockListEditor.TryNormalize(kind, input, out _));

    [Fact]
    public void Add_stores_normalized_value()
    {
        var repo = new InMemoryBlockRepository();
        var r = new BlockListEditor(repo).Add(Acc, BlockKind.Domain, "@Example.com", out var entry);
        Assert.Equal(BlockAddResult.Added, r);
        Assert.Equal("example.com", entry!.Value);
        Assert.Single(repo.GetAll(Acc));
    }

    [Fact]
    public void Add_rejects_duplicate_ignoring_case_and_at_sign()
    {
        var repo = new InMemoryBlockRepository();
        var ed = new BlockListEditor(repo);
        Assert.Equal(BlockAddResult.Added, ed.Add(Acc, BlockKind.Domain, "example.com", out _));
        Assert.Equal(BlockAddResult.Duplicate, ed.Add(Acc, BlockKind.Domain, "@EXAMPLE.com", out var e));
        Assert.Null(e);
        Assert.Single(repo.GetAll(Acc));
    }

    [Fact]
    public void Same_value_with_different_kind_or_account_is_not_duplicate()
    {
        var repo = new InMemoryBlockRepository();
        var ed = new BlockListEditor(repo);
        Assert.Equal(BlockAddResult.Added, ed.Add(Acc, BlockKind.Address, "a@example.com", out _));
        Assert.Equal(BlockAddResult.Added, ed.Add(Acc, BlockKind.Domain, "example.com", out _));
        Assert.Equal(BlockAddResult.Added, ed.Add("a2", BlockKind.Address, "a@example.com", out _));
    }

    [Fact]
    public void Invalid_input_is_reported_and_not_saved()
    {
        var repo = new InMemoryBlockRepository();
        Assert.Equal(BlockAddResult.Invalid, new BlockListEditor(repo).Add(Acc, BlockKind.Address, "bad", out _));
        Assert.Empty(repo.GetAll(Acc));
    }

    [Fact]
    public void AddFromSender_adds_address_or_whole_domain()
    {
        var repo = new InMemoryBlockRepository();
        var ed = new BlockListEditor(repo);
        Assert.Equal(BlockAddResult.Added, ed.AddFromSender(Acc, "Spam@Ads.Example.com", BlockKind.Address));
        Assert.Equal(BlockAddResult.Added, ed.AddFromSender(Acc, "Spam@Ads.Example.com", BlockKind.Domain));
        Assert.Equal(BlockAddResult.Duplicate, ed.AddFromSender(Acc, "other@ads.example.com", BlockKind.Domain));
        Assert.Equal(BlockAddResult.Invalid, ed.AddFromSender(Acc, "no-at-sign", BlockKind.Domain));
        var all = repo.GetAll(Acc);
        Assert.Equal(new[] { "spam@ads.example.com", "ads.example.com" }, all.Select(x => x.Value).ToArray());
    }
}
