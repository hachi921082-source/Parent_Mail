using Microsoft.Data.Sqlite;
using ParentMail.Core.Data;
using static ParentMail.Tests.Data.CacheFixture;

namespace ParentMail.Tests.Data;

public sealed class CacheMigrationTests : IDisposable
{
    private readonly CacheFixture _f = new();

    public void Dispose() => _f.Dispose();

    private static string Scalar(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToString(cmd.ExecuteScalar())!;
    }

    [Fact]
    public void Open_creates_schema_at_current_version_in_wal_mode()
    {
        using var c = _f.Db.Open(AccountId);
        Assert.Equal(CacheMigrations.CurrentVersion, CacheMigrations.GetVersion(c));
        Assert.Equal("wal", Scalar(c, "PRAGMA journal_mode"));
        Assert.Equal("1", Scalar(c, "PRAGMA foreign_keys"));
        foreach (var t in new[] { "folders", "messages", "message_folders", "attachments", "outbox", "drafts", "cache_meta", "pending_ops" })
            Assert.Equal("1", Scalar(c, $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{t}'"));
        Assert.Equal("1", Scalar(c, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'ix_mf_folder_uid'"));
    }

    [Fact]
    public void Reopen_is_idempotent_and_keeps_data()
    {
        using (var c = _f.Db.Open(AccountId))
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO cache_meta(key, value) VALUES('initial_sync_done', '1')";
            cmd.ExecuteNonQuery();
        }
        using var again = _f.Db.Open(AccountId);
        Assert.Equal("1", Scalar(again, "SELECT value FROM cache_meta WHERE key = 'initial_sync_done'"));
        Assert.Equal(CacheMigrations.CurrentVersion, CacheMigrations.GetVersion(again));
    }

    [Fact]
    public void Concurrent_first_open_migrates_once_without_error()
    {
        var errors = new List<Exception>();
        Parallel.For(0, 8, _ =>
        {
            try { using var c = _f.Db.Open(AccountId); }
            catch (Exception ex) { lock (errors) errors.Add(ex); }
        });
        Assert.Empty(errors);
    }

    [Fact]
    public void Database_file_lives_under_account_folder_with_japanese_root()
    {
        using (_f.Db.Open(AccountId)) { }
        Assert.Equal(Path.Combine(_f.Root, "accounts", AccountId, "cache.db"), _f.Paths.DbFile(AccountId));
        Assert.True(File.Exists(_f.Paths.DbFile(AccountId)));
    }

    [Fact]
    public void Deleting_message_cascades_to_memberships_and_attachments()
    {
        _f.Add(1, ParentMail.Core.Models.FolderKind.Inbox);
        _f.Mail.SaveAttachments(AccountId, 1, new[] { new ParentMail.Core.Models.AttachmentInfo { PartId = "2" } });
        using var c = _f.Db.Open(AccountId);
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM messages WHERE gm_msgid = 1";
            cmd.ExecuteNonQuery();
        }
        Assert.Equal("0", Scalar(c, "SELECT COUNT(*) FROM message_folders"));
        Assert.Equal("0", Scalar(c, "SELECT COUNT(*) FROM attachments"));
    }
}
