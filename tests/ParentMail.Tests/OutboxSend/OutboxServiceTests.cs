using ParentMail.App.Features.OutboxSend;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Models;
using ParentMail.Tests.Data;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.OutboxSend;

public sealed class OutboxServiceTests : IDisposable
{
    private const string Acc = CacheFixture.AccountId;
    private const string Other = CacheFixture.OtherAccountId;

    private readonly CacheFixture _fx = new();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeSmtpGateway _smtp = new();
    private readonly FakeNotifier _notifier = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeSyncService _sync = new();
    private readonly FakeDraftService _draftSvc = new();
    private readonly FakeAppLog _log = new();

    public OutboxServiceTests()
    {
        _accounts.Add(new Account(Acc, "me@gmail.com", "母", 0));
        _accounts.Add(new Account(Other, "dad@gmail.com", "父", 1));
    }

    public void Dispose() => _fx.Dispose();

    private OutboxService NewService() => new(_fx.Outbox, _fx.Drafts, _accounts, _smtp, _fx.Paths, _notifier, _dialogs, _log,
        () => _sync, () => _draftSvc);

    private static DraftSnapshot Snap(string account = Acc, long? draftId = null, IReadOnlyList<AttachmentInfo>? atts = null) => new()
    {
        AccountId = account, DraftId = draftId, To = new[] { "to@example.com" }, Bcc = new[] { "b@example.com" },
        Subject = "件名", Body = "本文", Attachments = atts ?? Array.Empty<AttachmentInfo>(),
        ReplyHeaders = new ReplyHeaders("<r@x>", "<r@x>"),
    };

    private string MakeSourceFile(string name, byte[] data)
    {
        var p = Path.Combine(_fx.Root, "元ファイル_" + name);
        Directory.CreateDirectory(_fx.Root);
        File.WriteAllBytes(p, data);
        return p;
    }

    [Fact]
    public async Task Success_removes_row_files_and_linked_draft_and_syncs()
    {
        var src = MakeSourceFile("請求書.pdf", new byte[] { 9, 8, 7 });
        var svc = NewService();
        var item = await svc.EnqueueAndSendAsync(Snap(draftId: 42, atts: new[] { new AttachmentInfo { FileName = "請求書.pdf", ContentType = "application/pdf", LocalPath = src } }));
        await svc.WaitIdleAsync();

        Assert.Empty(_fx.Outbox.GetAll(Acc));
        Assert.False(Directory.Exists(Path.Combine(_fx.Paths.AccountDir(Acc), "outbox", item.Id.ToString())));
        Assert.Equal(new long[] { 42 }, _draftSvc.Deleted);
        Assert.Contains((Acc, SyncTrigger.AfterSend), _sync.Requests);
        var (msg, account) = Assert.Single(_smtp.Sent);
        Assert.Equal(Acc, account);
        Assert.Equal("請求書.pdf", Assert.Single(msg.Attachments.OfType<MimeKit.MimePart>()).FileName);
        Assert.Equal(0, svc.PendingCount());
    }

    [Fact]
    public async Task Enqueue_copies_attachments_under_outbox_folder_before_sending()
    {
        _smtp.Result = SendResult.Transient;
        var src = MakeSourceFile("写真.jpg", new byte[] { 1, 2, 3 });
        var svc = NewService();
        var item = await svc.EnqueueAndSendAsync(Snap(atts: new[] { new AttachmentInfo { FileName = "写真.jpg", LocalPath = src } }));
        await svc.WaitIdleAsync();

        var stored = Assert.Single(_fx.Outbox.GetAll(Acc));
        var att = Assert.Single(stored.Attachments);
        Assert.Equal("写真.jpg", att.FileName);
        Assert.StartsWith(Path.Combine("outbox", item.Id.ToString()), att.LocalPath);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(_fx.Paths.AccountDir(Acc), att.LocalPath!)));
        Assert.True(File.Exists(src));
        Assert.Equal(1, stored.RetryCount);
    }

    [Fact]
    public async Task Failures_are_counted_persisted_and_notify_once_at_third()
    {
        _smtp.Result = SendResult.Transient;
        var svc = NewService();
        await svc.EnqueueAndSendAsync(Snap());
        await svc.WaitIdleAsync();
        Assert.Equal(1, Assert.Single(_fx.Outbox.GetAll(Acc)).RetryCount);
        Assert.Empty(_notifier.Banners);

        // 再起動を模して新しいサービスで再送する
        var svc2 = NewService();
        await svc2.RetryDueAsync(Acc);
        Assert.Equal(2, Assert.Single(_fx.Outbox.GetAll(Acc)).RetryCount);
        Assert.Empty(_notifier.Banners);
        Assert.Equal(0, svc2.FailedCount(Acc));

        await svc2.RetryDueAsync(Acc);
        var third = Assert.Single(_fx.Outbox.GetAll(Acc));
        Assert.Equal(3, third.RetryCount);
        Assert.True(third.FailureNotified);
        Assert.Equal(Strings.OB_01, _notifier.Banners[BannerKind.OutboxFailure].Text);
        Assert.Equal(1, svc2.FailedCount(Acc));

        // 3 回目以降も再送は続くが、通知は繰り返さない
        _notifier.Banners.Clear();
        await svc2.RetryDueAsync(Acc);
        Assert.Equal(4, Assert.Single(_fx.Outbox.GetAll(Acc)).RetryCount);
        Assert.Empty(_notifier.Banners);
        Assert.Equal(1, svc2.FailedCount(Acc));
    }

    [Fact]
    public async Task Marker_clears_when_a_failed_item_finally_succeeds()
    {
        _smtp.Result = SendResult.Permanent;
        var svc = NewService();
        await svc.EnqueueAndSendAsync(Snap(draftId: 5));
        await svc.WaitIdleAsync();
        await svc.RetryDueAsync(Acc);
        await svc.RetryDueAsync(Acc);
        Assert.Equal(1, svc.FailedCount(Acc));

        _smtp.Result = SendResult.Ok;
        await svc.RetryDueAsync(Acc);
        Assert.Empty(_fx.Outbox.GetAll(Acc));
        Assert.Equal(0, svc.FailedCount(Acc));
        Assert.Equal(new long[] { 5 }, _draftSvc.Deleted);
    }

    [Fact]
    public async Task Auth_failure_is_not_counted_and_marks_account()
    {
        _smtp.Result = SendResult.AuthFailed;
        var svc = NewService();
        await svc.EnqueueAndSendAsync(Snap());
        await svc.WaitIdleAsync();
        await svc.RetryDueAsync(Acc);
        var stored = Assert.Single(_fx.Outbox.GetAll(Acc));
        Assert.Equal(0, stored.RetryCount);
        Assert.Contains(Acc, _sync.AuthFailed);
        Assert.Empty(_notifier.Banners);
    }

    [Fact]
    public async Task SendNow_failure_shows_modal_and_counts_success_removes()
    {
        _smtp.Result = SendResult.Transient;
        var svc = NewService();
        var item = await svc.EnqueueAndSendAsync(Snap());
        await svc.WaitIdleAsync();

        var r = await svc.SendNowAsync(item.Id);
        Assert.Equal(SendResult.Transient, r);
        Assert.Equal((Strings.WD_10, Strings.OB_05), Assert.Single(_dialogs.Notices));
        Assert.Equal(2, Assert.Single(_fx.Outbox.GetAll(Acc)).RetryCount);

        _smtp.Result = SendResult.Ok;
        Assert.Equal(SendResult.Ok, await svc.SendNowAsync(item.Id));
        Assert.Empty(_fx.Outbox.GetAll(Acc));
        Assert.Single(_dialogs.Notices);
    }

    [Fact]
    public async Task ReturnToDrafts_moves_payload_to_dirty_draft_keeping_linkage_and_files()
    {
        _smtp.Result = SendResult.Transient;
        var src = MakeSourceFile("資料.txt", new byte[] { 5, 5 });
        var linked = _fx.Drafts.Save(new DraftItem { AccountId = Acc, Subject = "古い", GmailUid = 77, Dirty = false });
        var svc = NewService();
        var item = await svc.EnqueueAndSendAsync(Snap(draftId: linked.LocalId,
            atts: new[] { new AttachmentInfo { FileName = "資料.txt", LocalPath = src } }));
        await svc.WaitIdleAsync();

        var draft = await svc.ReturnToDraftsAsync(item.Id);

        Assert.Empty(_fx.Outbox.GetAll(Acc));
        Assert.False(Directory.Exists(Path.Combine(_fx.Paths.AccountDir(Acc), "outbox", item.Id.ToString())));
        Assert.Equal(linked.LocalId, draft.LocalId);
        var saved = Assert.Single(_fx.Drafts.GetAll(Acc));
        Assert.True(saved.Dirty);
        Assert.Equal(77u, saved.GmailUid);
        Assert.Equal("件名", saved.Subject);
        Assert.Equal("本文", saved.Body);
        Assert.Equal(new[] { "b@example.com" }, saved.Bcc);
        Assert.Equal("<r@x>", saved.ReplyHeaders?.InReplyTo);
        var att = Assert.Single(saved.Attachments);
        Assert.Equal("資料.txt", att.FileName);
        Assert.Equal(new byte[] { 5, 5 }, File.ReadAllBytes(Path.Combine(_fx.Paths.AccountDir(Acc), att.LocalPath!)));
    }

    [Fact]
    public async Task ReturnToDrafts_without_linked_draft_creates_new_draft()
    {
        _smtp.Result = SendResult.Transient;
        var svc = NewService();
        var item = await svc.EnqueueAndSendAsync(Snap());
        await svc.WaitIdleAsync();
        var draft = await svc.ReturnToDraftsAsync(item.Id);
        Assert.NotEqual(0, draft.LocalId);
        var saved = Assert.Single(_fx.Drafts.GetAll(Acc));
        Assert.True(saved.Dirty);
        Assert.Null(saved.GmailUid);
    }

    [Fact]
    public async Task SendAllPending_covers_both_accounts()
    {
        _smtp.Result = SendResult.Transient;
        var svc = NewService();
        await svc.EnqueueAndSendAsync(Snap(Acc));
        await svc.EnqueueAndSendAsync(Snap(Other));
        await svc.WaitIdleAsync();
        Assert.Equal(2, svc.PendingCount());
        Assert.Equal(1, svc.PendingCount(Other));

        Assert.Equal(SendAllResult.SomeFailed, await svc.SendAllPendingAsync());
        Assert.Equal(2, svc.PendingCount());

        _smtp.Result = SendResult.Ok;
        Assert.Equal(SendAllResult.AllSent, await svc.SendAllPendingAsync());
        Assert.Equal(0, svc.PendingCount());
    }

    [Fact]
    public async Task Oversized_message_stays_with_modal_notice()
    {
        var big = Path.Combine(_fx.Root, "大きい.bin");
        Directory.CreateDirectory(_fx.Root);
        using (var f = File.Create(big)) f.SetLength(Constants.MaxEncodedMessageBytes);
        var svc = NewService();
        await svc.EnqueueAndSendAsync(Snap(atts: new[] { new AttachmentInfo { FileName = "大きい.bin", LocalPath = big } }));
        await svc.WaitIdleAsync();
        Assert.Empty(_smtp.Sent);
        Assert.Single(_fx.Outbox.GetAll(Acc));
        Assert.Equal((Strings.WD_10, Strings.OB_06), Assert.Single(_dialogs.Notices));
    }

    [Fact]
    public async Task Changed_event_fires_on_enqueue_and_completion()
    {
        var svc = NewService();
        var n = 0;
        svc.Changed += (_, _) => Interlocked.Increment(ref n);
        await svc.EnqueueAndSendAsync(Snap());
        await svc.WaitIdleAsync();
        Assert.True(n >= 2);
    }
}
