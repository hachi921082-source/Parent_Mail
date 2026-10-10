using ParentMail.App.Features.MailList;
using ParentMail.App.Resources;

namespace ParentMail.Tests.MailList;

// 送信済み・下書き・送信箱の行の宛先要約（A67）。
public sealed class RecipientSummaryTests
{
    [Fact]
    public void OneRecipient_ShowsAddress()
    {
        Assert.Equal("宛先: haha@example.com", RecipientSummary.Format(new[] { "haha@example.com" }));
    }

    [Fact]
    public void ManyRecipients_ShowsFirstAndRemainingCount()
    {
        var text = RecipientSummary.Format(new[] { "山田 太郎 <taro@example.com>", "b@example.com", "c@example.com" });
        Assert.Equal("宛先: 山田 太郎 <taro@example.com> ほか 2 人", text);
    }

    [Fact]
    public void TwoRecipients_SaysOneMore()
    {
        Assert.Equal("宛先: a@example.com ほか 1 人", RecipientSummary.Format(new[] { "a@example.com", "b@example.com" }));
    }

    [Fact]
    public void BlankEntries_AreIgnored()
    {
        Assert.Equal("宛先: a@example.com", RecipientSummary.Format(new[] { " ", "a@example.com", "" }));
    }

    [Fact]
    public void NoRecipient_FallsBackToPlaceholder()
    {
        Assert.Equal(Strings.ML_12, RecipientSummary.Format(Array.Empty<string>()));
        Assert.Equal(Strings.ML_12, RecipientSummary.Format(null));
    }
}
