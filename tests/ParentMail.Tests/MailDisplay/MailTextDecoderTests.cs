using MimeKit;
using ParentMail.App.Features.MailList;
using ParentMail.Core.Features.MailDisplay;
using ParentMail.Core.Features.SyncCache;

namespace ParentMail.Tests.MailDisplay;

// Issue #7: 宛先の RFC 2047 符号化語（=?UTF-8?B?...?=）を表示用に復号する。
public sealed class MailTextDecoderTests
{
    private const string Encoded = "=?UTF-8?B?44OG44K544OI?="; // テスト

    [Fact]
    public void EncodedWord_IsDecodedToJapanese()
        => Assert.Equal("テスト", MailTextDecoder.ForDisplay(Encoded));

    [Fact]
    public void EncodedNameWithAddress_KeepsAddressPart()
        => Assert.Equal("テスト <a@example.com>", MailTextDecoder.ForDisplay(Encoded + " <a@example.com>"));

    [Theory]
    [InlineData("haha@example.com")]
    [InlineData("山田 太郎 <taro@example.com>")]
    [InlineData("")]
    public void PlainText_IsUnchanged(string text)
        => Assert.Equal(text, MailTextDecoder.ForDisplay(text));

    [Fact]
    public void Undecodable_IsUnchanged()
        => Assert.Equal("=?UTF-8?Q?", MailTextDecoder.ForDisplay("=?UTF-8?Q?"));

    [Fact]
    public void Null_ReturnsEmpty()
        => Assert.Equal("", MailTextDecoder.ForDisplay(null));

    [Fact]
    public void RecipientSummary_DecodesEncodedWord()
        => Assert.Equal("宛先: テスト", RecipientSummary.Format(new[] { Encoded }));

    [Fact]
    public void RecipientSummary_MixedList_DecodesOnlyEncoded()
        => Assert.Equal("宛先: テスト ほか 2 人", RecipientSummary.Format(new[] { Encoded, "b@example.com", "山田 <c@example.com>" }));

    [Fact]
    public void PreviewHeaderFormatter_DecodesList()
        => Assert.Equal("テスト、b@example.com", PreviewHeaderFormatter.FormatList(new[] { Encoded, "b@example.com" }));

    [Fact]
    public void PreviewHeaderFormatter_DecodesFrom()
        => Assert.Equal("テスト <a@example.com>", PreviewHeaderFormatter.FormatFrom(Encoded, "a@example.com"));

    // ゲートウェイ: 封筒の宛先がアドレスでない符号化文字列でも、キャッシュには復号済みで入る
    [Fact]
    public void EnvelopeMailbox_NonAddressEncoded_StoredDecoded()
        => Assert.Equal("テスト", ImapAddressText.Of(new MailboxAddress(null, Encoded)));

    [Fact]
    public void EnvelopeMailbox_RealAddress_StoredAsIs()
        => Assert.Equal("a@example.com", ImapAddressText.Of(new MailboxAddress("名前", "a@example.com")));

    [Fact]
    public void EnvelopeMailbox_EncodedWithDomainLike_StoredAsIs()
        => Assert.Equal("a@example.com", ImapAddressText.Of(new MailboxAddress(null, "a@example.com")));
}
