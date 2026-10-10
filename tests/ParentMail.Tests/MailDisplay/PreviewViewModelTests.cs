using ParentMail.App.Features.MailDisplay;
using ParentMail.App.Resources;
using ParentMail.App.SafetyWarnings;
using ParentMail.Core;
using ParentMail.Core.Features.MailDisplay;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Core.SafetyWarnings;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.MailDisplay;

public class PreviewViewModelTests
{
    private const string Acc = "acc1";

    private sealed class FakeReadState : IReadStateSync
    {
        public List<(long Id, bool Seen)> Calls { get; } = new();
        public bool Throw { get; set; }
        public Task SetSeenAsync(string accountId, long gmailMsgId, bool seen, CancellationToken ct = default)
        {
            Calls.Add((gmailMsgId, seen));
            return Throw ? Task.FromException(new InvalidOperationException("x")) : Task.CompletedTask;
        }
    }

    // 取得の間に本文を保存する動作を差し込める本文取得
    private sealed class ScriptedFetcher : IBodyFetcher
    {
        public BodyFetchResult Result { get; set; } = BodyFetchResult.Ready;
        public List<long> Requested { get; } = new();
        public Action<long>? OnFetch { get; set; }
        public Task<BodyFetchResult> EnsureBodyAsync(string accountId, long gmailMsgId, CancellationToken ct = default)
        {
            Requested.Add(gmailMsgId);
            if (Result == BodyFetchResult.Ready) OnFetch?.Invoke(gmailMsgId);
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeHtmlSwitch : IHtmlModeSwitch
    {
        private bool _on;
        public bool IsOn { get => _on; set { if (_on != value) { _on = value; Changed?.Invoke(this, EventArgs.Empty); } } }
        public event EventHandler? Changed;
    }

    private sealed class FakeLauncher : IExternalLauncher
    {
        public List<string> Opened { get; } = new();
        public void OpenUrl(string url) => Opened.Add(url);
    }

    private readonly FakeAppState _state = new() { CurrentAccountId = Acc };
    private readonly InMemoryMailRepository _mails = new();
    private readonly ScriptedFetcher _fetcher = new();
    private readonly FakeAttachmentService _attachments = new();
    private readonly FakeReadState _read = new();
    private readonly FakeContactLookup _contacts = new();
    private readonly FakeTextScale _scale = new();
    private readonly FakeHtmlSwitch _switch = new();
    private readonly PreviewSelectionHints _hints = new();
    private readonly InMemoryOutboxRepository _outbox = new();
    private readonly InMemoryDraftRepository _drafts = new();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLog _log = new();
    private readonly FakeLauncher _launcher = new();
    private bool _webView = true;
    private Dictionary<string, byte[]> _files = new();

    public PreviewViewModelTests()
    {
        _accounts.Add(new Account(Acc, "oya@example.com", "母", 0));
    }

    private PreviewViewModel Create()
        => new(_state, _mails, _fetcher, _attachments, _read, _contacts, _scale, _switch, _hints, _outbox, _drafts, _accounts,
            _dialogs, _log, new LinkClickHandler(_dialogs, _launcher, new FakeComposeWindowFactory(), _state),
            () => _webView, path => Task.FromResult(_files[path]));

    private MailMessage AddMail(long id, FolderKind folder = FolderKind.Inbox, bool fetched = true, bool read = false,
        string? text = "こんにちは、お元気ですか。", string? html = null, string? derived = null, string[]? cc = null,
        string from = "taro@example.com")
    {
        var m = new MailMessage
        {
            GmailMsgId = id, Subject = $"件名{id}：ご案内", FromAddress = from, FromName = "山田 太郎",
            To = new[] { "oya@example.com" }, Cc = cc ?? Array.Empty<string>(),
            DateUtc = new DateTime(2026, 10, 8, 1, 2, 0, DateTimeKind.Utc), IsRead = read,
            BodyState = fetched ? BodyState.Fetched : BodyState.None, BodyText = fetched ? text : null, BodyHtml = fetched ? html : null,
            DerivedText = fetched ? derived : null,
        };
        _mails.Upsert(Acc, m, new[] { new MessageFolder(id, folder, (uint)id, 1) });
        return m;
    }

    private async Task<PreviewViewModel> SelectAsync(PreviewViewModel vm, long id, bool auto = false)
    {
        if (auto) { using (_hints.BeginAutoSelection()) _state.SelectedMessageId = id; }
        else _state.SelectedMessageId = id;
        await vm.LastLoad;
        return vm;
    }

    [Fact]
    public async Task 何も選んでいないときは案内を出す()
    {
        var vm = Create();
        await vm.LastLoad;
        Assert.Equal(PreviewPane.Guide, vm.Pane);
        Assert.Equal(Strings.PV_12, vm.GuideText);
    }

    [Fact]
    public async Task 選んだメールのヘッダを出す()
    {
        AddMail(1, cc: new[] { "b@example.com", "c@example.com" });
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal(PreviewPane.Mail, vm.Pane);
        Assert.Equal("山田 太郎 <taro@example.com>", vm.FromText);
        Assert.Equal("oya@example.com", vm.ToText);
        Assert.Equal("b@example.com、c@example.com", vm.CcText);
        Assert.True(vm.HasCc);
        Assert.Equal("件名1：ご案内", vm.SubjectText);
        Assert.Equal(PreviewHeaderFormatter.FormatDate(new DateTime(2026, 10, 8, 1, 2, 0, DateTimeKind.Utc)), vm.DateText);
        Assert.Equal(BodyView.Plain, vm.Body);
        Assert.Equal("こんにちは、お元気ですか。", vm.PlainContent);
    }

    [Fact]
    public async Task CCがなければCC行を出さない()
    {
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        Assert.False(vm.HasCc);
    }

    // ---- 既読 ----
    [Theory]
    [InlineData(FolderKind.Inbox, true)]
    [InlineData(FolderKind.Spam, true)]
    [InlineData(FolderKind.Trash, false)]
    [InlineData(FolderKind.Sent, false)]
    public async Task 利用者が選ぶと受信トレイと迷惑メールだけ既読にする(FolderKind folder, bool expectRead)
    {
        _state.CurrentFolder = folder;
        AddMail(1, folder);
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal(expectRead ? 1 : 0, _read.Calls.Count);
        if (expectRead) Assert.Equal((1L, true), _read.Calls[0]);
    }

    [Fact]
    public async Task 自動で選び直されたメールは既読にしない()
    {
        AddMail(1);
        var vm = await SelectAsync(Create(), 1, auto: true);
        Assert.Empty(_read.Calls);
        Assert.Equal(BodyView.Plain, vm.Body);
    }

    [Fact]
    public async Task 既読のメールは書き込まない()
    {
        AddMail(1, read: true);
        await SelectAsync(Create(), 1);
        Assert.Empty(_read.Calls);
    }

    [Fact]
    public async Task 既読にできなくても表示は続く()
    {
        _read.Throw = true;
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal(BodyView.Plain, vm.Body);
        Assert.Contains(_log.Entries, e => e.StartsWith("E:MailDisplay"));
    }

    // ---- 本文取得 ----
    [Fact]
    public async Task 取得済みの本文は取得処理を呼ばない()
    {
        AddMail(1);
        await SelectAsync(Create(), 1);
        Assert.Empty(_fetcher.Requested);
    }

    [Fact]
    public async Task 未取得の本文は取得してから表示する()
    {
        AddMail(1, fetched: false);
        // 取得の間に本文が保存された状態を再現する
        _fetcher.OnFetch = id => _mails.SetBody(Acc, id, "あとから届いた本文", null, null);
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal(new long[] { 1 }, _fetcher.Requested);
        Assert.Equal("あとから届いた本文", vm.PlainContent);
        Assert.Equal(BodyView.Plain, vm.Body);
    }

    [Fact]
    public async Task 取得がオフラインならオフラインの文言を出す()
    {
        AddMail(1, fetched: false);
        _fetcher.Result = BodyFetchResult.Offline;
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal(BodyView.Offline, vm.Body);
        Assert.Equal(Strings.BN_08, vm.StatusText);
        // ヘッダは出す
        Assert.Equal("件名1：ご案内", vm.SubjectText);
    }

    [Fact]
    public async Task 取得に失敗したら読み込めなかった文言を出す()
    {
        AddMail(1, fetched: false);
        _fetcher.Result = BodyFetchResult.Failed;
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal(BodyView.Failed, vm.Body);
        Assert.Equal(Strings.BN_13, vm.StatusText);
    }

    [Fact]
    public async Task 取得成功でも本文が保存されていなければ失敗扱い()
    {
        AddMail(1, fetched: false);
        _fetcher.Result = BodyFetchResult.Ready;
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal(BodyView.Failed, vm.Body);
    }

    // ---- 本文の選び方 ----
    [Fact]
    public async Task テキストがなければHTMLから作った文章を使う()
    {
        AddMail(1, text: null, html: "<p>日本語のHTML</p>", derived: "日本語のHTML");
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal("日本語のHTML", vm.PlainContent);
        Assert.Equal(BodyView.Plain, vm.Body);
        Assert.True(vm.HtmlAvailable);
    }

    [Fact]
    public async Task 本文がまったくなければ表示できる本文がない旨を出す()
    {
        AddMail(1, text: null, html: null, derived: null);
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal(BodyView.NoBody, vm.Body);
        Assert.Equal(Strings.PV_03, vm.StatusText);
        Assert.False(vm.HtmlAvailable);
    }

    // ---- HTML ----
    [Fact]
    public async Task HTMLのチェックでHTML表示に切り替わりCSPが入る()
    {
        AddMail(1, html: "<html><body><img src=\"https://r.example/a.png\"><p>画像つき</p></body></html>");
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal(BodyView.Plain, vm.Body);

        _switch.IsOn = true;
        await vm.LastLoad;
        Assert.Equal(BodyView.Html, vm.Body);
        Assert.Contains("Content-Security-Policy", vm.HtmlContent);
        Assert.Contains("img-src data:;", vm.HtmlContent);
        Assert.True(vm.CanShowImages);

        _switch.IsOn = false;
        await vm.LastLoad;
        Assert.Equal(BodyView.Plain, vm.Body);
    }

    [Fact]
    public async Task 画像を表示で外部画像を許す表示に作り直す()
    {
        AddMail(1, html: "<img src=\"https://r.example/a.png\">");
        var vm = await SelectAsync(Create(), 1);
        _switch.IsOn = true;
        await vm.LastLoad;
        Assert.True(vm.ShowImagesCommand.CanExecute(null));

        vm.ShowImagesCommand.Execute(null);
        await vm.LastLoad;
        Assert.True(vm.ShowImages);
        Assert.Contains("img-src data: https: http:", vm.HtmlContent);
        Assert.False(vm.CanShowImages);
    }

    [Fact]
    public async Task 別のメールを選ぶとHTMLと画像表示は初期状態に戻る()
    {
        AddMail(1, html: "<img src=\"https://r.example/a.png\">");
        AddMail(2, html: "<p>二通目</p>");
        var vm = await SelectAsync(Create(), 1);
        _switch.IsOn = true;
        await vm.LastLoad;
        vm.ShowImagesCommand.Execute(null);
        await vm.LastLoad;

        await SelectAsync(vm, 2);
        Assert.False(_switch.IsOn);
        Assert.False(vm.ShowImages);
        Assert.Equal(BodyView.Plain, vm.Body);
    }

    [Fact]
    public async Task 外部画像がなければ画像を表示の案内は出さない()
    {
        AddMail(1, html: "<p>文字だけ</p>");
        var vm = await SelectAsync(Create(), 1);
        _switch.IsOn = true;
        await vm.LastLoad;
        Assert.Equal(BodyView.Html, vm.Body);
        Assert.False(vm.CanShowImages);
    }

    [Fact]
    public async Task cid画像は保存済みの部品からdataURIにする()
    {
        AddMail(1, html: "<img src=\"cid:logo1\"><p>ロゴ</p>");
        var path = @"C:\テスト\添付\ロゴ.png";
        _files[path] = new byte[] { 1, 2, 3, 4 };
        ((InMemoryMailRepository)_mails).AddAttachment(Acc, new AttachmentInfo
        {
            Id = 10, GmailMsgId = 1, PartId = "2", FileName = "ロゴ.png", ContentType = "image/png", Size = 4,
            IsInline = true, ContentId = "<logo1>", LocalPath = path,
        });
        var vm = await SelectAsync(Create(), 1);
        _switch.IsOn = true;
        await vm.LastLoad;
        Assert.Contains("data:image/png;base64," + Convert.ToBase64String(_files[path]), vm.HtmlContent);
        Assert.Empty(_attachments.Requested);
    }

    [Fact]
    public async Task 部品が未保存ならオンデマンド取得を使う()
    {
        AddMail(1, html: "<img src=\"cid:p1\">");
        var path = @"C:\取得済み\画像.jpg";
        _files[path] = new byte[] { 9, 9 };
        _attachments.Result = new AttachmentFetchResult(AttachmentFetchStatus.Ready, path);
        ((InMemoryMailRepository)_mails).AddAttachment(Acc, new AttachmentInfo
        {
            Id = 11, GmailMsgId = 1, PartId = "2", FileName = "画像.jpg", ContentType = "image/jpeg", Size = 2, IsInline = true, ContentId = "p1",
        });
        var vm = await SelectAsync(Create(), 1);
        _switch.IsOn = true;
        await vm.LastLoad;
        Assert.Equal(new long[] { 11 }, _attachments.Requested);
        Assert.Contains("data:image/jpeg;base64,", vm.HtmlContent);
    }

    [Fact]
    public async Task 取得できない埋め込み画像があっても本文は表示する()
    {
        AddMail(1, html: "<img src=\"cid:p1\"><p>本文</p>");
        _attachments.Result = new AttachmentFetchResult(AttachmentFetchStatus.Offline, null);
        ((InMemoryMailRepository)_mails).AddAttachment(Acc, new AttachmentInfo
        {
            Id = 11, GmailMsgId = 1, PartId = "2", FileName = "x.png", ContentType = "image/png", Size = 2, IsInline = true, ContentId = "p1",
        });
        var vm = await SelectAsync(Create(), 1);
        _switch.IsOn = true;
        await vm.LastLoad;
        Assert.Equal(BodyView.Html, vm.Body);
        Assert.Contains("本文", vm.HtmlContent);
    }

    [Fact]
    public async Task HTML表示の部品がない環境ではお知らせを出して戻す()
    {
        _webView = false;
        AddMail(1, html: "<p>x</p>");
        var vm = await SelectAsync(Create(), 1);
        _switch.IsOn = true;
        await vm.LastLoad;
        Assert.False(_switch.IsOn);
        Assert.Equal(BodyView.Plain, vm.Body);
        Assert.Equal((Strings.WD_10, Strings.PV_06), Assert.Single(_dialogs.Notices));
    }

    [Fact]
    public async Task HTMLがないメールではチェックを入れても戻る()
    {
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        Assert.False(vm.HtmlAvailable);
        _switch.IsOn = true;
        await vm.LastLoad;
        Assert.False(_switch.IsOn);
        Assert.Empty(_dialogs.Notices);
    }

    // ---- あやしいリンク ----
    [Fact]
    public async Task あやしいリンクがあれば赤い帯を出す()
    {
        AddMail(1, text: "確認はこちら http://192.168.0.1/login をご覧ください");
        var vm = await SelectAsync(Create(), 1);
        Assert.True(vm.SuspiciousLinkBanner);
        Assert.Equal(Strings.SW_01, vm.SuspiciousLinkBannerText);
    }

    [Fact]
    public async Task 普通のリンクでは帯を出さない()
    {
        AddMail(1, text: "https://www.example.co.jp/news をどうぞ");
        var vm = await SelectAsync(Create(), 1);
        Assert.False(vm.SuspiciousLinkBanner);
    }

    [Fact]
    public async Task リンクのクリックは危険判定の処理に渡す()
    {
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        await vm.HandleLinkAsync("https://www.example.co.jp/", null);
        Assert.Equal(new[] { "https://www.example.co.jp/" }, _launcher.Opened);
    }

    // ---- アドレス帳の案内 ----
    [Fact]
    public async Task 知らない差出人にはアドレス帳の案内を出す()
    {
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        Assert.True(vm.NotInAddressBook);
        Assert.Equal(Strings.PV_02, vm.NotInAddressBookText);
    }

    [Fact]
    public async Task アドレス帳にいる差出人には案内を出さない()
    {
        _contacts.Known.Add("taro@example.com");
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        Assert.False(vm.NotInAddressBook);
    }

    [Fact]
    public async Task 送信済みでは案内を出さない()
    {
        _state.CurrentFolder = FolderKind.Sent;
        AddMail(1, FolderKind.Sent);
        var vm = await SelectAsync(Create(), 1);
        Assert.False(vm.NotInAddressBook);
    }

    // ---- 複数選択の案内 ----
    [Fact]
    public async Task 一覧にチェックがあれば複数選択の案内を出し外すと元に戻る()
    {
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        _hints.CheckedCount = 3;
        Assert.Equal(PreviewPane.Multi, vm.Pane);
        Assert.Equal("3通のメールを選んでいます。「削除」を押すと、まとめてごみ箱に移します。", vm.GuideText);

        _hints.CheckedCount = 0;
        Assert.Equal(PreviewPane.Mail, vm.Pane);
        Assert.Equal("件名1：ご案内", vm.SubjectText);
    }

    // ---- 送信箱・下書き ----
    [Fact]
    public async Task 送信箱の内容は読み取り専用で出し本文取得も既読化もしない()
    {
        _state.CurrentFolder = FolderKind.Outbox;
        var item = _outbox.Add(new OutboxItem
        {
            AccountId = Acc, To = new[] { "hanako@example.com" }, Subject = "送信待ちの件名", Body = "送信待ちの本文",
            CreatedUtc = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
        });
        var vm = Create();
        _state.SelectedMessageId = item.Id;
        await vm.LastLoad;
        Assert.Equal("oya@example.com", vm.FromText);
        Assert.Equal("hanako@example.com", vm.ToText);
        Assert.Equal("送信待ちの本文", vm.PlainContent);
        Assert.False(vm.NotInAddressBook);
        Assert.Empty(_fetcher.Requested);
        Assert.Empty(_read.Calls);
    }

    [Fact]
    public async Task ローカルの下書きの内容を出す()
    {
        _state.CurrentFolder = FolderKind.Drafts;
        var d = _drafts.Save(new DraftItem { AccountId = Acc, To = new[] { "a@example.com" }, Subject = "書きかけ", Body = "書きかけの本文" });
        var vm = Create();
        _state.SelectedMessageId = d.LocalId;
        await vm.LastLoad;
        Assert.Equal("書きかけ", vm.SubjectText);
        Assert.Equal("書きかけの本文", vm.PlainContent);
    }

    [Fact]
    public async Task 見つからないメールは案内に戻る()
    {
        var vm = Create();
        _state.SelectedMessageId = 999;
        await vm.LastLoad;
        Assert.Equal(PreviewPane.Guide, vm.Pane);
    }

    // ---- 拡大 ----
    [Fact]
    public async Task 拡大縮小とリセットとラベル()
    {
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal("100%", vm.ZoomLabel);
        Assert.False(vm.ZoomOutCommand.CanExecute(null));
        Assert.False(vm.ZoomResetCommand.CanExecute(null));

        vm.ZoomInCommand.Execute(null);
        vm.ZoomInCommand.Execute(null);
        Assert.Equal("150%", vm.ZoomLabel);
        Assert.True(vm.ZoomResetCommand.CanExecute(null));

        vm.ZoomOutCommand.Execute(null);
        Assert.Equal("125%", vm.ZoomLabel);
        vm.ZoomResetCommand.Execute(null);
        Assert.Equal("100%", vm.ZoomLabel);
    }

    [Fact]
    public async Task 拡大は300パーセントで止まりボタンが使えなくなる()
    {
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        for (var i = 0; i < 12; i++) vm.ZoomIn();
        Assert.Equal("300%", vm.ZoomLabel);
        Assert.False(vm.ZoomInCommand.CanExecute(null));
    }

    [Fact]
    public async Task 文字サイズと拡大率からプレーンの大きさとZoomFactorを出す()
    {
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        Assert.Equal(24.0, vm.PlainFontSize, 6);
        Assert.Equal(1.5, vm.WebZoomFactor);

        vm.ZoomIn();
        vm.ZoomIn();
        vm.ZoomIn();
        vm.ZoomIn(); // 200%
        Assert.Equal(48.0, vm.PlainFontSize, 6);
        Assert.Equal(3.0, vm.WebZoomFactor);

        _scale.TextSizePt = 48;
        _scale.RaiseChanged();
        for (var i = 0; i < 8; i++) vm.ZoomIn(); // 300%
        Assert.Equal(80.0, vm.PlainFontSize, 6); // 60pt で頭打ち
        Assert.Equal(5.0, vm.WebZoomFactor);
    }

    [Fact]
    public async Task メールを変えると拡大は100パーセントに戻る()
    {
        AddMail(1);
        AddMail(2);
        var vm = await SelectAsync(Create(), 1);
        vm.ZoomIn();
        await SelectAsync(vm, 2);
        Assert.Equal("100%", vm.ZoomLabel);
    }

    [Fact]
    public async Task フォルダやアカウントを変えても拡大は100パーセントに戻る()
    {
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        vm.ZoomIn();
        _state.CurrentFolder = FolderKind.Trash;
        await vm.LastLoad;
        Assert.Equal("100%", vm.ZoomLabel);

        vm.ZoomIn();
        _state.CurrentAccountId = "acc2";
        await vm.LastLoad;
        Assert.Equal("100%", vm.ZoomLabel);
    }

    [Fact]
    public async Task 設定の文字サイズが変わると大きさの値を通知する()
    {
        AddMail(1);
        var vm = await SelectAsync(Create(), 1);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _scale.TextSizePt = 24;
        _scale.RaiseChanged();
        Assert.Contains(string.Empty, changed);
        Assert.Equal(32.0, vm.PlainFontSize, 6);
    }
}
