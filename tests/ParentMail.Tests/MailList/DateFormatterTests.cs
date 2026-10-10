using ParentMail.App.Features.MailList;

namespace ParentMail.Tests.MailList;

// 一覧の日時表示（Q44）。端末の時差に依存しないよう、now のオフセットを +09:00 に固定して確かめる。
public sealed class DateFormatterTests
{
    private static readonly TimeSpan Jst = TimeSpan.FromHours(9);
    // 2026-10-10 15:00 (JST)
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, Jst);

    private static DateTimeOffset Utc(int y, int mo, int d, int h, int mi)
        => new(y, mo, d, h, mi, 0, TimeSpan.Zero);

    [Fact]
    public void Today_ShowsTimeOnly()
    {
        // UTC 05:30 = JST 14:30
        Assert.Equal("14:30", DateFormatter.Format(Utc(2026, 10, 10, 5, 30), Now));
    }

    [Fact]
    public void Today_ZeroPadsHour()
    {
        // JST 09:05
        Assert.Equal("09:05", DateFormatter.Format(Utc(2026, 10, 10, 0, 5), Now));
    }

    [Fact]
    public void MidnightEdge_UsesLocalDateNotUtcDate()
    {
        // UTC 10/9 15:00 = JST 10/10 00:00 -> 今日
        Assert.Equal("00:00", DateFormatter.Format(Utc(2026, 10, 9, 15, 0), Now));
        // UTC 10/9 14:59 = JST 10/9 23:59 -> 昨日（今年）
        Assert.Equal("10月9日 23:59", DateFormatter.Format(Utc(2026, 10, 9, 14, 59), Now));
    }

    [Fact]
    public void ThisYear_ShowsMonthDayTime_WithoutZeroPad()
    {
        // JST 2026-01-05 08:03
        Assert.Equal("1月5日 8:03", DateFormatter.Format(Utc(2026, 1, 4, 23, 3), Now));
    }

    [Fact]
    public void OlderYear_ShowsFullDate()
    {
        Assert.Equal("2025年10月8日 14:30", DateFormatter.Format(Utc(2025, 10, 8, 5, 30), Now));
    }

    [Fact]
    public void YearBoundary_LocalNewYearIsThisYear_LocalNewYearsEveIsLastYear()
    {
        var now = new DateTimeOffset(2026, 1, 1, 10, 0, 0, Jst);
        // JST 2026-01-01 00:30 -> 今日
        Assert.Equal("00:30", DateFormatter.Format(Utc(2025, 12, 31, 15, 30), now));
        // JST 2025-12-31 23:30 -> 前の年
        Assert.Equal("2025年12月31日 23:30", DateFormatter.Format(Utc(2025, 12, 31, 14, 30), now));
    }

    [Fact]
    public void DateTimeOverload_TreatsStoredValueAsUtc()
    {
        var local = new DateTime(2026, 10, 10, 15, 0, 0, DateTimeKind.Local);
        var utcSameDay = local.ToUniversalTime().AddMinutes(-30);
        var stored = DateTime.SpecifyKind(utcSameDay, DateTimeKind.Unspecified);
        var expected = DateTime.SpecifyKind(utcSameDay, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm");
        Assert.Equal(expected, DateFormatter.Format(stored, local));
    }
}
