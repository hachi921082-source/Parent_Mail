using ParentMail.App.Resources;
using ParentMail.App.SafetyWarnings;
using ParentMail.Core;
using ParentMail.Core.SafetyWarnings;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.SafetyWarnings;

public class LinkClickHandlerTests
{
    private sealed class RecordingDialogs : IDialogService
    {
        public List<(string Title, string Message)> Notices { get; } = new();
        public List<(string Title, string Message, string Yes, string No, bool SafeIsDefault)> Confirms { get; } = new();
        public bool ConfirmAnswer { get; set; }
        public void ShowNotice(string title, string message) => Notices.Add((title, message));
        public bool Confirm(string title, string message, string yesText, string noText, bool safeIsDefault = true)
        {
            Confirms.Add((title, message, yesText, noText, safeIsDefault));
            return ConfirmAnswer;
        }
        public int Choose(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int safeIndex) => -1;
    }

    private sealed class FakeLauncher : IExternalLauncher
    {
        public List<string> Opened { get; } = new();
        public void OpenUrl(string url) => Opened.Add(url);
    }

    private readonly RecordingDialogs _dialogs = new();
    private readonly FakeLauncher _launcher = new();
    private readonly FakeComposeWindowFactory _compose = new();
    private readonly FakeAppState _state = new() { CurrentAccountId = "acc1" };

    private LinkClickHandler Create() => new(_dialogs, _launcher, _compose, _state);

    [Fact]
    public async Task 安全なリンクは確認なしで開く()
    {
        await Create().HandleAsync("https://example.com/a", "詳しくはこちら");
        Assert.Equal(new[] { "https://example.com/a" }, _launcher.Opened);
        Assert.Empty(_dialogs.Confirms);
    }

    [Fact]
    public async Task あやしいリンクは確認ダイアログを出し既定は安全側()
    {
        await Create().HandleAsync("http://bit.ly/abc", null);
        var c = Assert.Single(_dialogs.Confirms);
        Assert.Equal(Strings.SW_02, c.Title);
        Assert.Contains("http://bit.ly/abc", c.Message);
        Assert.Equal("それでも開く", c.Yes);
        Assert.Equal("やめる", c.No);
        Assert.True(c.SafeIsDefault);
        Assert.Empty(_launcher.Opened);
    }

    [Fact]
    public async Task 確認で開くを選ぶと開く()
    {
        _dialogs.ConfirmAnswer = true;
        await Create().HandleAsync("http://bit.ly/abc", null);
        Assert.Single(_launcher.Opened);
    }

    [Fact]
    public async Task 表示とリンク先が違うと確認する()
    {
        await Create().HandleAsync("https://evil.example.net/", "https://www.example.com/");
        Assert.Single(_dialogs.Confirms);
        Assert.Empty(_launcher.Opened);
    }

    [Fact]
    public async Task 非ASCIIのリンク先はpunycodeと復号形を並べて見せる()
    {
        await Create().HandleAsync("https://日本語.jp/", null);
        var msg = Assert.Single(_dialogs.Confirms).Message;
        Assert.Contains("xn--", msg);
        Assert.Contains("日本語.jp", msg);
    }

    [Fact]
    public async Task mailtoは宛先入りで作成ウィンドウを開く()
    {
        await Create().HandleAsync("mailto:taro@example.com", "taro@example.com");
        var init = Assert.Single(_compose.Opened);
        Assert.Equal("acc1", init.AccountId);
        Assert.Equal(new[] { "taro@example.com" }, init.To);
        Assert.Empty(_launcher.Opened);
        Assert.Empty(_dialogs.Confirms);
    }

    [Fact]
    public async Task mailtoは作成ウィンドウが開いていれば新しい窓を作らず前面に出すだけ()
    {
        _compose.WindowOpen = true;
        await Create().HandleAsync("mailto:taro@example.com", null);
        Assert.Empty(_compose.Opened);
        Assert.Equal(1, _compose.Activated);
        Assert.Empty(_dialogs.Notices);
    }

    [Fact]
    public async Task mailtoのクエリと複数宛先を扱う()
    {
        await Create().HandleAsync("mailto:a@example.com,b@example.com?subject=x", null);
        Assert.Equal(new[] { "a@example.com", "b@example.com" }, Assert.Single(_compose.Opened).To);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-msdt:/id PCWDiagnostic")]
    [InlineData("ftp://example.com/x")]
    [InlineData("tel:0312345678")]
    [InlineData("not a url")]
    [InlineData("mailto:")]
    public async Task 許可しないスキームは起動せず通知する(string url)
    {
        await Create().HandleAsync(url, null);
        var n = Assert.Single(_dialogs.Notices);
        Assert.Equal(Strings.WD_10, n.Title);
        Assert.Equal(Strings.SW_05, n.Message);
        Assert.Empty(_launcher.Opened);
        Assert.Empty(_compose.Opened);
        Assert.Empty(_dialogs.Confirms);
    }

    [Fact]
    public async Task アカウント未選択のmailtoは開けない()
    {
        _state.CurrentAccountId = null;
        await Create().HandleAsync("mailto:taro@example.com", null);
        Assert.Empty(_compose.Opened);
        Assert.Single(_dialogs.Notices);
    }
}
