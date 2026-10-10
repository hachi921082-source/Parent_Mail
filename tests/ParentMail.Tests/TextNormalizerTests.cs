using ParentMail.Core;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("ABC", "abc")]
    [InlineData("ＡＢＣ１２３", "abc123")]          // 全角英数 -> 半角小文字
    [InlineData("ﾔﾏﾀﾞ", "ヤマダ")]                 // 半角カナ -> 全角カナ
    [InlineData("山田 太郎", "山田 太郎")]
    [InlineData("", "")]
    public void NormalizeKey_折り返し(string input, string expected)
        => Assert.Equal(expected, TextNormalizer.NormalizeKey(input));

    [Fact]
    public void NormalizeKey_nullは空文字() => Assert.Equal("", TextNormalizer.NormalizeKey(null));

    // フェイクがコンパイルでき、契約どおり動くことの確認
    [Fact]
    public void FakeTextScale_既定18ptでHTML倍率は1_5()
        => Assert.Equal(1.5, new FakeTextScale().GetWebViewZoom(1.0));
}
