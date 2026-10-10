using Microsoft.Data.Sqlite;
using ParentMail.Core.Models;
using static ParentMail.Core.Data.DbUtil;

namespace ParentMail.Core.Data;

// settings.db の filter_rules を読み書きする。アカウント削除時は FK の連鎖削除で消える。
// 並びは sort_order の昇順（同じなら id 順）。新しいルールは末尾に入る。
public sealed class FilterRepository : IFilterRepository
{
    private readonly ISettingsDb _db;

    public FilterRepository(ISettingsDb db) => _db = db;

    public IReadOnlyList<FilterRule> GetAll(string accountId)
    {
        using var conn = _db.Open();
        return Load(conn, accountId);
    }

    public FilterRule Save(FilterRule rule)
    {
        using var conn = _db.Open();
        using var tx = Begin(conn);
        if (rule.Id == 0)
        {
            var next = Scalar(conn, "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM filter_rules WHERE account_id=$a",
                ("$a", rule.AccountId));
            var order = Convert.ToInt32(next);
            Run(conn,
                "INSERT INTO filter_rules(account_id,sort_order,enabled,sender_contains,subject_contains,body_contains,action) " +
                "VALUES($a,$o,$e,$s,$j,$b,$x)",
                ("$a", rule.AccountId), ("$o", order), ("$e", rule.Enabled ? 1 : 0),
                ("$s", rule.SenderContains), ("$j", rule.SubjectContains), ("$b", rule.BodyContains), ("$x", (int)rule.Action));
            var id = Convert.ToInt64(Scalar(conn, "SELECT last_insert_rowid()"));
            tx.Commit();
            return rule with { Id = id, SortOrder = order };
        }

        // 更新では並び順は変えない（並べ替えは Reorder だけ）
        Run(conn,
            "UPDATE filter_rules SET enabled=$e,sender_contains=$s,subject_contains=$j,body_contains=$b,action=$x WHERE id=$i",
            ("$e", rule.Enabled ? 1 : 0), ("$s", rule.SenderContains), ("$j", rule.SubjectContains),
            ("$b", rule.BodyContains), ("$x", (int)rule.Action), ("$i", rule.Id));
        var saved = Load(conn, rule.AccountId).FirstOrDefault(r => r.Id == rule.Id) ?? rule;
        tx.Commit();
        return saved;
    }

    public void Remove(long id)
    {
        using var conn = _db.Open();
        Run(conn, "DELETE FROM filter_rules WHERE id=$i", ("$i", id));
    }

    public void Reorder(string accountId, IReadOnlyList<long> orderedIds)
    {
        using var conn = _db.Open();
        using var tx = Begin(conn);
        for (var order = 0; order < orderedIds.Count; order++)
        {
            Run(conn, "UPDATE filter_rules SET sort_order=$o WHERE id=$i AND account_id=$a",
                ("$o", order), ("$i", orderedIds[order]), ("$a", accountId));
        }
        tx.Commit();
    }

    private static List<FilterRule> Load(SqliteConnection conn, string accountId)
    {
        var result = new List<FilterRule>();
        using var cmd = Cmd(conn,
            "SELECT id,sort_order,enabled,sender_contains,subject_contains,body_contains,action FROM filter_rules " +
            "WHERE account_id=$a ORDER BY sort_order, id", ("$a", accountId));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            result.Add(new FilterRule
            {
                Id = r.GetInt64(0),
                AccountId = accountId,
                SortOrder = r.GetInt32(1),
                Enabled = r.GetInt32(2) != 0,
                SenderContains = r.IsDBNull(3) ? null : r.GetString(3),
                SubjectContains = r.IsDBNull(4) ? null : r.GetString(4),
                BodyContains = r.IsDBNull(5) ? null : r.GetString(5),
                Action = (FilterAction)r.GetInt32(6),
            });
        }
        return result;
    }
}
