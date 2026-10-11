using System.IO;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.Compose;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.Compose;

// 下書きサービス（compose PLAN-2）。Gmail の Drafts への反映順、オフライン、日本語名の添付、編集用の初期値を確かめる。
public sealed class DraftServiceTests : IDisposable
{
    private const string Acc = "acc1";
    private const string DraftsPath = "[Gmail]/Drafts";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ParentMail_下書き_" + Guid.NewGuid().ToString("N"));
    private readonly InMemoryDraftRepository _drafts = new();
    private readonly InMemoryMailRepository _mail = new();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeImapGatewayFactory _gateways = new();
    private readonly FakeBodyFetcher _body = new();
    private readonly FakeAttachmentService _attachments = new();
    private readonly FakeAppLog _log = new();
    private readonly FakeImapGateway _gw;
    private readonly DraftService _svc;

    public DraftServiceTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "入力"));
        _accounts.Add(new Account(Acc, "taro@example.com", "母のメール", 0));
        _gw = new FakeImapGateway();
        _gw.Folders.Add(new ImapFolderInfo(DraftsPath, FolderKind.Drafts));
        _gateways.ByAccountId[Acc] = _gw;
        _svc = new DraftService(_drafts, _mail, _accounts, _gateways, _body, _attachments, new CachePaths(Path.Combine(_root, "データ")), _log);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private static DraftSnapshot Snap(string subject = "会議のご案内", long? id = null, params AttachmentInfo[] files) => new()
    {
        DraftId = id, AccountId = Acc, To = new[] { "山田 太郎 <taro@example.com>" }, Cc = new[] { "b@example.com" },
        Subject = subject, Body = "本文です。\r\nよろしくお願いします。", Attachments = files,
    };

    private string MakeFile(string name, string content)
    {
        var path = Path.Combine(_root, "入力", name);
        File.WriteAllText(path, content);
        return path;
    }

    // ---- 保存と反映 ----

    [Fact]
    public async Task Save_with_upload_appends_to_drafts_and_marks_clean()
    {
        var row = await _svc.SaveAsync(Snap(), uploadNow: true);

        Assert.NotEqual(0, row.LocalId);
        Assert.False(row.Dirty);
        Assert.Equal(1u, row.GmailUid);
        Assert.Contains("Append:" + DraftsPath, _gw.Calls);
        var stored = Assert.Single(_drafts.GetAll(Acc));
        Assert.False(stored.Dirty);
        Assert.Equal("会議のご案内", stored.Subject);
    }

    [Fact]
    public async Task Save_without_upload_stays_dirty_and_does_not_touch_gmail()
    {
        var row = await _svc.SaveAsync(Snap(), uploadNow: false);

        Assert.True(row.Dirty);
        Assert.Empty(_gw.Calls);
    }

    [Fact]
    public async Task Offline_save_keeps_the_row_dirty_and_flush_uploads_after_reconnect()
    {
        _gw.Offline = true;
        var row = await _svc.SaveAsync(Snap(), uploadNow: true);
        Assert.True(row.Dirty);
        Assert.True(Assert.Single(_drafts.GetAll(Acc)).Dirty);

        _gw.Offline = false;
        _gw.Calls.Clear();
        await _svc.FlushDirtyAsync(Acc);

        Assert.Contains("Append:" + DraftsPath, _gw.Calls);
        var stored = Assert.Single(_drafts.GetAll(Acc));
        Assert.False(stored.Dirty);
        Assert.Equal(1u, stored.GmailUid);
    }

    [Fact]
    public async Task Flush_stops_at_the_first_failure_so_offline_does_not_wait_per_draft()
    {
        await _svc.SaveAsync(Snap("一通目"), uploadNow: false);
        await _svc.SaveAsync(Snap("二通目"), uploadNow: false);
        _gw.Offline = true;
        _gw.Calls.Clear();

        await _svc.FlushDirtyAsync(Acc);

        Assert.Equal(1, _gw.Calls.Count(c => c == "ListFolders"));
        Assert.All(_drafts.GetAll(Acc), d => Assert.True(d.Dirty));
    }

    [Fact]
    public async Task Update_appends_the_new_version_before_deleting_the_old_one()
    {
        var first = await _svc.SaveAsync(Snap(), uploadNow: true);
        _gw.Calls.Clear();

        var second = await _svc.SaveAsync(Snap("会議のご案内(修正)", first.LocalId), uploadNow: true);

        Assert.Equal(first.LocalId, second.LocalId);
        Assert.Single(_drafts.GetAll(Acc));
        var append = _gw.Calls.IndexOf("Append:" + DraftsPath);
        var delete = _gw.Calls.IndexOf("Delete:" + DraftsPath);
        Assert.True(append >= 0 && delete > append, "追加のあとに古い版を消す");
    }

    [Fact]
    public async Task First_upload_does_not_delete_anything()
    {
        await _svc.SaveAsync(Snap(), uploadNow: true);
        Assert.DoesNotContain(_gw.Calls, c => c.StartsWith("Delete:"));
    }

    [Fact]
    public async Task Old_uid_is_found_in_the_cache_by_message_id_when_the_row_has_none()
    {
        _gw.Offline = true;
        var row = await _svc.SaveAsync(Snap(), uploadNow: true); // 行に UID は無い
        _gw.Offline = false;
        _gw.Calls.Clear();

        // 同期で、この下書きの Message-ID を持つメールが Drafts に入ってきた
        _mail.Upsert(Acc, new MailMessage { GmailMsgId = 100, MessageIdHeader = "<" + DraftService.MessageIdOf(row.LocalId) + ">" },
            new[] { new MessageFolder(100, FolderKind.Drafts, 7, 1) });

        await _svc.FlushDirtyAsync(Acc);

        Assert.Contains("Delete:" + DraftsPath, _gw.Calls);
    }

    [Fact]
    public void Message_id_depends_only_on_the_row_id()
    {
        Assert.Equal(DraftService.MessageIdOf(42), DraftService.MessageIdOf(42));
        Assert.NotEqual(DraftService.MessageIdOf(42), DraftService.MessageIdOf(43));
        Assert.EndsWith("@parent-mail.local", DraftService.MessageIdOf(42));
    }

    [Fact]
    public async Task Unfinished_recipient_is_kept_locally_and_does_not_block_the_upload()
    {
        var snap = Snap() with { To = new[] { "yama", "ok@example.com" } };

        var row = await _svc.SaveAsync(snap, uploadNow: true);

        Assert.False(row.Dirty);
        Assert.Equal(new[] { "yama", "ok@example.com" }, Assert.Single(_drafts.GetAll(Acc)).To);
    }

    [Fact]
    public async Task Drafts_folder_missing_leaves_the_row_dirty()
    {
        _gw.Folders.Clear();
        var row = await _svc.SaveAsync(Snap(), uploadNow: true);
        Assert.True(row.Dirty);
    }

    // ---- 添付 ----

    [Fact]
    public async Task Attachments_are_copied_under_the_draft_folder_and_keep_japanese_names()
    {
        var src = MakeFile("見積書.txt", "中身");
        var info = new AttachmentInfo { FileName = "見積書 令和7年.txt", ContentType = "text/plain", Size = 6, LocalPath = src };

        var row = await _svc.SaveAsync(Snap("添付つき", null, info), uploadNow: false);

        var saved = Assert.Single(row.Attachments);
        Assert.Equal("見積書 令和7年.txt", saved.FileName);
        Assert.False(Path.IsPathRooted(saved.LocalPath));
        var full = Path.Combine(_root, "データ", "accounts", Acc, saved.LocalPath!);
        Assert.Equal("中身", File.ReadAllText(full));
        Assert.StartsWith(Path.Combine("drafts", row.LocalId.ToString()), saved.LocalPath);
    }

    [Fact]
    public async Task Saving_again_from_the_draft_folder_itself_does_not_lose_the_attachment()
    {
        var src = MakeFile("資料.txt", "内容");
        var first = await _svc.SaveAsync(Snap("a", null, new AttachmentInfo { FileName = "資料.txt", Size = 4, LocalPath = src }), uploadNow: false);

        // 開き直した窓は相対パスの添付を持ったまま保存する
        var second = await _svc.SaveAsync(Snap("b", first.LocalId, first.Attachments.ToArray()), uploadNow: false);

        var full = Path.Combine(_root, "データ", "accounts", Acc, second.Attachments.Single().LocalPath!);
        Assert.Equal("内容", File.ReadAllText(full));
    }

    // ---- 削除 ----

    [Fact]
    public async Task Delete_removes_the_row_and_the_gmail_copy()
    {
        var row = await _svc.SaveAsync(Snap(), uploadNow: true);
        _gw.Calls.Clear();

        await _svc.DeleteAsync(row.LocalId);

        Assert.Empty(_drafts.GetAll(Acc));
        Assert.Contains("Delete:" + DraftsPath, _gw.Calls);
    }

    [Fact]
    public async Task Delete_of_a_local_only_draft_does_not_touch_gmail()
    {
        var row = await _svc.SaveAsync(Snap(), uploadNow: false);
        await _svc.DeleteAsync(row.LocalId);

        Assert.Empty(_drafts.GetAll(Acc));
        Assert.Empty(_gw.Calls);
    }

    [Fact]
    public async Task Delete_offline_still_removes_the_local_row()
    {
        var row = await _svc.SaveAsync(Snap(), uploadNow: true);
        _gw.Offline = true;

        await _svc.DeleteAsync(row.LocalId);

        Assert.Empty(_drafts.GetAll(Acc));
    }

    // ---- 編集用の初期値 ----

    [Fact]
    public async Task Open_for_edit_returns_the_saved_fields()
    {
        var src = MakeFile("写真.jpg", "x");
        var row = await _svc.SaveAsync(Snap("下書きの件名", null, new AttachmentInfo { FileName = "写真.jpg", Size = 1, LocalPath = src }), uploadNow: false);

        var init = await _svc.OpenForEditAsync(row.LocalId);

        Assert.NotNull(init);
        Assert.Equal(row.LocalId, init!.SourceDraftId);
        Assert.Equal(Acc, init.AccountId);
        Assert.Equal("下書きの件名", init.Subject);
        Assert.Equal(new[] { "山田 太郎 <taro@example.com>" }, init.To);
        Assert.Equal("写真.jpg", init.Attachments.Single().FileName);
    }

    [Fact]
    public async Task Open_for_edit_of_an_unknown_draft_returns_null()
        => Assert.Null(await _svc.OpenForEditAsync(999));

    private void AddGmailDraft(long id, uint uid, BodyState state = BodyState.Fetched)
        => _mail.Upsert(Acc, new MailMessage
        {
            GmailMsgId = id, MessageIdHeader = "<gmail-" + id + "@mail.gmail.com>", Subject = "Gmail の下書き",
            To = new[] { "花子 <hanako@example.jp>" }, Cc = new[] { "c@example.com" }, BodyState = state, BodyText = "書きかけの本文",
        }, new[] { new MessageFolder(id, FolderKind.Drafts, uid, 1) });

    [Fact]
    public async Task Open_by_message_creates_a_local_row_linked_to_the_gmail_uid_and_reuses_it_next_time()
    {
        AddGmailDraft(100, 7);

        var init = await _svc.OpenForEditByMessageAsync(Acc, 100);

        Assert.NotNull(init);
        Assert.Equal("Gmail の下書き", init!.Subject);
        Assert.Equal("書きかけの本文", init.Body);
        Assert.Equal(new[] { "c@example.com" }, init.Cc);
        var row = Assert.Single(_drafts.GetAll(Acc));
        Assert.Equal(7u, row.GmailUid);
        Assert.False(row.Dirty);
        Assert.Equal(row.LocalId, init.SourceDraftId);

        var again = await _svc.OpenForEditByMessageAsync(Acc, 100);
        Assert.Equal(init.SourceDraftId, again!.SourceDraftId);
        Assert.Single(_drafts.GetAll(Acc));
    }

    [Fact]
    public async Task Editing_a_gmail_draft_replaces_the_old_uid_when_saved()
    {
        AddGmailDraft(100, 7);
        var init = await _svc.OpenForEditByMessageAsync(Acc, 100);
        _gw.Calls.Clear();

        await _svc.SaveAsync(Snap("直した件名", init!.SourceDraftId), uploadNow: true);

        Assert.Contains("Delete:" + DraftsPath, _gw.Calls);
        Assert.True(_gw.Calls.IndexOf("Append:" + DraftsPath) < _gw.Calls.IndexOf("Delete:" + DraftsPath));
    }

    [Fact]
    public async Task Open_by_message_fetches_a_missing_body_and_gives_up_when_offline()
    {
        AddGmailDraft(100, 7, BodyState.None);
        _body.Result = BodyFetchResult.Offline;

        Assert.Null(await _svc.OpenForEditByMessageAsync(Acc, 100));
        Assert.Contains(100L, _body.Requested);
        Assert.Empty(_drafts.GetAll(Acc));
    }

    [Fact]
    public async Task Open_by_message_gives_up_when_an_attachment_cannot_be_fetched()
    {
        AddGmailDraft(100, 7);
        _mail.AddAttachment(Acc, new AttachmentInfo { Id = 5, GmailMsgId = 100, FileName = "報告書.pdf", Size = 10 });
        _attachments.Result = new AttachmentFetchResult(AttachmentFetchStatus.Offline, null);

        Assert.Null(await _svc.OpenForEditByMessageAsync(Acc, 100));
        Assert.Empty(_drafts.GetAll(Acc));
    }

    [Fact]
    public async Task Open_by_message_brings_the_attachments_into_the_local_draft()
    {
        AddGmailDraft(100, 7);
        var file = MakeFile("fetched.bin", "pdf");
        _mail.AddAttachment(Acc, new AttachmentInfo { Id = 5, GmailMsgId = 100, FileName = "報告書.pdf", Size = 3 });
        _attachments.Result = new AttachmentFetchResult(AttachmentFetchStatus.Ready, file);

        var init = await _svc.OpenForEditByMessageAsync(Acc, 100);

        var a = Assert.Single(init!.Attachments);
        Assert.Equal("報告書.pdf", a.FileName);
        Assert.Equal("pdf", File.ReadAllText(Path.Combine(_root, "データ", "accounts", Acc, a.LocalPath!)));
    }
}
