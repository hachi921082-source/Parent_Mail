using Microsoft.Data.Sqlite;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.Data;

/// <summary>日本語を含む一時フォルダに cache.db を作るテスト基盤。破棄時にフォルダごと消す。</summary>
public sealed class CacheFixture : IDisposable
{
    public const string AccountId = "11111111-aaaa-bbbb-cccc-000000000001";
    public const string OtherAccountId = "22222222-aaaa-bbbb-cccc-000000000002";

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "親メール_テスト_" + Guid.NewGuid().ToString("N"));
    public CachePaths Paths { get; }
    public CacheDb Db { get; }
    public AttachmentStore Files { get; }
    public MailRepository Mail { get; }
    public OutboxRepository Outbox { get; }
    public DraftRepository Drafts { get; }
    public FakeClock Clock { get; } = new();

    public CacheFixture()
    {
        Paths = new CachePaths(Root);
        Db = new CacheDb(Paths);
        Files = new AttachmentStore(Paths);
        Mail = new MailRepository(Db, Files, Clock);
        Outbox = new OutboxRepository(Db, Paths, Clock);
        Drafts = new DraftRepository(Db, Paths, Clock);
    }

    public static MailMessage Msg(long id, DateTime? date = null, bool read = false, string subject = "件名", string fromName = "送信者") => new()
    {
        GmailMsgId = id,
        MessageIdHeader = $"<{id}@example.com>",
        Subject = subject,
        FromAddress = "from@example.com",
        FromName = fromName,
        To = new[] { "to@example.com" },
        DateUtc = date ?? new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(id),
        IsRead = read,
    };

    public static MessageFolder In(long id, FolderKind folder, uint uid = 1) => new(id, folder, uid, 0);

    /// <summary>メールを保存する。memberships の folder 指定は UID を 100 + id にする。</summary>
    public MailMessage Add(long id, params FolderKind[] folders)
    {
        var m = Msg(id);
        Mail.Upsert(AccountId, m, folders.Select(f => In(id, f, (uint)(100 + id))).ToList());
        return m;
    }

    public void Dispose()
    {
        // 接続プールが cache.db を掴んだままだと削除できない
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); } catch (IOException) { }
    }
}
