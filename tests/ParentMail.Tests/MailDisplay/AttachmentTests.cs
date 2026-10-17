using System.IO;
using System.IO.Compression;
using ParentMail.App.Features.MailDisplay;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.MailDisplay;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.MailDisplay;

public class AttachmentFileNameTests
{
    [Theory]
    [InlineData("請求書_2026年10月.pdf", "請求書_2026年10月.pdf")]
    [InlineData("写真 (1).jpg", "写真 (1).jpg")]
    [InlineData("ＡＢＣ　あいう.txt", "ＡＢＣ　あいう.txt")]
    public void 日本語の名前はそのまま残る(string input, string expected)
        => Assert.Equal(expected, AttachmentFileName.Sanitize(input));

    [Fact]
    public void パスに使えない文字は置き換え_日本語は残る()
    {
        var result = AttachmentFileName.Sanitize("報告:書?*<>|\"2026.pdf");
        Assert.Equal("報告_書______2026.pdf", result);
    }

    [Theory]
    [InlineData("..\\..\\悪い.exe", "悪い.exe")]
    [InlineData("a/b/資料.docx", "資料.docx")]
    public void フォルダ区切りより前は取り除く(string input, string expected)
        => Assert.Equal(expected, AttachmentFileName.Sanitize(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData(null)]
    public void 空やドットだけなら既定名(string? input)
        => Assert.Equal("添付ファイル", AttachmentFileName.Sanitize(input));

    [Fact]
    public void 予約名は先頭に下線を付ける()
        => Assert.Equal("_CON.txt", AttachmentFileName.Sanitize("CON.txt"));

    [Fact]
    public void 長い名前は拡張子を残して縮める()
    {
        var result = AttachmentFileName.Sanitize(new string('あ', 300) + ".pdf");
        Assert.EndsWith(".pdf", result);
        Assert.True(result.Length <= 120);
    }

    [Theory]
    [InlineData(0, "1KB")]
    [InlineData(1500, "2KB")]
    [InlineData(999_999, "1000KB")]
    [InlineData(1_250_000, "1.3MB")]
    public void 大きさはKBとMBで表す(long bytes, string expected)
        => Assert.Equal(expected, AttachmentFileName.FormatSize(bytes));
}

public sealed class AttachmentActionsTests : IDisposable
{
    private const string Acc = "acc1";

    private sealed class ScriptedAttachments : IAttachmentService
    {
        public AttachmentFetchResult Result { get; set; } = new(AttachmentFetchStatus.Failed, null);
        public bool Throw { get; set; }
        public int Calls { get; private set; }
        public Task<AttachmentFetchResult> EnsureLocalAsync(string accountId, long attachmentId, CancellationToken ct = default)
        {
            Calls++;
            if (Throw) throw new InvalidOperationException("x");
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingConfirmDialogs : IDialogService
    {
        public List<(string Title, string Message)> Notices { get; } = new();
        public List<(string Title, string Message, string Yes, string No, bool SafeDefault)> Confirms { get; } = new();
        public bool ConfirmAnswer { get; set; }
        public void ShowNotice(string title, string message) => Notices.Add((title, message));
        public bool Confirm(string title, string message, string yesText, string noText, bool safeIsDefault = true)
        {
            Confirms.Add((title, message, yesText, noText, safeIsDefault));
            return ConfirmAnswer;
        }
        public int Choose(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int safeIndex) => -1;
    }

    private sealed class FakeFileUi : IAttachmentFileUi
    {
        public string? SavePath { get; set; }
        public List<string> Suggested { get; } = new();
        public List<string> Opened { get; } = new();
        public bool OpenThrows { get; set; }
        public string? PickSavePath(string suggestedName)
        {
            Suggested.Add(suggestedName);
            return SavePath;
        }
        public void OpenWithDefaultApp(string path)
        {
            if (OpenThrows) throw new InvalidOperationException("x");
            Opened.Add(path);
        }
    }

    // 日本語を含むフォルダで動かす
    private readonly string _root = Path.Combine(Path.GetTempPath(), "親メール_添付テスト_" + Guid.NewGuid().ToString("N"));
    private readonly ScriptedAttachments _service = new();
    private readonly RecordingConfirmDialogs _dialogs = new();
    private readonly FakeFileUi _ui = new();
    private readonly FakeAppLog _log = new();
    private readonly AttachmentActions _actions;

    public AttachmentActionsTests()
    {
        Directory.CreateDirectory(_root);
        _actions = new AttachmentActions(_service, _dialogs, _ui, _log, Path.Combine(_root, "temp"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private static AttachmentItem Item(string fileName, long id = 1)
        => new(new AttachmentInfo { Id = id, GmailMsgId = 10, FileName = fileName, ContentType = "application/octet-stream", Size = 2048 });

    // キャッシュ上のファイル（保管庫の名前は拡張子を持たない）を用意して Ready にする
    private string Cache(byte[] content)
    {
        var path = Path.Combine(_root, "保管庫", "1.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        _service.Result = new AttachmentFetchResult(AttachmentFetchStatus.Ready, path);
        return path;
    }

    private static byte[] Zip(params string[] entryNames)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var n in entryNames)
            {
                using var w = new StreamWriter(zip.CreateEntry(n).Open());
                w.Write("x");
            }
        }
        return ms.ToArray();
    }

    // ---- 危険な添付 ----
    [Fact]
    public void 実行ファイルの行は危険の印が付き_開けない()
    {
        var item = Item("setup.exe");
        Assert.True(item.IsDangerous);
        Assert.False(item.CanOpen);
        Assert.True(item.CanSave);
        Assert.Equal(Strings.PV_08, item.DangerText);
        Assert.Equal("", item.ReasonText);
    }

    [Fact]
    public async Task 危険な添付は開く操作をしても何もしない()
    {
        Cache(new byte[] { 1 });
        var item = Item("setup.exe");
        await _actions.OpenAsync(Acc, item);
        Assert.Equal(0, _service.Calls);
        Assert.Empty(_ui.Opened);
    }

    [Fact]
    public async Task 危険な添付の保存は確認を出し_キャンセルなら保存しない()
    {
        Cache(new byte[] { 1, 2, 3 });
        var target = Path.Combine(_root, "out.exe");
        _ui.SavePath = target;
        _dialogs.ConfirmAnswer = false;

        await _actions.SaveAsync(Acc, Item("setup.exe"));

        var c = Assert.Single(_dialogs.Confirms);
        Assert.Equal(Strings.WD_27, c.Title);
        Assert.Equal(Strings.PV_09, c.Message);
        Assert.Equal("保存する", c.Yes);
        Assert.Equal("キャンセル", c.No);
        Assert.True(c.SafeDefault);
        Assert.Empty(_ui.Suggested);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public async Task 危険な添付でも確認で保存するを選べば保存できる()
    {
        Cache(new byte[] { 1, 2, 3 });
        var target = Path.Combine(_root, "out.exe");
        _ui.SavePath = target;
        _dialogs.ConfirmAnswer = true;

        await _actions.SaveAsync(Acc, Item("setup.exe"));

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(target));
    }

    // ---- 通常の添付 ----
    [Fact]
    public async Task 通常の添付の保存は確認なしで日本語名のまま提案して保存する()
    {
        Cache(new byte[] { 9, 9 });
        var target = Path.Combine(_root, "保存先フォルダ", "請求書.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        _ui.SavePath = target;

        await _actions.SaveAsync(Acc, Item("請求書.pdf"));

        Assert.Empty(_dialogs.Confirms);
        Assert.Equal("請求書.pdf", Assert.Single(_ui.Suggested));
        Assert.Equal(new byte[] { 9, 9 }, File.ReadAllBytes(target));
    }

    [Fact]
    public async Task 保存先を選ばなければ何も書かない()
    {
        Cache(new byte[] { 1 });
        _ui.SavePath = null;
        await _actions.SaveAsync(Acc, Item("a.pdf"));
        Assert.Empty(_dialogs.Notices);
    }

    [Fact]
    public async Task 保存に失敗したらお知らせを出す()
    {
        Cache(new byte[] { 1 });
        _ui.SavePath = Path.Combine(_root, "無い", "フォルダ", "a.pdf");
        await _actions.SaveAsync(Acc, Item("a.pdf"));
        var n = Assert.Single(_dialogs.Notices);
        Assert.Equal((Strings.WD_10, Strings.BN_15), n);
    }

    [Fact]
    public async Task 開くは一時フォルダに元の日本語名でコピーして既定のアプリで開く()
    {
        Cache(new byte[] { 5, 6 });
        await _actions.OpenAsync(Acc, Item("お知らせ 10月号.pdf"));

        var opened = Assert.Single(_ui.Opened);
        Assert.StartsWith(Path.Combine(_root, "temp"), opened);
        Assert.Equal("お知らせ 10月号.pdf", Path.GetFileName(opened));
        Assert.Equal(new byte[] { 5, 6 }, File.ReadAllBytes(opened));
    }

    [Fact]
    public async Task 開けなかったらお知らせを出す()
    {
        Cache(new byte[] { 5 });
        _ui.OpenThrows = true;
        await _actions.OpenAsync(Acc, Item("a.pdf"));
        Assert.Equal((Strings.WD_10, Strings.BN_16), Assert.Single(_dialogs.Notices));
    }

    // ---- ダウンロードの結果 ----
    [Fact]
    public async Task オフラインなら保存されていない旨のお知らせ()
    {
        _service.Result = new AttachmentFetchResult(AttachmentFetchStatus.Offline, null);
        var item = Item("a.pdf");
        await _actions.OpenAsync(Acc, item);
        await _actions.SaveAsync(Acc, item);
        Assert.All(_dialogs.Notices, n => Assert.Equal((Strings.WD_10, Strings.BN_09), n));
        Assert.Equal(2, _dialogs.Notices.Count);
        Assert.Empty(_ui.Opened);
        Assert.False(item.IsBusy);
    }

    [Fact]
    public async Task 取得に失敗したら取り込めなかった旨のお知らせ()
    {
        _service.Result = new AttachmentFetchResult(AttachmentFetchStatus.Failed, null);
        await _actions.OpenAsync(Acc, Item("a.pdf"));
        Assert.Equal((Strings.WD_10, Strings.BN_14), Assert.Single(_dialogs.Notices));
    }

    [Fact]
    public async Task 取得で例外が出ても失敗のお知らせにして処理中を戻す()
    {
        _service.Throw = true;
        var item = Item("a.pdf");
        await _actions.SaveAsync(Acc, item);
        Assert.Equal((Strings.WD_10, Strings.BN_14), Assert.Single(_dialogs.Notices));
        Assert.False(item.IsBusy);
    }

    [Fact]
    public async Task Readyでもファイルが無ければ失敗のお知らせ()
    {
        _service.Result = new AttachmentFetchResult(AttachmentFetchStatus.Ready, Path.Combine(_root, "無い.bin"));
        await _actions.OpenAsync(Acc, Item("a.pdf"));
        Assert.Equal((Strings.WD_10, Strings.BN_14), Assert.Single(_dialogs.Notices));
    }

    // ---- zip はダウンロード後に中身を確認する ----
    [Fact]
    public void zipは取得前は名前だけの判定で通常の行()
    {
        var item = Item("書類.zip");
        Assert.False(item.IsDangerous);
        Assert.True(item.NeedsContentCheck);
        Assert.True(item.CanOpen);
    }

    [Fact]
    public async Task exeを含むzipを開こうとすると_取得後に危険と判定され開かない()
    {
        Cache(Zip("説明.txt", "インストーラ.exe"));
        var item = Item("書類.zip");

        await _actions.OpenAsync(Acc, item);

        Assert.True(item.IsDangerous);
        Assert.False(item.CanOpen);
        Assert.Empty(_ui.Opened);
        Assert.Empty(_dialogs.Notices);
    }

    [Fact]
    public async Task 安全なzipは開ける()
    {
        Cache(Zip("説明.txt", "写真/家族.jpg"));
        var item = Item("書類.zip");
        await _actions.OpenAsync(Acc, item);
        Assert.False(item.IsDangerous);
        Assert.Single(_ui.Opened);
    }

    [Fact]
    public async Task 壊れたzipは中身を確認できない旨の理由行が付き_開けない()
    {
        Cache(new byte[] { 1, 2, 3, 4 });
        var item = Item("書類.zip");
        await _actions.OpenAsync(Acc, item);
        Assert.True(item.IsDangerous);
        Assert.Equal(Strings.SW_07, item.ReasonText);
        Assert.True(item.HasReason);
        Assert.Empty(_ui.Opened);
    }

    [Fact]
    public async Task 危険なzipの保存も確認を挟む()
    {
        Cache(Zip("a.exe"));
        _dialogs.ConfirmAnswer = false;
        await _actions.SaveAsync(Acc, Item("書類.zip"));
        Assert.Single(_dialogs.Confirms);
        Assert.Empty(_ui.Suggested);
    }

    // ---- 一時フォルダの掃除 ----
    [Fact]
    public void 起動時の掃除で一時フォルダの中身が消える()
    {
        var temp = Path.Combine(_root, "temp");
        var sub = Path.Combine(temp, "abc");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "古い 添付.pdf"), "x");
        File.WriteAllText(Path.Combine(temp, "残り.txt"), "x");

        _actions.CleanTemp();

        Assert.True(Directory.Exists(temp));
        Assert.Empty(Directory.GetFileSystemEntries(temp));
    }

    [Fact]
    public void 起動時の掃除はHTML表示用の一時フォルダを消さない()
    {
        var temp = Path.Combine(_root, "temp");
        var html = Path.Combine(temp, ParentMail.Core.Platform.AppPaths.HtmlTempDirName);
        Directory.CreateDirectory(html);
        File.WriteAllText(Path.Combine(html, "mail.html"), "x");
        var sub = Path.Combine(temp, "abc");
        Directory.CreateDirectory(sub);

        _actions.CleanTemp();

        Assert.True(File.Exists(Path.Combine(html, "mail.html")));
        Assert.False(Directory.Exists(sub));
    }

    [Fact]
    public void 一時フォルダが無くても掃除は失敗しない()
        => _actions.CleanTemp();
}

public sealed class AttachmentListViewModelTests
{
    private const string Acc = "acc1";

    private sealed class NoopAttachments : IAttachmentService
    {
        public Task<AttachmentFetchResult> EnsureLocalAsync(string accountId, long attachmentId, CancellationToken ct = default)
            => Task.FromResult(new AttachmentFetchResult(AttachmentFetchStatus.Failed, null));
    }

    private sealed class NoopUi : IAttachmentFileUi
    {
        public string? PickSavePath(string suggestedName) => null;
        public void OpenWithDefaultApp(string path) { }
    }

    private readonly InMemoryMailRepository _mails = new();
    private readonly AttachmentListViewModel _vm;

    public AttachmentListViewModelTests()
    {
        var actions = new AttachmentActions(new NoopAttachments(), new FakeDialogService(), new NoopUi(), new FakeAppLog(),
            Path.Combine(Path.GetTempPath(), "親メール_一覧テスト"));
        _vm = new AttachmentListViewModel(_mails, actions, new FakeTextScale());
    }

    private void Add(long id, string name, bool inline = false, long size = 3000)
        => _mails.AddAttachment(Acc, new AttachmentInfo
        {
            Id = id, GmailMsgId = 10, PartId = id.ToString(), FileName = name, ContentType = "application/pdf", Size = size,
            IsInline = inline, ContentId = inline ? "cid" + id : null,
        });

    [Fact]
    public void 埋め込み画像は一覧に出さない()
    {
        Add(1, "請求書.pdf");
        Add(2, "logo.png", inline: true);
        _vm.Load(Acc, 10);
        var item = Assert.Single(_vm.Items);
        Assert.Equal("請求書.pdf", item.FileName);
        Assert.Equal("3KB", item.SizeText);
        Assert.True(_vm.HasItems);
    }

    [Fact]
    public void 添付が無ければ一覧は空で非表示()
    {
        _vm.Load(Acc, 10);
        Assert.False(_vm.HasItems);
    }

    [Fact]
    public void 同じメールの再読み込みでは行を作り直さない()
    {
        Add(1, "a.pdf");
        _vm.Load(Acc, 10);
        var first = _vm.Items[0];
        _vm.Load(Acc, 10);
        Assert.Same(first, _vm.Items[0]);
    }

    [Fact]
    public void Clearで空になる()
    {
        Add(1, "a.pdf");
        _vm.Load(Acc, 10);
        _vm.Clear();
        Assert.False(_vm.HasItems);
    }

    [Fact]
    public void 危険な行は開くコマンドが使えず_保存は使える()
    {
        Add(1, "setup.exe");
        Add(2, "ok.pdf");
        _vm.Load(Acc, 10);
        Assert.False(_vm.OpenCommand.CanExecute(_vm.Items[0]));
        Assert.True(_vm.SaveCommand.CanExecute(_vm.Items[0]));
        Assert.True(_vm.OpenCommand.CanExecute(_vm.Items[1]));
    }

    [Fact]
    public void 文字の大きさは設定に従う()
        => Assert.Equal(new FakeTextScale().FontSizeDip, _vm.FontSize);
}

public sealed class PreviewAttachmentWiringTests
{
    private const string Acc = "acc1";

    private sealed class Switch : ParentMail.App.Features.MailDisplay.IHtmlModeSwitch
    {
        public bool IsOn { get; set; }
        public event EventHandler? Changed { add { } remove { } }
    }

    private sealed class NoFetch : IBodyFetcher
    {
        public Task<BodyFetchResult> EnsureBodyAsync(string accountId, long gmailMsgId, CancellationToken ct = default)
            => Task.FromResult(BodyFetchResult.Ready);
    }

    private sealed class NoRead : ParentMail.Core.Features.SyncCache.IReadStateSync
    {
        public Task SetSeenAsync(string accountId, long gmailMsgId, bool seen, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoLauncher : ParentMail.Core.SafetyWarnings.IExternalLauncher
    {
        public void OpenUrl(string url) { }
    }

    private sealed class NoUi : IAttachmentFileUi
    {
        public string? PickSavePath(string suggestedName) => null;
        public void OpenWithDefaultApp(string path) { }
    }

    [Fact]
    public async Task メールを選ぶと添付の一覧が出て_選択を外すと消える()
    {
        var state = new FakeAppState { CurrentAccountId = Acc };
        var mails = new InMemoryMailRepository();
        mails.Upsert(Acc, new MailMessage
        {
            GmailMsgId = 7, Subject = "資料", FromAddress = "a@example.com", To = new[] { "oya@example.com" },
            DateUtc = DateTime.UtcNow, BodyState = BodyState.Fetched, BodyText = "本文",
        }, new[] { new MessageFolder(7, FolderKind.Inbox, 7, 1) });
        mails.AddAttachment(Acc, new AttachmentInfo { Id = 1, GmailMsgId = 7, PartId = "2", FileName = "資料.pdf", Size = 5000, ContentType = "application/pdf" });

        var scale = new FakeTextScale();
        var dialogs = new FakeDialogService();
        var list = new AttachmentListViewModel(mails,
            new AttachmentActions(new FakeAttachmentService(), dialogs, new NoUi(), new FakeAppLog(), Path.Combine(Path.GetTempPath(), "親メール_配線テスト")),
            scale);
        var accounts = new InMemoryAccountRepository();
        var vm = new PreviewViewModel(state, mails, new NoFetch(), new FakeAttachmentService(), new NoRead(), new FakeContactLookup(), scale,
            new Switch(), new PreviewSelectionHints(), new InMemoryOutboxRepository(), new InMemoryDraftRepository(), accounts, dialogs,
            new FakeAppLog(), new ParentMail.App.SafetyWarnings.LinkClickHandler(dialogs, new NoLauncher(), new FakeComposeWindowFactory(), state),
            attachmentList: list);

        state.SelectedMessageId = 7;
        await vm.LastLoad;
        Assert.Equal("資料.pdf", Assert.Single(list.Items).FileName);

        state.SelectedMessageId = null;
        await vm.LastLoad;
        Assert.False(list.HasItems);
    }
}
