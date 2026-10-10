using ParentMail.Core.Data;
using static ParentMail.Core.Data.DbUtil;

namespace ParentMail.Core.Features.SyncCache;

/// <summary>未送信の既読・未読の書き込み 1 件（pending_ops の行）。</summary>
public sealed record PendingOp(long Id, string Op, long GmailMsgId);

/// <summary>同期エンジンが使う cache.db の補助項目（初回同期完了フラグ、未反映の既読操作、既知メール ID）。</summary>
public interface ISyncCacheStore
{
    bool IsInitialSyncDone(string accountId);
    void SetInitialSyncDone(string accountId);
    /// <summary>キャッシュにある全メールの ID。</summary>
    HashSet<long> GetMessageIds(string accountId);
    /// <summary>全メールの既読状態。</summary>
    Dictionary<long, bool> GetReadStates(string accountId);
    /// <summary>そのフォルダに所属し本文が未取得で、sinceUtc 以降のメール（新しい順）。AllMail は他フォルダに属さないものだけ。</summary>
    IReadOnlyList<(long GmailMsgId, uint Uid)> GetBodylessInFolder(string accountId, Models.FolderKind folder, DateTime sinceUtc);
    /// <summary>既読・未読の変更を後で反映するために積む（同じメールの古い操作は置き換える）。</summary>
    void EnqueueSeen(string accountId, long gmailMsgId, bool seen);
    IReadOnlyList<PendingOp> GetPending(string accountId);
    IReadOnlySet<long> GetPendingMessageIds(string accountId);
    void RemovePending(string accountId, long opId);
}

/// <summary>ISyncCacheStore の cache.db 実装（cache_meta と pending_ops を使う）。</summary>
public sealed class SyncCacheStore : ISyncCacheStore
{
    public const string OpSeen = "seen";
    public const string OpUnseen = "unseen";
    private const string InitialKey = "initial_sync_done";

    private readonly ICacheDb _db;

    public SyncCacheStore(ICacheDb db) => _db = db;

    public bool IsInitialSyncDone(string accountId)
    {
        using var c = _db.Open(accountId);
        return Scalar(c, "SELECT value FROM cache_meta WHERE key = @k", ("@k", InitialKey)) as string == "1";
    }

    public void SetInitialSyncDone(string accountId)
    {
        using var c = _db.Open(accountId);
        Run(c, "INSERT INTO cache_meta(key, value) VALUES(@k, '1') ON CONFLICT(key) DO UPDATE SET value = '1'", ("@k", InitialKey));
    }

    public HashSet<long> GetMessageIds(string accountId)
    {
        using var c = _db.Open(accountId);
        using var cmd = Cmd(c, "SELECT gm_msgid FROM messages");
        using var r = cmd.ExecuteReader();
        var set = new HashSet<long>();
        while (r.Read()) set.Add(r.GetInt64(0));
        return set;
    }

    public Dictionary<long, bool> GetReadStates(string accountId)
    {
        using var c = _db.Open(accountId);
        using var cmd = Cmd(c, "SELECT gm_msgid, is_read FROM messages");
        using var r = cmd.ExecuteReader();
        var map = new Dictionary<long, bool>();
        while (r.Read()) map[r.GetInt64(0)] = r.GetInt32(1) != 0;
        return map;
    }

    public IReadOnlyList<(long GmailMsgId, uint Uid)> GetBodylessInFolder(string accountId, Models.FolderKind folder, DateTime sinceUtc)
    {
        using var c = _db.Open(accountId);
        var allMail = (int)Models.FolderKind.AllMail;
        using var cmd = Cmd(c,
            "SELECT m.gm_msgid, mf.uid FROM messages m JOIN message_folders mf ON mf.gm_msgid = m.gm_msgid " +
            "WHERE mf.folder = @f AND m.body_state = 0 AND m.date_utc >= @since " +
            $"AND (@f <> {allMail} OR NOT EXISTS (SELECT 1 FROM message_folders o WHERE o.gm_msgid = m.gm_msgid AND o.folder <> {allMail})) " +
            "ORDER BY m.date_utc DESC, m.gm_msgid DESC",
            ("@f", (int)folder), ("@since", ToUnix(sinceUtc)));
        using var r = cmd.ExecuteReader();
        var list = new List<(long, uint)>();
        while (r.Read()) list.Add((r.GetInt64(0), (uint)r.GetInt64(1)));
        return list;
    }

    public void EnqueueSeen(string accountId, long gmailMsgId, bool seen)
    {
        using var c = _db.Open(accountId);
        using var tx = Begin(c);
        Run(c, "DELETE FROM pending_ops WHERE gm_msgid = @m AND op IN ('seen', 'unseen')", ("@m", gmailMsgId));
        Run(c, "INSERT INTO pending_ops(op, gm_msgid) VALUES(@o, @m)", ("@o", seen ? OpSeen : OpUnseen), ("@m", gmailMsgId));
        tx.Commit();
    }

    public IReadOnlyList<PendingOp> GetPending(string accountId)
    {
        using var c = _db.Open(accountId);
        using var cmd = Cmd(c, "SELECT id, op, gm_msgid FROM pending_ops ORDER BY id");
        using var r = cmd.ExecuteReader();
        var list = new List<PendingOp>();
        while (r.Read()) list.Add(new PendingOp(r.GetInt64(0), r.GetString(1), r.GetInt64(2)));
        return list;
    }

    public IReadOnlySet<long> GetPendingMessageIds(string accountId)
        => GetPending(accountId).Select(p => p.GmailMsgId).ToHashSet();

    public void RemovePending(string accountId, long opId)
    {
        using var c = _db.Open(accountId);
        Run(c, "DELETE FROM pending_ops WHERE id = @i", ("@i", opId));
    }
}
