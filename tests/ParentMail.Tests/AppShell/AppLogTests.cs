using ParentMail.Core.Platform;

namespace ParentMail.Tests.AppShell;

public class AppLogTests : IDisposable
{
    // 日本語を含むフォルダでもログが書けることを確かめるため、あえて日本語名にする。
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ParentMailテスト_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private AppLog Create(long maxBytes = AppLog.DefaultMaxBytes)
        => new(new AppPaths(_root), maxBytes, () => new DateTime(2026, 10, 10, 9, 8, 7, 123));

    [Fact]
    public void 一行の形式は日時_レベル_領域_本文()
    {
        Create().Info("Sync", "done");

        var line = File.ReadAllLines(new AppPaths(_root).LogFile).Single();
        Assert.Equal("2026-10-10 09:08:07.123 [INFO] Sync: done", line);
    }

    [Fact]
    public void 上限を超えると回転して三ファイルまで残る()
    {
        var paths = new AppPaths(_root);
        var log = Create(maxBytes: 200);
        for (var i = 0; i < 40; i++) log.Info("Test", new string('x', 50) + i);

        Assert.True(File.Exists(paths.LogFile));
        Assert.True(File.Exists(Path.Combine(paths.LogsDir, "parentmail.1.log")));
        Assert.True(File.Exists(Path.Combine(paths.LogsDir, "parentmail.2.log")));
        Assert.False(File.Exists(Path.Combine(paths.LogsDir, "parentmail.3.log")));
        Assert.Equal(3, Directory.GetFiles(paths.LogsDir).Length);
    }

    [Fact]
    public void 例外は種類とメッセージとスタックだけを一行で書く()
    {
        Exception ex;
        try { throw new InvalidOperationException("boom"); } catch (Exception e) { ex = e; }

        Create().Error("Area", "failed", ex);

        var text = File.ReadAllText(new AppPaths(_root).LogFile);
        Assert.Single(text.TrimEnd().Split('\n'));
        Assert.Contains("System.InvalidOperationException: boom", text);
    }

    [Fact]
    public void アドレスとURLはログに残らない()
    {
        Create().Warn("Mail", "from taro@example.com see https://example.com/x?a=1");

        var text = File.ReadAllText(new AppPaths(_root).LogFile);
        Assert.DoesNotContain("@", text);
        Assert.DoesNotContain("example.com", text);
    }

    [Fact]
    public void 書き込みに失敗しても例外を投げない()
    {
        var blocked = Path.Combine(_root, "file");
        Directory.CreateDirectory(_root);
        File.WriteAllText(blocked, "x");   // フォルダを作るべき場所にファイルがある

        var log = new AppLog(new AppPaths(blocked));
        var ex = Record.Exception(() => log.Error("A", "m", new Exception("e")));

        Assert.Null(ex);
    }
}

public class LogScrubberTests
{
    [Theory]
    [InlineData("a@b.com")]
    [InlineData("連絡先は taro@example.co.jp です")]
    [InlineData("x @ y")]
    public void アットマークを含む語は取り除かれる(string input)
        => Assert.DoesNotContain("@", LogScrubber.Scrub(input));

    [Fact]
    public void 個人情報のない文字列は変わらない()
        => Assert.Equal("sync done 12 items", LogScrubber.Scrub("sync done 12 items"));
}
