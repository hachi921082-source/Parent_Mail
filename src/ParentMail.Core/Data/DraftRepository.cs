using ParentMail.Core.Models;
using static ParentMail.Core.Data.DbUtil;

namespace ParentMail.Core.Data;

/// <summary>
/// ローカル下書き（cache.db の drafts）。添付のコピーは drafts\&lt;id&gt;\&lt;n&gt;.bin（作成は呼び出し側）。ID の規則は LocalIds を参照。
/// </summary>
public sealed class DraftRepository : IDraftRepository
{
    private readonly ICacheDb _db;
    private readonly CachePaths _paths;
    private readonly IClock? _clock;

    public DraftRepository(ICacheDb db, CachePaths paths, IClock? clock = null)
    {
        _db = db;
        _paths = paths;
        _clock = clock;
    }

    public IReadOnlyList<DraftItem> GetAll(string accountId)
    {
        using var c = _db.Open(accountId);
        using var cmd = Cmd(c, "SELECT id, payload_json, gmail_uid, dirty FROM drafts ORDER BY updated_utc DESC, id DESC");
        using var r = cmd.ExecuteReader();
        var list = new List<DraftItem>();
        while (r.Read())
        {
            var p = MailPayload.FromJson(r.GetString(1));
            list.Add(new DraftItem
            {
                LocalId = r.GetInt64(0), AccountId = accountId,
                To = p.To, Cc = p.Cc, Bcc = p.Bcc, Subject = p.Subject, Body = p.Body, Attachments = p.Attachments,
                ReplyHeaders = p.ReplyHeaders,
                GmailUid = r.IsDBNull(2) ? null : (uint)r.GetInt64(2),
                Dirty = r.GetInt32(3) != 0,
            });
        }
        return list;
    }

    /// <summary>LocalId が 0 なら新規採番、それ以外は同じ ID の行を更新（無ければその ID で作る）。保存後の下書きを返す。</summary>
    public DraftItem Save(DraftItem draft)
    {
        using var c = _db.Open(draft.AccountId);
        using var tx = Begin(c);
        var id = draft.LocalId != 0 ? draft.LocalId : LocalIds.Next(c, "drafts", draft.AccountId);
        var payload = new MailPayload(draft.To, draft.Cc, draft.Bcc, draft.Subject, draft.Body, draft.Attachments, draft.ReplyHeaders);
        Run(c,
            """
            INSERT INTO drafts(id, payload_json, attachment_dir, gmail_uid, dirty, updated_utc) VALUES(@id, @p, @dir, @uid, @dirty, @now)
            ON CONFLICT(id) DO UPDATE SET payload_json = excluded.payload_json, gmail_uid = excluded.gmail_uid,
                dirty = excluded.dirty, updated_utc = excluded.updated_utc
            """,
            ("@id", id), ("@p", payload.ToJson()), ("@dir", Path.Combine("drafts", id.ToString())),
            ("@uid", draft.GmailUid is { } u ? (long)u : null), ("@dirty", draft.Dirty ? 1 : 0),
            ("@now", ToUnix(_clock?.UtcNow ?? DateTime.UtcNow)));
        tx.Commit();
        return draft with { LocalId = id };
    }

    public void Remove(long localId)
    {
        foreach (var accountId in LocalIds.Candidates(_paths, localId))
        {
            int n;
            using (var c = _db.Open(accountId)) n = Run(c, "DELETE FROM drafts WHERE id = @id", ("@id", localId));
            if (n == 0) continue;
            var dir = Path.Combine(_paths.AccountDir(accountId), "drafts", localId.ToString());
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            return;
        }
    }
}
