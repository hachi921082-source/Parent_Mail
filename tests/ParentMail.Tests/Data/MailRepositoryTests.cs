using System.Text;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Models;
using static ParentMail.Tests.Data.CacheFixture;

namespace ParentMail.Tests.Data;

// sync-cache PLAN-1 の表示規則とリポジトリの確認（日本語を含む一時パス）。
public sealed class MailRepositoryTests : IDisposable
{
    private readonly CacheFixture _f = new();

    public void Dispose() => _f.Dispose();

    private IReadOnlyList<long> Ids(FolderKind folder) => _f.Mail.GetList(AccountId, folder).Select(m => m.GmailMsgId).ToList();

    [Fact]
    public void Inbox_excludes_messages_also_in_trash_or_spam()
    {
        _f.Add(1, FolderKind.Inbox, FolderKind.AllMail);
        _f.Add(2, FolderKind.Inbox, FolderKind.Trash, FolderKind.AllMail);
        _f.Add(3, FolderKind.Inbox, FolderKind.Spam);
        Assert.Equal(new long[] { 1 }, Ids(FolderKind.Inbox));
    }

    [Fact]
    public void Sent_and_drafts_apply_the_same_exclusion()
    {
        _f.Add(1, FolderKind.Sent);
        _f.Add(2, FolderKind.Sent, FolderKind.Trash);
        _f.Add(3, FolderKind.Drafts);
        _f.Add(4, FolderKind.Drafts, FolderKind.Spam);
        Assert.Equal(new long[] { 1 }, Ids(FolderKind.Sent));
        Assert.Equal(new long[] { 3 }, Ids(FolderKind.Drafts));
    }

    [Fact]
    public void Trash_and_spam_views_list_their_own_members()
    {
        _f.Add(1, FolderKind.Inbox, FolderKind.Trash);
        _f.Add(2, FolderKind.Spam);
        _f.Add(3, FolderKind.Inbox);
        Assert.Equal(new long[] { 1 }, Ids(FolderKind.Trash));
        Assert.Equal(new long[] { 2 }, Ids(FolderKind.Spam));
    }

    [Fact]
    public void AllMail_only_message_is_hidden_from_every_view()
    {
        _f.Add(1, FolderKind.AllMail);
        foreach (var k in Enum.GetValues<FolderKind>()) Assert.Empty(Ids(k));
        // 保存はされており、取得も検索もできる
        Assert.NotNull(_f.Mail.Get(AccountId, 1));
        Assert.Single(_f.Mail.Search(AccountId, TextNormalizer.NormalizeKey("件名"), 10));
    }

    [Fact]
    public void Message_in_several_folders_is_stored_once()
    {
        _f.Add(1, FolderKind.Inbox, FolderKind.AllMail);
        _f.Mail.AddMembership(AccountId, In(1, FolderKind.Sent, 7));
        using var c = _f.Db.Open(AccountId);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT (SELECT COUNT(*) FROM messages), (SELECT COUNT(*) FROM message_folders)";
        using var r = cmd.ExecuteReader();
        Assert.True(r.Read());
        Assert.Equal(1, r.GetInt32(0));
        Assert.Equal(3, r.GetInt32(1));
        Assert.Equal(3, _f.Mail.GetMemberships(AccountId, 1).Count);
    }

    [Fact]
    public void Upsert_merges_memberships_and_keeps_fetched_body_on_header_update()
    {
        _f.Add(1, FolderKind.Inbox);
        _f.Mail.SetBody(AccountId, 1, "本文です", null, null);
        _f.Mail.Upsert(AccountId, Msg(1, read: true), new[] { In(1, FolderKind.AllMail, 9) });
        var m = _f.Mail.Get(AccountId, 1)!;
        Assert.True(m.IsRead);
        Assert.Equal(BodyState.Fetched, m.BodyState);
        Assert.Equal("本文です", m.BodyText);
        Assert.Equal(2, _f.Mail.GetMemberships(AccountId, 1).Count);
    }

    [Fact]
    public void RemoveMembership_moves_message_out_of_view_but_keeps_it()
    {
        _f.Add(1, FolderKind.Inbox, FolderKind.AllMail);
        _f.Mail.RemoveMembership(AccountId, 1, FolderKind.Inbox);
        Assert.Empty(Ids(FolderKind.Inbox));
        Assert.NotNull(_f.Mail.Get(AccountId, 1));
    }

    [Fact]
    public void GetList_is_date_descending_with_limit_and_offset()
    {
        for (var i = 1; i <= 5; i++) _f.Add(i, FolderKind.Inbox);
        Assert.Equal(new long[] { 5, 4, 3, 2, 1 }, Ids(FolderKind.Inbox));
        Assert.Equal(new long[] { 4, 3 }, _f.Mail.GetList(AccountId, FolderKind.Inbox, 2, 1).Select(m => m.GmailMsgId));
    }

    [Fact]
    public void GetList_does_not_load_bodies_but_Get_does()
    {
        _f.Add(1, FolderKind.Inbox);
        _f.Mail.SetBody(AccountId, 1, "text", "<p>html</p>", "derived");
        var listed = _f.Mail.GetList(AccountId, FolderKind.Inbox).Single();
        Assert.Equal(BodyState.Fetched, listed.BodyState);
        Assert.Null(listed.BodyText);
        var full = _f.Mail.Get(AccountId, 1)!;
        Assert.Equal("text", full.BodyText);
        Assert.Equal("<p>html</p>", full.BodyHtml);
        Assert.Equal("derived", full.DerivedText);
    }

    [Fact]
    public void ResetFolderUids_drops_uid_mapping_but_keeps_body_and_other_memberships()
    {
        _f.Add(1, FolderKind.Inbox, FolderKind.AllMail);
        _f.Mail.SetBody(AccountId, 1, "残る本文", null, null);
        _f.Mail.SaveFolderState(AccountId, new FolderSyncState(FolderKind.Inbox, 5, 200, 42));
        _f.Mail.ResetFolderUids(AccountId, FolderKind.Inbox);

        Assert.Empty(_f.Mail.GetKnownUids(AccountId, FolderKind.Inbox));
        Assert.Null(_f.Mail.GetFolderState(AccountId, FolderKind.Inbox));
        Assert.Equal("残る本文", _f.Mail.Get(AccountId, 1)!.BodyText);
        Assert.Equal(new[] { FolderKind.AllMail }, _f.Mail.GetMemberships(AccountId, 1).Select(m => m.Folder));
        Assert.Single(_f.Mail.GetKnownUids(AccountId, FolderKind.AllMail));
    }

    [Fact]
    public void GetKnownUids_maps_uid_to_message_per_folder()
    {
        _f.Add(1, FolderKind.Inbox);
        _f.Add(2, FolderKind.Inbox, FolderKind.Sent);
        var inbox = _f.Mail.GetKnownUids(AccountId, FolderKind.Inbox);
        Assert.Equal(1L, inbox[101]);
        Assert.Equal(2L, inbox[102]);
        Assert.Single(_f.Mail.GetKnownUids(AccountId, FolderKind.Sent));
    }

    [Fact]
    public void FolderState_roundtrip_and_membership_uidvalidity()
    {
        Assert.Null(_f.Mail.GetFolderState(AccountId, FolderKind.Inbox));
        _f.Mail.SaveFolderState(AccountId, new FolderSyncState(FolderKind.Inbox, 4_000_000_000u, 17, null));
        var s = _f.Mail.GetFolderState(AccountId, FolderKind.Inbox)!;
        Assert.Equal(4_000_000_000u, s.UidValidity);
        Assert.Equal(17u, s.UidNext);
        Assert.Null(s.HighestModSeq);
        _f.Mail.SaveFolderState(AccountId, s with { HighestModSeq = 123UL });
        Assert.Equal(123UL, _f.Mail.GetFolderState(AccountId, FolderKind.Inbox)!.HighestModSeq);

        _f.Add(1, FolderKind.Inbox);
        Assert.Equal(4_000_000_000u, _f.Mail.GetMemberships(AccountId, 1).Single().UidValidity);
    }

    [Fact]
    public void ImapName_is_kept_across_state_saves()
    {
        _f.Mail.SaveImapName(AccountId, FolderKind.Trash, "[Gmail]/ゴミ箱");
        _f.Mail.SaveFolderState(AccountId, new FolderSyncState(FolderKind.Trash, 1, 1, null));
        Assert.Equal("[Gmail]/ゴミ箱", _f.Mail.GetImapName(AccountId, FolderKind.Trash));
    }

    [Fact]
    public void UnreadCount_excludes_trash_and_spam()
    {
        _f.Add(1, FolderKind.Inbox);                                   // 未読・数える
        _f.Add(2, FolderKind.Inbox, FolderKind.Trash);                 // 数えない
        _f.Add(3, FolderKind.Inbox, FolderKind.Spam);                  // 数えない
        _f.Add(4, FolderKind.Inbox);
        _f.Mail.SetRead(AccountId, 4, true);                           // 既読
        _f.Add(5, FolderKind.AllMail);                                 // 受信トレイ外
        Assert.Equal(1, _f.Mail.UnreadCount(AccountId, FolderKind.Inbox));
        Assert.Equal(0, _f.Mail.UnreadCount(AccountId, FolderKind.Outbox));
    }

    [Fact]
    public void DeleteMessage_removes_rows_and_attachment_files()
    {
        _f.Add(1, FolderKind.Inbox);
        _f.Add(2, FolderKind.Inbox);
        var atts = _f.Mail.SaveAttachments(AccountId, 1, new[]
        {
            new AttachmentInfo { GmailMsgId = 1, PartId = "2", FileName = "請求書.pdf", ContentType = "application/pdf", Size = 3 },
            new AttachmentInfo { GmailMsgId = 1, PartId = "3", FileName = "写真.jpg", ContentType = "image/jpeg", Size = 3 },
        });
        var other = _f.Mail.SaveAttachments(AccountId, 2, new[] { new AttachmentInfo { GmailMsgId = 2, PartId = "2", FileName = "x.txt" } });
        foreach (var a in atts.Concat(other))
        {
            var rel = _f.Files.SaveFile(AccountId, a.Id, new MemoryStream(new byte[] { 1, 2, 3 }));
            _f.Mail.SetAttachmentPath(AccountId, a.Id, rel);
        }
        Assert.All(atts, a => Assert.True(_f.Files.Exists(AccountId, a.Id)));

        _f.Mail.DeleteMessage(AccountId, 1);

        Assert.Null(_f.Mail.Get(AccountId, 1));
        Assert.Empty(_f.Mail.GetMemberships(AccountId, 1));
        Assert.Empty(_f.Mail.GetAttachments(AccountId, 1));
        Assert.All(atts, a => Assert.False(_f.Files.Exists(AccountId, a.Id)));
        Assert.True(_f.Files.Exists(AccountId, other[0].Id)); // 別のメールの添付は残る
        Assert.NotNull(_f.Mail.Get(AccountId, 2));
    }

    [Fact]
    public void SaveAttachments_is_idempotent_per_part_and_keeps_local_path()
    {
        _f.Add(1, FolderKind.Inbox);
        var first = _f.Mail.SaveAttachments(AccountId, 1, new[] { new AttachmentInfo { PartId = "2", FileName = "a.txt", Size = 1 } });
        _f.Mail.SetAttachmentPath(AccountId, first[0].Id, @"attachments\1.bin");
        var again = _f.Mail.SaveAttachments(AccountId, 1, new[] { new AttachmentInfo { PartId = "2", FileName = "a2.txt", Size = 5 } });
        Assert.Single(again);
        Assert.Equal(first[0].Id, again[0].Id);
        Assert.Equal("a2.txt", again[0].FileName);
        Assert.Equal(@"attachments\1.bin", again[0].LocalPath);
    }

    [Fact]
    public void Search_matches_sender_subject_and_body_with_width_and_case_folding()
    {
        _f.Mail.Upsert(AccountId, Msg(1, subject: "ＡＢＣ会議", fromName: "山田"), new[] { In(1, FolderKind.Inbox) });
        _f.Add(2, FolderKind.Inbox);
        _f.Mail.SetBody(AccountId, 2, "本文に Hello が含まれる", null, null);
        Assert.Equal(new long[] { 1 }, Key("abc"));
        Assert.Equal(new long[] { 1 }, Key("山田"));
        Assert.Equal(new long[] { 2 }, Key("hello"));
        Assert.Empty(Key("存在しない"));
        Assert.Empty(Key("100%")); // % と _ は文字として扱う

        IReadOnlyList<long> Key(string q) => _f.Mail.Search(AccountId, TextNormalizer.NormalizeKey(q), 10).Select(m => m.GmailMsgId).ToList();
    }

    [Fact]
    public void Dates_roundtrip_as_utc_seconds()
    {
        var d = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        _f.Mail.Upsert(AccountId, Msg(1, d), new[] { In(1, FolderKind.Inbox) });
        var got = _f.Mail.Get(AccountId, 1)!.DateUtc;
        Assert.Equal(d, got);
        Assert.Equal(DateTimeKind.Utc, got.Kind);
    }

    [Fact]
    public void Accounts_are_isolated_in_separate_db_files()
    {
        _f.Add(1, FolderKind.Inbox);
        Assert.Empty(_f.Mail.GetList(OtherAccountId, FolderKind.Inbox));
        Assert.True(File.Exists(_f.Paths.DbFile(AccountId)));
        Assert.Contains(AccountId, _f.Paths.AccountIds());
    }

    [Fact]
    public void Invalid_account_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => _f.Mail.GetList(@"..\evil", FolderKind.Inbox));
    }

    [Fact]
    public void Contract_gaps_do_not_throw_for_unknown_ids()
    {
        Assert.Null(_f.Mail.Get(AccountId, 999));
        _f.Mail.SetRead(AccountId, 999, true);
        _f.Mail.SetBody(AccountId, 999, "x", null, null);
        _f.Mail.DeleteMessage(AccountId, 999);
        Assert.Empty(_f.Mail.GetAttachments(AccountId, 999));
    }

    [Fact]
    public void AttachmentStore_roundtrip_with_japanese_path()
    {
        var rel = _f.Files.SaveFile(AccountId, 42, new MemoryStream(Encoding.UTF8.GetBytes("中身")));
        Assert.Equal(Path.Combine("attachments", "42.bin"), rel);
        Assert.Contains("親メール_テスト_", _f.Files.GetFullPath(AccountId, 42));
        using (var s = _f.Files.OpenRead(AccountId, 42))
            Assert.Equal("中身", new StreamReader(s, Encoding.UTF8).ReadToEnd());
        _f.Files.SaveFile(AccountId, 42, new MemoryStream(new byte[] { 9 })); // 上書き
        Assert.Equal(1, new FileInfo(_f.Files.GetFullPath(AccountId, 42)).Length);
        _f.Files.Delete(AccountId, 42);
        _f.Files.Delete(AccountId, 42); // 無くてもエラーにならない
        Assert.False(_f.Files.Exists(AccountId, 42));
        Assert.Empty(Directory.GetFiles(_f.Paths.AttachmentsDir(AccountId), "*.tmp"));
    }
}
