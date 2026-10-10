using ParentMail.Core;
using ParentMail.Core.Features.Compose;

namespace ParentMail.Tests.Compose;

// 添付サイズの見積り（compose PLAN-1 3 章）。25,000,000 バイトの境目を確かめる。
public class EncodedSizeEstimatorTests
{
    [Fact]
    public void Empty_message_is_header_overhead_only()
        => Assert.Equal(Constants.HeaderOverheadBytes, EncodedSizeEstimator.Estimate(Array.Empty<long>(), 0));

    [Fact]
    public void Eighteen_megabyte_file_is_allowed()
    {
        var estimate = EncodedSizeEstimator.Estimate(new long[] { 18_000_000 }, 1000);
        Assert.True(estimate <= Constants.MaxEncodedMessageBytes, estimate.ToString());
        Assert.True(EncodedSizeEstimator.IsAllowed(estimate));
    }

    [Fact]
    public void Nineteen_megabyte_file_exceeds()
    {
        var estimate = EncodedSizeEstimator.Estimate(new long[] { 19_000_000 }, 1000);
        Assert.True(estimate > Constants.MaxEncodedMessageBytes, estimate.ToString());
        Assert.False(EncodedSizeEstimator.IsAllowed(estimate));
    }

    [Fact]
    public void Boundary_is_inclusive()
    {
        Assert.True(EncodedSizeEstimator.IsAllowed(25_000_000));
        Assert.False(EncodedSizeEstimator.IsAllowed(25_000_001));
    }

    [Fact]
    public void Sums_files_and_counts_body_bytes()
    {
        var one = EncodedSizeEstimator.Estimate(new long[] { 3000 }, 0) - Constants.HeaderOverheadBytes;
        var two = EncodedSizeEstimator.Estimate(new long[] { 3000, 3000 }, 0) - Constants.HeaderOverheadBytes;
        Assert.Equal(2 * one, two);
        Assert.Equal(Constants.HeaderOverheadBytes + 500, EncodedSizeEstimator.Estimate(Array.Empty<long>(), 500));
    }

    [Fact]
    public void Base64_expansion_includes_line_breaks_factor()
    {
        // 3,000,000 バイト -> 4,000,000 文字 -> 改行込みで x78/76
        var estimate = EncodedSizeEstimator.Estimate(new long[] { 3_000_000 }, 0) - Constants.HeaderOverheadBytes;
        Assert.InRange(estimate, 4_105_262, 4_105_264);
    }

    [Theory]
    [InlineData(25_000_000)]
    [InlineData(20_000_000)]
    public void Over_amount_zero_when_allowed(long estimate)
        => Assert.Equal(0.0, EncodedSizeEstimator.OverRawMegabytes(estimate));

    [Fact]
    public void Over_amount_is_encoded_overage_divided_by_1_37_rounded_up_to_0_1()
    {
        // 超過 3,200,000 / 1.37 = 2,335,766 -> 2.4 MB に切り上げ
        Assert.Equal(2.4, EncodedSizeEstimator.OverRawMegabytes(25_000_000 + 3_200_000));
        // 超過 3,150,000 / 1.37 = 2,299,270 -> 2.3 MB に切り上げ
        Assert.Equal(2.3, EncodedSizeEstimator.OverRawMegabytes(25_000_000 + 3_150_000));
    }

    [Fact]
    public void Over_text_replaces_the_sample_amount()
        => Assert.Equal("あと約2.4MB減らしてください", EncodedSizeEstimator.OverText(25_000_000 + 3_200_000, "あと約2.3MB減らしてください"));
}
