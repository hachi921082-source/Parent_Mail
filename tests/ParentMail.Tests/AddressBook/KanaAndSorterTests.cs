using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Models;

namespace ParentMail.Tests.AddressBook;

// ふりがなの正規化キーと並び順のテスト。
public class KanaAndSorterTests
{
    private static Contact C(long id, string name, string reading)
        => new(id, "a", name, reading, new[] { $"u{id}@example.com" }, "");

    [Theory]
    [InlineData("ヤマダ", "やまだ")]
    [InlineData("ﾔﾏﾀﾞ", "やまだ")]          // 半角カタカナ（濁点つき）
    [InlineData("やまだ　たろう", "やまだたろう")] // 全角空白は無視
    [InlineData("ＡＢＣ", "abc")]            // 全角英字・大文字小文字
    [InlineData("", "")]
    public void ToHiraganaKey_normalizes(string input, string expected)
        => Assert.Equal(expected, KanaNormalizer.ToHiraganaKey(input));

    [Fact]
    public void ToHiraganaKey_null_is_empty() => Assert.Equal("", KanaNormalizer.ToHiraganaKey(null));

    [Fact]
    public void Sort_mixes_hiragana_and_katakana_readings()
    {
        var list = new[] { C(1, "山田", "ヤマダ"), C(2, "阿部", "あべ"), C(3, "佐藤", "さとう") };
        var sorted = ContactSorter.Sort(list).Select(c => c.Name).ToList();
        Assert.Equal(new[] { "阿部", "佐藤", "山田" }, sorted);
    }

    [Fact]
    public void Sort_puts_empty_reading_last_ordered_by_name()
    {
        var list = new[] { C(1, "ぞう", ""), C(2, "山田", "やまだ"), C(3, "あひる", "  "), C(4, "阿部", "あべ") };
        var sorted = ContactSorter.Sort(list).Select(c => c.Name).ToList();
        Assert.Equal(new[] { "阿部", "山田", "あひる", "ぞう" }, sorted);
    }

    [Fact]
    public void Sort_same_reading_falls_back_to_name_then_id()
    {
        var list = new[] { C(2, "B", "あ"), C(1, "A", "あ"), C(3, "A", "あ") };
        Assert.Equal(new long[] { 1, 3, 2 }, ContactSorter.Sort(list).Select(c => c.Id).ToArray());
    }

    [Theory]
    [InlineData("taro@example.com", true)]
    [InlineData(" taro@example.com ", true)]
    [InlineData("a.b+c@sub.example.co.jp", true)]
    [InlineData("taro", false)]
    [InlineData("taro@", false)]
    [InlineData("@example.com", false)]
    [InlineData("ta ro@example.com", false)]
    [InlineData("taro@example", false)]
    [InlineData("たろう@example.com", false)]
    [InlineData("", false)]
    public void EmailValidator_checks_shape(string input, bool ok)
        => Assert.Equal(ok, ContactEmailValidator.IsValid(input));
}
