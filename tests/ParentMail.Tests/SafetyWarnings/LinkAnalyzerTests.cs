using ParentMail.Core.SafetyWarnings;

namespace ParentMail.Tests.SafetyWarnings;

public class LinkAnalyzerTests
{
    private static bool Html(string href, string text)
        => LinkAnalyzer.AnalyzeHtml($"<a href=\"{href}\">{text}</a>").AnySuspicious;

    [Fact]
    public void 普通のhttpsリンクは安全()
    {
        Assert.False(Html("https://www.example.com/a?b=1", "詳しくはこちら"));
        Assert.False(Html("https://example.com/", "example.com"));
        Assert.False(Html("https://example.com/", "https://www.EXAMPLE.com/news"));
    }

    [Fact]
    public void 表示テキストとリンク先のホストが違えばあやしい()
    {
        Assert.True(Html("https://evil.example.net/", "https://www.example.com/"));
        Assert.True(Html("https://evil.example.net/", "www.example.com"));
        Assert.True(Html("https://evil.example.net/", "example.com"));
    }

    [Fact]
    public void 文章のアンカーは不一致判定の対象外()
    {
        Assert.False(Html("https://example.com/", "こちらをクリック"));
        Assert.False(Html("https://example.com/", "Click here to continue."));
    }

    [Theory]
    [InlineData("https://192.168.0.1/login")]
    [InlineData("https://[::1]/")]
    [InlineData("https://[2001:db8::1]/")]
    [InlineData("https://2130706433/")]
    [InlineData("https://0x7f000001/")]
    [InlineData("https://0x7f.0.0.1/")]
    [InlineData("https://017700000001/")]
    [InlineData("https://0177.0.0.1/")]
    public void IPアドレスの各表記はあやしい(string url)
    {
        Assert.True(Html(url, "ログイン"));
    }

    [Theory]
    [InlineData("https://bit.ly/abc")]
    [InlineData("https://BIT.LY/abc")]
    [InlineData("https://www.bit.ly/abc")]
    [InlineData("https://foo.t.co/x")]
    [InlineData("https://s.id/x")]
    public void 短縮URLの完全一致とサブドメインはあやしい(string url)
    {
        Assert.True(Html(url, "ここ"));
    }

    [Theory]
    [InlineData("https://notbit.ly.example.com/")]
    [InlineData("https://abit.ly.jp/")]
    [InlineData("https://cat.co/")]
    public void 短縮URLに似ているだけのホストは安全(string url)
    {
        Assert.False(Html(url, "ここ"));
    }

    [Theory]
    [InlineData("http://example.com/")]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    public void https以外はあやしい(string url)
    {
        Assert.True(Html(url, "ここ"));
    }

    [Fact]
    public void mailtoとtelは対象外()
    {
        Assert.False(Html("mailto:taro@example.com", "taro@example.com"));
        Assert.False(Html("tel:0312345678", "電話"));
    }

    [Fact]
    public void punycodeはあやしい()
    {
        Assert.True(Html("https://xn--pple-43d.com/", "ここ"));
    }

    [Fact]
    public void キリル文字が混ざったホストはあやしい()
    {
        // 先頭の a がキリル文字（U+0430）
        Assert.True(Html("https://аpple.com/", "ここ"));
        Assert.True(HostChecks.HasMixedScript("аpple.com"));
        Assert.False(HostChecks.HasMixedScript("apple.com"));
        Assert.False(HostChecks.HasMixedScript("абв.com"));
    }

    [Fact]
    public void 日本語ホストは非ASCIIなのであやしい()
    {
        Assert.True(Html("https://日本語.jp/", "ここ"));
    }

    [Theory]
    [InlineData("https://")]
    [InlineData("http://[bad")]
    [InlineData("not a url")]
    public void 解析できないURLはあやしい(string href)
    {
        var a = LinkAnalyzer.AnalyzeHtml($"<a href=\"{href}\">x</a>");
        Assert.True(a.AnySuspicious);
        Assert.Contains(LinkReason.Unparsable, a.Findings[0].Reasons);
    }

    [Fact]
    public void ページ内リンクと空のhrefは無視する()
    {
        Assert.False(LinkAnalyzer.AnalyzeHtml("<a href=\"#top\">上へ</a><a href=\"\">x</a><a>y</a>").AnySuspicious);
    }

    [Fact]
    public void エンティティ入りのhrefも展開して判定する()
    {
        Assert.True(Html("http&#58;//example.com/", "ここ"));
    }

    [Fact]
    public void テキストの中のURLを判定する()
    {
        Assert.False(LinkAnalyzer.AnalyzeText("詳細は https://example.com/a をご覧ください。").AnySuspicious);
        Assert.True(LinkAnalyzer.AnalyzeText("こちら http://example.com/a を開く").AnySuspicious);
        Assert.True(LinkAnalyzer.AnalyzeText("https://bit.ly/xyz").AnySuspicious);
        Assert.True(LinkAnalyzer.AnalyzeText("https://10.0.0.1/").AnySuspicious);
        Assert.False(LinkAnalyzer.AnalyzeText("www.example.com を見る").AnySuspicious);
    }

    [Fact]
    public void テキストのURL直後の全角句読点は含めない()
    {
        Assert.False(LinkAnalyzer.AnalyzeText("https://example.com/a。次の文").AnySuspicious);
        Assert.False(LinkAnalyzer.AnalyzeText("（https://example.com/a）").AnySuspicious);
    }

    [Fact]
    public void テキストのキリル混在もあやしい()
    {
        Assert.True(LinkAnalyzer.AnalyzeText("https://exаmple.com/ へ").AnySuspicious);
    }

    [Fact]
    public void HTMLとテキストをまとめて判定する()
    {
        var a = LinkAnalyzer.Analyze("<a href=\"https://example.com/\">こちら</a>", "http://example.com/");
        Assert.True(a.AnySuspicious);
        Assert.Single(a.Findings);
        Assert.False(LinkAnalyzer.Analyze(null, null).AnySuspicious);
    }

    [Fact]
    public void IsSuspiciousは表示テキストを渡せる()
    {
        var uri = new Uri("https://evil.example.net/");
        Assert.True(LinkAnalyzer.IsSuspicious(uri, "www.example.com"));
        Assert.False(LinkAnalyzer.IsSuspicious(uri, null));
    }
}
