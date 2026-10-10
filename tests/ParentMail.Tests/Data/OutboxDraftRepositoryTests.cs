using ParentMail.Core.Models;
using static ParentMail.Tests.Data.CacheFixture;

namespace ParentMail.Tests.Data;

public sealed class OutboxDraftRepositoryTests : IDisposable
{
    private readonly CacheFixture _f = new();

    public void Dispose() => _f.Dispose();

    private static OutboxItem NewOutbox(string account, string subject = "宛先確認") => new()
    {
        AccountId = account,
        To = new[] { "a@example.com" },
        Cc = new[] { "b@example.com" },
        Bcc = new[] { "c@example.com" },
        Subject = subject,
        Body = "本文\r\n二行目",
        Attachments = new[] { new AttachmentInfo { Id = 1, FileName = "資料.xlsx", ContentType = "application/vnd.ms-excel", Size = 10, LocalPath = @"outbox\1\0.bin" } },
        ReplyHeaders = new ReplyHeaders("<x@example.com>", "<w@example.com> <x@example.com>"),
        SourceDraftId = 5,
    };

    [Fact]
    public void Outbox_add_assigns_id_and_roundtrips_payload()
    {
        var added = _f.Outbox.Add(NewOutbox(AccountId));
        Assert.NotEqual(0, added.Id);
        var got = Assert.Single(_f.Outbox.GetAll(AccountId));
        Assert.Equal(added.Id, got.Id);
        Assert.Equal(AccountId, got.AccountId);
        Assert.Equal(new[] { "a@example.com" }, got.To);
        Assert.Equal(new[] { "c@example.com" }, got.Bcc);
        Assert.Equal("本文\r\n二行目", got.Body);
        Assert.Equal("資料.xlsx", got.Attachments.Single().FileName);
        Assert.Equal("<x@example.com>", got.ReplyHeaders!.InReplyTo);
        Assert.Equal(5, got.SourceDraftId);
        Assert.Equal(_f.Clock.UtcNow, got.CreatedUtc);
    }

    [Fact]
    public void Outbox_ids_are_unique_across_accounts_and_remove_finds_the_account()
    {
        var a = _f.Outbox.Add(NewOutbox(AccountId));
        var b = _f.Outbox.Add(NewOutbox(OtherAccountId));
        Assert.NotEqual(a.Id, b.Id);

        _f.Outbox.Remove(b.Id);
        Assert.Empty(_f.Outbox.GetAll(OtherAccountId));
        Assert.Single(_f.Outbox.GetAll(AccountId));
    }

    [Fact]
    public void Outbox_update_changes_retry_state_and_remove_deletes_attachment_folder()
    {
        var item = _f.Outbox.Add(NewOutbox(AccountId));
        var dir = Path.Combine(_f.Paths.AccountDir(AccountId), "outbox", item.Id.ToString());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "0.bin"), "x");

        _f.Outbox.Update(item with { RetryCount = 3, FailureNotified = true });
        var got = _f.Outbox.GetAll(AccountId).Single();
        Assert.Equal(3, got.RetryCount);
        Assert.True(got.FailureNotified);

        _f.Outbox.Remove(item.Id);
        Assert.Empty(_f.Outbox.GetAll(AccountId));
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Outbox_is_ordered_oldest_first()
    {
        var first = _f.Outbox.Add(NewOutbox(AccountId, "1通目"));
        _f.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = _f.Outbox.Add(NewOutbox(AccountId, "2通目"));
        Assert.Equal(new[] { first.Id, second.Id }, _f.Outbox.GetAll(AccountId).Select(i => i.Id));
    }

    [Fact]
    public void Draft_save_inserts_then_updates_same_row()
    {
        var saved = _f.Drafts.Save(new DraftItem { AccountId = AccountId, To = new[] { "a@example.com" }, Subject = "下書き", Body = "途中", Dirty = true });
        Assert.NotEqual(0, saved.LocalId);
        _f.Drafts.Save(saved with { Body = "続き", Dirty = false, GmailUid = 4_000_000_000u });

        var got = Assert.Single(_f.Drafts.GetAll(AccountId));
        Assert.Equal("続き", got.Body);
        Assert.False(got.Dirty);
        Assert.Equal(4_000_000_000u, got.GmailUid);
        Assert.Equal(saved.LocalId, got.LocalId);
    }

    [Fact]
    public void Draft_remove_works_by_id_alone_and_deletes_folder()
    {
        var a = _f.Drafts.Save(new DraftItem { AccountId = AccountId, Subject = "A" });
        var b = _f.Drafts.Save(new DraftItem { AccountId = OtherAccountId, Subject = "B" });
        Assert.NotEqual(a.LocalId, b.LocalId);
        var dir = Path.Combine(_f.Paths.AccountDir(OtherAccountId), "drafts", b.LocalId.ToString());
        Directory.CreateDirectory(dir);

        _f.Drafts.Remove(b.LocalId);

        Assert.Empty(_f.Drafts.GetAll(OtherAccountId));
        Assert.Single(_f.Drafts.GetAll(AccountId));
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Remove_unknown_id_is_a_noop()
    {
        _f.Outbox.Remove(123456789);
        _f.Drafts.Remove(123456789);
    }
}
