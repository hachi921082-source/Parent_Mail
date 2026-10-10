using Microsoft.Data.Sqlite;
using ParentMail.Core.Models;
using static ParentMail.Core.Data.DbUtil;

namespace ParentMail.Core.Data;

/// <summary>
/// 送信箱（cache.db の outbox）。添付のコピーは outbox\&lt;id&gt;\&lt;n&gt;.bin に置き、ファイルの作成は呼び出し側（outbox-send / compose）が行う。
/// ここは payload の保存と、行を消すときのフォルダ削除を受け持つ。ID の規則は LocalIds を参照。
/// </summary>
public sealed class OutboxRepository : IOutboxRepository
{
    private readonly ICacheDb _db;
    private readonly CachePaths _paths;
    private readonly IClock? _clock;

    public OutboxRepository(ICacheDb db, CachePaths paths, IClock? clock = null)
    {
        _db = db;
        _paths = paths;
        _clock = clock;
    }

    public IReadOnlyList<OutboxItem> GetAll(string accountId)
    {
        using var c = _db.Open(accountId);
        using var cmd = Cmd(c,
            "SELECT id, payload_json, retry_count, failure_notified, created_utc, source_draft_id FROM outbox ORDER BY created_utc, id");
        using var r = cmd.ExecuteReader();
        var list = new List<OutboxItem>();
        while (r.Read())
        {
            var p = MailPayload.FromJson(r.GetString(1));
            list.Add(new OutboxItem
            {
                Id = r.GetInt64(0), AccountId = accountId,
                To = p.To, Cc = p.Cc, Bcc = p.Bcc, Subject = p.Subject, Body = p.Body, Attachments = p.Attachments,
                ReplyHeaders = p.ReplyHeaders,
                RetryCount = r.GetInt32(2), FailureNotified = r.GetInt32(3) != 0,
                CreatedUtc = FromUnix(r.GetInt64(4)),
                SourceDraftId = r.IsDBNull(5) ? null : r.GetInt64(5),
            });
        }
        return list;
    }

    /// <summary>item.Id は無視して採番し、採番済みの item を返す。CreatedUtc が未設定ならいまの時刻。</summary>
    public OutboxItem Add(OutboxItem item)
    {
        using var c = _db.Open(item.AccountId);
        using var tx = Begin(c);
        var id = LocalIds.Next(c, "outbox", item.AccountId);
        var created = item.CreatedUtc == default ? (_clock?.UtcNow ?? DateTime.UtcNow) : item.CreatedUtc;
        Run(c,
            "INSERT INTO outbox(id, payload_json, attachment_dir, retry_count, failure_notified, created_utc, source_draft_id) " +
            "VALUES(@id, @p, @dir, @rc, @fn, @cr, @sd)",
            ("@id", id), ("@p", ToPayload(item).ToJson()), ("@dir", Path.Combine("outbox", id.ToString())),
            ("@rc", item.RetryCount), ("@fn", item.FailureNotified ? 1 : 0), ("@cr", ToUnix(created)), ("@sd", item.SourceDraftId));
        tx.Commit();
        return item with { Id = id, CreatedUtc = created };
    }

    public void Update(OutboxItem item)
    {
        using var c = _db.Open(item.AccountId);
        Run(c,
            "UPDATE outbox SET payload_json = @p, retry_count = @rc, failure_notified = @fn, source_draft_id = @sd WHERE id = @id",
            ("@id", item.Id), ("@p", ToPayload(item).ToJson()), ("@rc", item.RetryCount),
            ("@fn", item.FailureNotified ? 1 : 0), ("@sd", item.SourceDraftId));
    }

    public void Remove(long id)
    {
        foreach (var accountId in LocalIds.Candidates(_paths, id))
        {
            int n;
            using (var c = _db.Open(accountId)) n = Run(c, "DELETE FROM outbox WHERE id = @id", ("@id", id));
            if (n == 0) continue;
            DeleteDir(Path.Combine(_paths.AccountDir(accountId), "outbox", id.ToString()));
            return;
        }
    }

    private static MailPayload ToPayload(OutboxItem i)
        => new(i.To, i.Cc, i.Bcc, i.Subject, i.Body, i.Attachments, i.ReplyHeaders);

    private static void DeleteDir(string dir)
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}
