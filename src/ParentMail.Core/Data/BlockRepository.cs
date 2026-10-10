using ParentMail.Core.Models;
using static ParentMail.Core.Data.DbUtil;

namespace ParentMail.Core.Data;

/// <summary>同じ種類・同じ値の受信拒否がすでにある。</summary>
public sealed class DuplicateBlockEntryException : Exception
{
    public DuplicateBlockEntryException(string value) : base("duplicate block entry") => Value = value;
    public string Value { get; }
}

// settings.db の block_entries を読み書きする。アカウント削除時は FK の連鎖削除で消える。
public sealed class BlockRepository : IBlockRepository
{
    private readonly ISettingsDb _db;

    public BlockRepository(ISettingsDb db) => _db = db;

    public IReadOnlyList<BlockEntry> GetAll(string accountId)
    {
        using var conn = _db.Open();
        return Load(conn, accountId);
    }

    public BlockEntry Add(string accountId, BlockKind kind, string value)
    {
        using var conn = _db.Open();
        using var tx = Begin(conn);
        var key = TextNormalizer.NormalizeKey(value);
        if (Load(conn, accountId).Any(e => e.Kind == kind && TextNormalizer.NormalizeKey(e.Value) == key))
            throw new DuplicateBlockEntryException(value);
        Run(conn, "INSERT INTO block_entries(account_id,kind,value) VALUES($a,$k,$v)",
            ("$a", accountId), ("$k", (int)kind), ("$v", value));
        var id = Convert.ToInt64(Scalar(conn, "SELECT last_insert_rowid()"));
        tx.Commit();
        return new BlockEntry(id, accountId, kind, value);
    }

    public void Remove(long id)
    {
        using var conn = _db.Open();
        Run(conn, "DELETE FROM block_entries WHERE id=$i", ("$i", id));
    }

    private static List<BlockEntry> Load(Microsoft.Data.Sqlite.SqliteConnection conn, string accountId)
    {
        var result = new List<BlockEntry>();
        using var cmd = Cmd(conn, "SELECT id,kind,value FROM block_entries WHERE account_id=$a ORDER BY id", ("$a", accountId));
        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(new BlockEntry(r.GetInt64(0), accountId, (BlockKind)r.GetInt32(1), r.GetString(2)));
        return result;
    }
}
