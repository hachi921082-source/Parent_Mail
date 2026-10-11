using System.IO;
using ParentMail.App.Features.Compose;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.Compose;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.Compose;

// 作成画面の下書き（compose PLAN-2）。自動保存の条件、閉じるときの 3 択、送信後の下書きとの結びつけを確かめる。
public sealed class ComposeDraftTests : IDisposable
{
    private const string Acc = "acc1";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ParentMail_作成下書き_" + Guid.NewGuid().ToString("N"));
    private readonly FakeOutboxService _outbox = new();
    private readonly RecordingDialogs _dialogs = new();
    private readonly StubDraftService _drafts = new();
    private readonly InMemoryDraftRepository _rows = new();
    private readonly FakeAppLog _log = new();

    public ComposeDraftTests() => Directory.CreateDirectory(Path.Combine(_root, "入力"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private ComposeViewModel Make(ComposeInit? init = null)
        => new(init ?? new ComposeInit { AccountId = Acc }, "母のメール", _outbox, _dialogs,
            new RecipientSuggester(new InMemoryContactRepository(), new InMemoryMailRepository()),
            new CachePaths(Path.Combine(_root, "データ")), _log, _drafts, _rows);

    /// <summary>Choose の呼び出しを記録し、決めた答えを返す対話の代役。</summary>
    private sealed class RecordingDialogs : IDialogService
    {
        public List<(string Title, string Message)> Notices { get; } = new();
        public List<(string Title, string Message, IReadOnlyList<string> Buttons, int Default, int Safe)> Chooses { get; } = new();
        public int Answer { get; set; } = -1;
        public void ShowNotice(string title, string message) => Notices.Add((title, message));
        public bool Confirm(string title, string message, string yesText, string noText, bool safeIsDefault = true) => true;
        public int Choose(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int safeIndex)
        {
            Chooses.Add((title, message, buttons, defaultIndex, safeIndex));
            return Answer;
        }
    }

    /// <summary>保存の結果（Dirty）や失敗を差し替えられる下書きサービスの代役。</summary>
    private sealed class StubDraftService : IDraftService
    {
        public List<(DraftSnapshot Snapshot, bool UploadNow)> Saved { get; } = new();
        public List<long> Deleted { get; } = new();
        public bool NextDirty { get; set; }
        public bool Throw { get; set; }
        private long _next = 1;

        public Task<DraftItem> SaveAsync(DraftSnapshot snapshot, bool uploadNow, CancellationToken ct = default)
        {
            if (Throw) throw new IOException("disk");
            Saved.Add((snapshot, uploadNow));
            return Task.FromResult(new DraftItem { LocalId = snapshot.DraftId ?? _next++, AccountId = snapshot.AccountId, Dirty = NextDirty });
        }

        public Task DeleteAsync(long draftId, CancellationToken ct = default) { Deleted.Add(draftId); return Task.CompletedTask; }
        public Task FlushDirtyAsync(string accountId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ComposeInit?> OpenForEditAsync(long draftId, CancellationToken ct = default) => Task.FromResult<ComposeInit?>(null);
        public Task<ComposeInit?> OpenForEditByMessageAsync(string accountId, long gmailMsgId, CancellationToken ct = default) => Task.FromResult<ComposeInit?>(null);
    }

    // ---- 自動保存 ----

    [Fact]
    public async Task Autosave_interval_constant_is_thirty_seconds()
    {
        Assert.Equal(30, Constants.DraftAutoSaveSeconds);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Autosave_does_nothing_for_an_empty_window()
    {
        var vm = Make();
        await vm.AutoSaveTickAsync();
        Assert.Empty(_drafts.Saved);
    }

    [Fact]
    public async Task Autosave_does_nothing_for_a_reply_that_was_only_opened()
    {
        var vm = Make(new ComposeInit { AccountId = Acc, Subject = "Re: 件名", Body = "\n\n> 引用", ReplyHeaders = new ReplyHeaders("<a@b>", "<a@b>") });
        Assert.True(vm.HasContent);
        Assert.False(vm.IsDirtySinceLastSave);

        await vm.AutoSaveTickAsync();

        Assert.Empty(_drafts.Saved);
    }

    [Fact]
    public async Task Autosave_saves_changes_once_and_reuses_the_same_draft_row()
    {
        var vm = Make();
        vm.ToText = "山田 太郎 <taro@example.com>";
        vm.Subject = "会議の件";
        await vm.AutoSaveTickAsync();
        await vm.AutoSaveTickAsync(); // 変更なし

        Assert.Single(_drafts.Saved);
        Assert.True(_drafts.Saved[0].UploadNow);
        Assert.Null(_drafts.Saved[0].Snapshot.DraftId);
        Assert.Equal(1, vm.DraftId);

        vm.Body = "本文を足しました";
        await vm.AutoSaveTickAsync();

        Assert.Equal(2, _drafts.Saved.Count);
        Assert.Equal(1, _drafts.Saved[1].Snapshot.DraftId);
        Assert.False(vm.IsDirtySinceLastSave);
    }

    [Fact]
    public async Task Autosave_keeps_unreadable_recipients_as_typed()
    {
        var vm = Make();
        vm.ToText = "yama, ok@example.com";
        await vm.AutoSaveTickAsync();
        Assert.Equal(new[] { "yama", "ok@example.com" }, _drafts.Saved.Single().Snapshot.To);
    }

    [Fact]
    public async Task Autosave_counts_attachments_as_content_and_changes()
    {
        var vm = Make();
        var src = Path.Combine(_root, "入力", "請求書.txt");
        File.WriteAllText(src, "金額");
        vm.AddFiles(new[] { src });

        Assert.True(vm.HasContent);
        await vm.AutoSaveTickAsync();

        Assert.Equal("請求書.txt", _drafts.Saved.Single().Snapshot.Attachments.Single().FileName);
    }

    [Fact]
    public async Task Upload_failure_shows_the_status_line_and_retries_each_cycle_even_if_unchanged()
    {
        var vm = Make();
        vm.Subject = "件名";
        _drafts.NextDirty = true;
        await vm.AutoSaveTickAsync();
        Assert.Equal(Strings.BN_18, vm.DraftStatusText);
        Assert.True(vm.UploadPending);
        Assert.Empty(_dialogs.Notices); // 自動保存の失敗は知らせない

        await vm.AutoSaveTickAsync(); // 変更が無くても上げ直す
        Assert.Equal(2, _drafts.Saved.Count);

        _drafts.NextDirty = false;
        await vm.AutoSaveTickAsync();
        Assert.Equal("", vm.DraftStatusText);
        Assert.False(vm.UploadPending);

        await vm.AutoSaveTickAsync(); // もう何もしない
        Assert.Equal(3, _drafts.Saved.Count);
    }

    [Fact]
    public async Task Autosave_swallows_a_local_save_failure()
    {
        var vm = Make();
        vm.Subject = "件名";
        _drafts.Throw = true;

        await vm.AutoSaveTickAsync();

        Assert.Empty(_dialogs.Notices);
        Assert.Null(vm.DraftId);
    }

    [Fact]
    public async Task Clearing_everything_after_an_autosave_deletes_that_draft_on_the_next_tick()
    {
        var vm = Make();
        vm.Subject = "件名";
        await vm.AutoSaveTickAsync();
        Assert.Equal(1, vm.DraftId);

        vm.Subject = "";
        await vm.AutoSaveTickAsync();

        Assert.Equal(new long[] { 1 }, _drafts.Deleted);
        Assert.Null(vm.DraftId);
    }

    // ---- 閉じるとき ----

    [Fact]
    public async Task Close_with_no_content_is_silent()
    {
        var vm = Make();
        Assert.True(await vm.ConfirmCloseAsync());
        Assert.Empty(_dialogs.Chooses);
        Assert.Empty(_drafts.Saved);
    }

    [Fact]
    public async Task Close_with_no_content_removes_the_autosaved_draft()
    {
        var vm = Make();
        vm.Subject = "件名";
        await vm.AutoSaveTickAsync();
        vm.Subject = "";

        Assert.False(vm.TryApproveSilentClose()); // 消す下書きがあるので同期では決められない
        Assert.True(await vm.ConfirmCloseAsync());

        Assert.Equal(new long[] { 1 }, _drafts.Deleted);
        Assert.Empty(_dialogs.Chooses);
    }

    [Fact]
    public async Task Close_with_content_asks_with_save_as_default_and_back_as_the_safe_choice()
    {
        var vm = Make();
        vm.Subject = "件名";
        _dialogs.Answer = 2;

        Assert.False(await vm.ConfirmCloseAsync());

        var ask = Assert.Single(_dialogs.Chooses);
        Assert.Equal(Strings.WD_23, ask.Title);
        Assert.Equal(Strings.CP_15, ask.Message);
        Assert.Equal(new[] { Strings.CP_12, Strings.CP_13, Strings.CP_14 }, ask.Buttons);
        Assert.Equal(0, ask.Default);
        Assert.Equal(2, ask.Safe);
        Assert.Empty(_drafts.Saved);
    }

    [Fact]
    public async Task Close_dialog_cancel_by_escape_returns_to_the_window()
    {
        var vm = Make();
        vm.Subject = "件名";
        _dialogs.Answer = -1;
        Assert.False(await vm.ConfirmCloseAsync());
        Assert.False(vm.CloseApproved);
    }

    [Fact]
    public async Task Choosing_save_saves_and_uploads_then_allows_closing()
    {
        var vm = Make();
        vm.ToText = "花子 <hanako@example.jp>";
        vm.Subject = "お願い";
        _dialogs.Answer = 0;

        Assert.True(await vm.ConfirmCloseAsync());

        var saved = Assert.Single(_drafts.Saved);
        Assert.True(saved.UploadNow);
        Assert.Equal("お願い", saved.Snapshot.Subject);
        Assert.True(vm.CloseApproved);
    }

    [Fact]
    public async Task Choosing_save_while_offline_still_closes_with_a_local_draft()
    {
        var vm = Make();
        vm.Subject = "件名";
        _drafts.NextDirty = true;
        _dialogs.Answer = 0;

        Assert.True(await vm.ConfirmCloseAsync());
        Assert.Empty(_dialogs.Notices);
    }

    [Fact]
    public async Task Choosing_save_that_fails_shows_the_notice_and_keeps_the_window_open()
    {
        var vm = Make();
        vm.Subject = "件名";
        _dialogs.Answer = 0;
        _drafts.Throw = true;

        Assert.False(await vm.ConfirmCloseAsync());

        var notice = Assert.Single(_dialogs.Notices);
        Assert.Equal(Strings.WD_10, notice.Title);
        Assert.Equal(Strings.BN_17, notice.Message);
        Assert.False(vm.CloseApproved);

        // 直ったあとでもう一度選べる
        _drafts.Throw = false;
        Assert.True(await vm.ConfirmCloseAsync());
    }

    [Fact]
    public async Task Choosing_discard_keeps_the_last_autosave_and_saves_nothing_more()
    {
        var vm = Make();
        vm.Subject = "自動保存された件名";
        await vm.AutoSaveTickAsync();
        vm.Subject = "そのあとの書き足し";
        _dialogs.Answer = 1;

        Assert.True(await vm.ConfirmCloseAsync());

        Assert.Single(_drafts.Saved);
        Assert.Empty(_drafts.Deleted);
    }

    [Fact]
    public async Task Choosing_discard_without_any_autosave_leaves_nothing()
    {
        var vm = Make();
        vm.Subject = "件名";
        _dialogs.Answer = 1;

        Assert.True(await vm.ConfirmCloseAsync());
        Assert.Empty(_drafts.Saved);
        Assert.Empty(_drafts.Deleted);
    }

    [Fact]
    public async Task Closing_without_a_draft_service_still_works()
    {
        var vm = new ComposeViewModel(new ComposeInit { AccountId = Acc }, "母のメール", _outbox, _dialogs,
            new RecipientSuggester(new InMemoryContactRepository(), new InMemoryMailRepository()),
            new CachePaths(Path.Combine(_root, "データ")), _log);
        vm.Subject = "件名";
        _dialogs.Answer = 0;
        Assert.True(await vm.ConfirmCloseAsync());
    }

    // ---- 送信との関係 ----

    [Fact]
    public async Task Send_hands_the_autosaved_draft_id_to_the_outbox_and_stops_autosaving()
    {
        var vm = Make();
        vm.ToText = "山田 太郎 <taro@example.com>";
        vm.Subject = "件名";
        await vm.AutoSaveTickAsync();

        Assert.True(await vm.SendAsync());

        Assert.Equal(1, _outbox.Enqueued.Single().DraftId);
        Assert.True(vm.CloseApproved);
        vm.Body = "送信後の変更";
        await vm.AutoSaveTickAsync();
        Assert.Single(_drafts.Saved);
    }

    [Fact]
    public async Task Send_without_any_autosave_has_no_draft_id()
    {
        var vm = Make();
        vm.ToText = "taro@example.com";
        vm.Subject = "件名";
        Assert.True(await vm.SendAsync());
        Assert.Null(_outbox.Enqueued.Single().DraftId);
    }

    // ---- 下書きを開く ----

    [Fact]
    public async Task Opening_a_draft_restores_bcc_from_the_draft_row_and_keeps_saving_the_same_row()
    {
        var row = _rows.Save(new DraftItem { AccountId = Acc, To = new[] { "a@example.com" }, Bcc = new[] { "秘密 <hidden@example.com>" }, Subject = "続き" });
        var vm = Make(new ComposeInit { AccountId = Acc, To = row.To, Subject = row.Subject, SourceDraftId = row.LocalId });

        Assert.Equal("秘密 <hidden@example.com>", vm.BccText);
        Assert.True(vm.ShowBcc);
        Assert.Equal(row.LocalId, vm.DraftId);
        Assert.False(vm.IsDirtySinceLastSave);

        vm.Body = "続きを書いた";
        await vm.AutoSaveTickAsync();

        Assert.Equal(row.LocalId, _drafts.Saved.Single().Snapshot.DraftId);
        Assert.Equal(new[] { "秘密 <hidden@example.com>" }, _drafts.Saved.Single().Snapshot.Bcc);
    }
}
