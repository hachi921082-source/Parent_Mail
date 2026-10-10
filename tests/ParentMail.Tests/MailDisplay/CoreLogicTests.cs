using ParentMail.Core;
using ParentMail.Core.Features.MailDisplay;
using ParentMail.Core.Services;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.MailDisplay;

public class PreviewZoomTests
{
    [Fact]
    public void 初期値は100パーセントで刻みは25パーセント()
    {
        var z = new PreviewZoom();
        Assert.Equal(100, z.Percent);
        Assert.Equal(1.0, z.Value);
        Assert.True(z.StepUp());
        Assert.Equal(125, z.Percent);
        Assert.Equal(1.25, z.Value);
    }

    [Fact]
    public void 上限300パーセントで止まり縮小は下限で止まる()
    {
        var z = new PreviewZoom();
        for (var i = 0; i < 20; i++) z.StepUp();
        Assert.Equal(300, z.Percent);
        Assert.False(z.CanStepUp);
        Assert.False(z.StepUp());

        for (var i = 0; i < 20; i++) z.StepDown();
        Assert.Equal(100, z.Percent);
        Assert.False(z.CanStepDown);
        Assert.False(z.StepDown());
    }

    [Fact]
    public void リセットで100パーセントに戻る()
    {
        var z = new PreviewZoom();
        z.StepUp();
        z.StepUp();
        Assert.True(z.Reset());
        Assert.Equal(1.0, z.Value);
        Assert.False(z.Reset());
    }
}

public class ZoomMappingTests
{
    private static TextScale Scale(double pt) => new(new FakeSettingsStore { Current = new() { TextSizePt = pt } });

    [Theory]
    [InlineData(18, 1.0, 1.50)]
    [InlineData(18, 3.0, 4.50)]
    [InlineData(48, 3.0, 5.00)]
    [InlineData(12, 1.0, 1.00)]
    public void WebViewのZoomFactorは文字サイズと倍率から決まる(double pt, double zoom, double expected)
        => Assert.Equal(expected, Scale(pt).GetWebViewZoom(zoom));

    [Fact]
    public void プレーン表示の大きさは60ptで頭打ち()
    {
        // 48pt x 3.0 = 144pt -> 60pt -> 80 DIP
        Assert.Equal(80, Scale(48).GetPlainTextSizeDip(3.0), 6);
        Assert.Equal(24, Scale(18).GetPlainTextSizeDip(1.0), 6);
    }
}

public class LinkTextBuilderTests
{
    [Fact]
    public void 日本語の直後に続くURLを切り出す()
    {
        var segs = LinkTextBuilder.ToSegments("詳細はhttps://example.com/path?a=1をご覧ください。");
        Assert.Collection(segs,
            s => { Assert.Equal("詳細は", s.Text); Assert.Null(s.Url); },
            s => { Assert.Equal("https://example.com/path?a=1", s.Text); Assert.Equal("https://example.com/path?a=1", s.Url); },
            s => { Assert.Equal("をご覧ください。", s.Text); Assert.Null(s.Url); });
    }

    [Fact]
    public void wwwはhttpsを補い末尾の句読点は含めない()
    {
        var segs = LinkTextBuilder.ToSegments("see www.example.jp/abc.");
        Assert.Equal("www.example.jp/abc", segs[1].Text);
        Assert.Equal("https://www.example.jp/abc", segs[1].Url);
        Assert.Equal(".", segs[2].Text);
    }

    [Fact]
    public void mailtoもリンクにする()
    {
        var segs = LinkTextBuilder.ToSegments("連絡先: mailto:taro@example.com です");
        Assert.Contains(segs, s => s.Url == "mailto:taro@example.com");
    }

    [Fact]
    public void URLがなければ全体が1つの文字列()
    {
        var segs = LinkTextBuilder.ToSegments("こんにちは。\n元気ですか");
        var only = Assert.Single(segs);
        Assert.Null(only.Url);
        Assert.Equal("こんにちは。\n元気ですか", only.Text);
        Assert.Empty(LinkTextBuilder.ToSegments(null));
    }

    [Fact]
    public void 接頭辞だけの文字列はリンクにしない()
    {
        var only = Assert.Single(LinkTextBuilder.ToSegments("https:// だけ"));
        Assert.Null(only.Url);
    }
}

public class HtmlPreprocessorTests
{
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };

    private static IReadOnlyList<InlineImage> Images(string cid = "logo@x") => new[] { new InlineImage(cid, "image/png", Png) };

    [Fact]
    public void cidをdataURIに置き換える()
    {
        var html = "<html><head></head><body><img src=\"cid:logo@x\"><p>本文</p></body></html>";
        var result = HtmlPreprocessor.Prepare(html, Images("<logo@x>"), allowRemote: false);
        Assert.Contains("data:image/png;base64," + Convert.ToBase64String(Png), result);
        Assert.DoesNotContain("cid:logo@x", result);
        Assert.Contains("本文", result);
    }

    [Fact]
    public void 一致しないcidと画像以外はそのまま()
    {
        var images = new[] { new InlineImage("a", "text/html", new byte[] { 1 }) };
        var result = HtmlPreprocessor.Prepare("<img src=\"cid:a\"><img src=\"cid:none\">", images, false);
        Assert.Contains("cid:a", result);
        Assert.Contains("cid:none", result);
    }

    [Fact]
    public void 外部画像は既定で止めるCSPを入れる()
    {
        var result = HtmlPreprocessor.Prepare("<html><head><title>t</title></head><body><img src=\"https://a.example/x.png\"></body></html>", Images(), false);
        Assert.Contains("Content-Security-Policy", result);
        Assert.Contains("img-src data:;", result);
        Assert.DoesNotContain("img-src data: https: http:", result);
    }

    [Fact]
    public void 画像を表示するとCSPが外部画像を許す()
    {
        var result = HtmlPreprocessor.Prepare("<p>x</p>", Images(), true);
        Assert.Contains("img-src data: https: http:", result);
        Assert.Contains("default-src 'none'", result);
    }

    [Fact]
    public void head無しの断片にもCSPを入れる()
    {
        var result = HtmlPreprocessor.Prepare("日本語だけの断片", Array.Empty<InlineImage>(), false);
        Assert.Contains("Content-Security-Policy", result);
        Assert.Contains("日本語だけの断片", result);
    }

    [Fact]
    public void metaRefreshとscriptとbaseを取り除く()
    {
        var html = "<html><head><meta http-equiv=\"refresh\" content=\"0;url=https://evil.example\"><base href=\"https://evil.example/\">" +
                   "<script>alert(1)</script></head><body>ok</body></html>";
        var result = HtmlPreprocessor.Prepare(html, Array.Empty<InlineImage>(), false);
        Assert.DoesNotContain("refresh", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<base", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert(1)", result);
        Assert.Contains("ok", result);
    }

    [Theory]
    [InlineData("<img src=\"https://a.example/x.png\">", true)]
    [InlineData("<td background='http://a.example/b.gif'>", true)]
    [InlineData("<div style=\"background:url(https://a.example/b.png)\">", true)]
    [InlineData("<img src=\"cid:logo\">", false)]
    [InlineData("<a href=\"https://a.example\">link</a>", false)]
    [InlineData("", false)]
    public void 外部画像の有無を見分ける(string html, bool expected)
        => Assert.Equal(expected, HtmlPreprocessor.HasRemoteImages(html));
}

public class PreviewHeaderFormatterTests
{
    [Fact]
    public void 差出人は名前とアドレスを並べる()
    {
        Assert.Equal("山田 太郎 <taro@example.com>", PreviewHeaderFormatter.FormatFrom("山田 太郎", "taro@example.com"));
        Assert.Equal("taro@example.com", PreviewHeaderFormatter.FormatFrom("", "taro@example.com"));
        Assert.Equal("taro@example.com", PreviewHeaderFormatter.FormatFrom("taro@example.com", "taro@example.com"));
    }

    [Fact]
    public void 宛先は読点でつなぐ()
        => Assert.Equal("a@x.jp、b@y.jp", PreviewHeaderFormatter.FormatList(new[] { "a@x.jp", " ", "b@y.jp" }));

    [Fact]
    public void 日時はローカル時刻で表示する()
    {
        var utc = new DateTime(2026, 10, 8, 0, 5, 0, DateTimeKind.Utc);
        var expected = utc.ToLocalTime();
        Assert.Equal($"{expected.Year}年{expected.Month}月{expected.Day}日 {expected.Hour}:{expected.Minute:00}", PreviewHeaderFormatter.FormatDate(utc));
        Assert.Equal("", PreviewHeaderFormatter.FormatDate(default));
    }
}

public class PreviewSelectionHintsTests
{
    [Fact]
    public void 自動選択はスコープの間だけ有効()
    {
        var h = new PreviewSelectionHints();
        Assert.False(h.IsAutoSelection);
        using (h.BeginAutoSelection()) Assert.True(h.IsAutoSelection);
        Assert.False(h.IsAutoSelection);
    }

    [Fact]
    public void チェック数の変化だけイベントを出す()
    {
        var h = new PreviewSelectionHints();
        var count = 0;
        h.CheckedCountChanged += (_, _) => count++;
        h.CheckedCount = 2;
        h.CheckedCount = 2;
        h.CheckedCount = -5;
        Assert.Equal(0, h.CheckedCount);
        Assert.Equal(2, count);
    }
}
