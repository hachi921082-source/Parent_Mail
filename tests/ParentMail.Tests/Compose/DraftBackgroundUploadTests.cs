using System.IO;
using NSubstitute;
using ParentMail.App.Features.Compose;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.Compose;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.Compose;

// 閉じるときの下書き保存は Gmail を待たない（Issue #4）。遅い偽ゲートウェイで、
// 窓が待たないこと・失敗後も Dirty が残ること・重なっても二重に上げないこと・反映中の書き換えを失わないことを確かめる。
public sealed class DraftBackgroundUploadTests : IDisposable
{
    private const string Acc = "acc1";
    private const string DraftsPath = "[Gmail]/Drafts";
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ParentMail_裏反映_" + Guid.NewGuid().ToString("N"));
    private readonly InMemoryDraftRepository _drafts = new();
    private readonly InMemoryMailRepository _mail = new();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeImapGatewayFactory _gateways = new();
    private readonly FakeAppLog _log = new();
    private readonly FakeImapGateway _gw = new();
    private readonly DraftService _svc;

    public DraftBackgroundUploadTests()
    {
        Directory.CreateDirectory(_root);
        _accounts.Add(new Account(Acc, "taro@example.com", "母のメール", 0));
        _gw.Folders.Add(new ImapFolderInfo(DraftsPath, FolderKind.Drafts));
        _gateways.ByAccountId[Acc] = _gw;
        _svc = new DraftService(_drafts, _mail, _accounts, _gateways, new FakeBodyFetcher(), new FakeAttachmentService(),
            new CachePaths(Path.Combine(_root, "データ")), _log);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private static DraftSnapshot Snap(string subject = "会議のご案内", long? id = null) => new()
    {
        DraftId = id, AccountId = Acc, To = new[] { "taro@example.com" }, Subject = subject, Body = "本文です。",
        Attachments = Array.Empty<AttachmentInfo>(),
    };

    private static async Task<T> Within<T>(Task<T> task)
    {
        if (await Task.WhenAny(task, Task.Delay(Limit)) != task) throw new TimeoutException("待ちが終わらない");
        return await task;
    }

    private static async Task Within(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(Limit)) != task) throw new TimeoutException("待ちが終わらない");
        await task;
    }

    // ---- サービス ----

    [Fact]
    public async Task Local_save_returns_while_gmail_is_slow_and_queued_upload_clears_dirty_afterwards()
    {
        _gw.AppendGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var row = await Within(_svc.SaveAsync(Snap(), uploadNow: false));
        _svc.QueueUpload(Acc);
        await Within(_gw.AppendEntered.Task); // 裏で APPEND が始まり、止まっている

        Assert.True(row.Dirty);
        Assert.True(Assert.Single(_drafts.GetAll(Acc)).Dirty);

        _gw.AppendGate.SetResult();
        await Within(_svc.WaitForBackgroundAsync());

        var stored = Assert.Single(_drafts.GetAll(Acc));
        Assert.False(stored.Dirty);
        Assert.Equal(1u, stored.GmailUid);
    }

    [Fact]
    public async Task Background_upload_raises_completed_so_the_list_can_refresh()
    {
        var raised = new List<string>();
        _svc.BackgroundCompleted += (_, e) => raised.Add(e.AccountId);

        await _svc.SaveAsync(Snap(), uploadNow: false);
        _svc.QueueUpload(Acc);
        await Within(_svc.WaitForBackgroundAsync());

        Assert.Equal(new[] { Acc }, raised);
    }

    [Fact]
    public async Task Failed_background_upload_keeps_dirty_and_flush_retries_it()
    {
        _gw.Offline = true;
        var raised = 0;
        _svc.BackgroundCompleted += (_, _) => raised++;

        await _svc.SaveAsync(Snap(), uploadNow: false);
        _svc.QueueUpload(Acc);
        await Within(_svc.WaitForBackgroundAsync());

        Assert.True(Assert.Single(_drafts.GetAll(Acc)).Dirty);
        Assert.Equal(0, raised);

        _gw.Offline = false; // 再接続後の最初の同期で FlushDirtyAsync が呼ばれる
        await _svc.FlushDirtyAsync(Acc);

        var stored = Assert.Single(_drafts.GetAll(Acc));
        Assert.False(stored.Dirty);
        Assert.Equal(1, _gw.AppendCount);
    }

    [Fact]
    public async Task Local_save_is_not_blocked_by_an_upload_in_flight_and_the_newer_text_stays_dirty()
    {
        _gw.AppendGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = await _svc.SaveAsync(Snap("古い件名"), uploadNow: false);
        _svc.QueueUpload(Acc);
        await Within(_gw.AppendEntered.Task);

        // 反映が止まっている間に、同じ下書きを書き換えて保存する（待たされない）
        await Within(_svc.SaveAsync(Snap("新しい件名", first.LocalId), uploadNow: false));
        _gw.AppendGate.SetResult();
        await Within(_svc.WaitForBackgroundAsync());

        var stored = Assert.Single(_drafts.GetAll(Acc));
        Assert.Equal("新しい件名", stored.Subject);
        Assert.True(stored.Dirty);          // 古い版を上げたので、新しい版はまだ未反映
        Assert.Equal(1u, stored.GmailUid);   // 上げた版の UID は覚えている（次に消す対象）

        _gw.AppendGate = null;
        await _svc.FlushDirtyAsync(Acc);
        Assert.False(Assert.Single(_drafts.GetAll(Acc)).Dirty);
        Assert.Equal(2, _gw.AppendCount);
    }

    [Fact]
    public async Task Autosave_upload_and_close_queue_overlapping_upload_only_once()
    {
        _gw.AppendGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var autosave = _svc.SaveAsync(Snap(), uploadNow: true); // 自動保存の反映が止まっている
        await Within(_gw.AppendEntered.Task);

        _svc.QueueUpload(Acc);                                   // 閉じる側の反映が重なる
        _gw.AppendGate.SetResult();
        await Within(autosave);
        await Within(_svc.WaitForBackgroundAsync());

        Assert.Equal(1, _gw.AppendCount);
        Assert.False(Assert.Single(_drafts.GetAll(Acc)).Dirty);
    }

    [Fact]
    public async Task Delete_for_close_removes_the_row_at_once_and_deletes_the_gmail_copy_in_background()
    {
        var row = await _svc.SaveAsync(Snap(), uploadNow: true); // Gmail に UID 付きで載っている
        _gw.Calls.Clear();

        await Within(_svc.DeleteLocalAndQueueRemoteAsync(row.LocalId));
        Assert.Empty(_drafts.GetAll(Acc));

        await Within(_svc.WaitForBackgroundAsync());
        Assert.Contains("Delete:" + DraftsPath, _gw.Calls);
    }

    [Fact]
    public async Task Draft_deleted_while_uploading_does_not_leave_the_appended_copy_in_gmail()
    {
        _gw.AppendGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var row = await _svc.SaveAsync(Snap(), uploadNow: false);
        _svc.QueueUpload(Acc);
        await Within(_gw.AppendEntered.Task);

        await Within(_svc.DeleteLocalAndQueueRemoteAsync(row.LocalId));
        _gw.AppendGate.SetResult();
        await Within(_svc.WaitForBackgroundAsync());

        Assert.Empty(_drafts.GetAll(Acc));
        Assert.Contains("Delete:" + DraftsPath, _gw.Calls); // 追加した版を片付けた
    }

    [Fact]
    public async Task Hook_asks_for_a_sync_when_background_work_completes()
    {
        var sync = Substitute.For<ISyncService>();
        var drafts = new FakeDraftService();
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sync.SyncNowAsync(Acc, SyncTrigger.Manual, Arg.Any<CancellationToken>())
            .Returns(_ => { called.TrySetResult(); return Task.CompletedTask; });
        new DraftSyncHook(sync, drafts, _log).Attach();

        drafts.RaiseBackgroundCompleted(Acc);

        await Within(called.Task);
    }

    // ---- 作成画面 ----

    [Fact]
    public async Task Closing_with_save_completes_without_waiting_for_gmail()
    {
        _gw.AppendGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialogs = Substitute.For<IDialogService>();
        dialogs.Choose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<int>(), Arg.Any<int>()).Returns(0);
        var vm = new ComposeViewModel(new ComposeInit { AccountId = Acc }, "母のメール", new FakeOutboxService(), dialogs,
            new RecipientSuggester(new InMemoryContactRepository(), new InMemoryMailRepository()),
            new CachePaths(Path.Combine(_root, "データ")), _log, _svc, _drafts);
        vm.Subject = "閉じるときの件名";

        var closed = await Within(vm.ConfirmCloseAsync()); // Append は止まったまま

        Assert.True(closed);
        Assert.True(Assert.Single(_drafts.GetAll(Acc)).Dirty);

        await Within(_gw.AppendEntered.Task);
        _gw.AppendGate.SetResult();
        await Within(_svc.WaitForBackgroundAsync());
        Assert.False(Assert.Single(_drafts.GetAll(Acc)).Dirty);
    }

    [Fact]
    public async Task Closing_an_emptied_window_removes_the_autosaved_draft_without_waiting_for_gmail()
    {
        var saved = await _svc.SaveAsync(Snap(), uploadNow: true);
        var dialogs = Substitute.For<IDialogService>();
        var vm = new ComposeViewModel(new ComposeInit { AccountId = Acc, SourceDraftId = saved.LocalId }, "母のメール", new FakeOutboxService(),
            dialogs, new RecipientSuggester(new InMemoryContactRepository(), new InMemoryMailRepository()),
            new CachePaths(Path.Combine(_root, "データ")), _log, _svc, _drafts);
        vm.ToText = "";
        vm.Subject = "";
        vm.Body = "";

        Assert.True(await Within(vm.ConfirmCloseAsync()));

        Assert.Empty(_drafts.GetAll(Acc));
        await Within(_svc.WaitForBackgroundAsync());
    }
}
