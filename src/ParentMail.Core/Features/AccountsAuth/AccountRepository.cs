using ParentMail.Core.Data;
using ParentMail.Core.Models;

namespace ParentMail.Core.Features.AccountsAuth;

/// <summary>settings.db の accounts テーブルを読み書きする。パスワードは扱わない。</summary>
public sealed class AccountRepository : IAccountRepository
{
    private readonly ISettingsDb _db;

    public AccountRepository(ISettingsDb db) => _db = db;

    public IReadOnlyList<Account> GetAll()
    {
        using var conn = _db.Open();
        using var cmd = DbUtil.Cmd(conn, "SELECT id, email, display_name, sort_order FROM accounts ORDER BY sort_order, created_utc;");
        using var r = cmd.ExecuteReader();
        var list = new List<Account>();
        while (r.Read()) list.Add(Read(r));
        return list;
    }

    public Account? Get(string accountId)
    {
        using var conn = _db.Open();
        using var cmd = DbUtil.Cmd(conn, "SELECT id, email, display_name, sort_order FROM accounts WHERE id=$id;", ("$id", accountId));
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    /// <summary>末尾に追加する（SortOrder は既存の最大値 + 1 に振り直す）。同じアドレス（大文字小文字無視）は例外。</summary>
    public Account Add(Account account)
    {
        using var conn = _db.Open();
        var max = DbUtil.Scalar(conn, "SELECT MAX(sort_order) FROM accounts;");
        var order = max is null ? 0 : Convert.ToInt32(max) + 1;
        DbUtil.Run(conn,
            "INSERT INTO accounts(id, email, display_name, sort_order, created_utc) VALUES($id, $email, $name, $order, $created);",
            ("$id", account.Id), ("$email", account.Email), ("$name", account.DisplayName), ("$order", order),
            ("$created", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        return account with { SortOrder = order };
    }

    public void Rename(string accountId, string displayName)
    {
        using var conn = _db.Open();
        DbUtil.Run(conn, "UPDATE accounts SET display_name=$name WHERE id=$id;", ("$name", displayName), ("$id", accountId));
    }

    public void Delete(string accountId)
    {
        using var conn = _db.Open();
        DbUtil.Run(conn, "DELETE FROM accounts WHERE id=$id;", ("$id", accountId));
    }

    private static Account Read(Microsoft.Data.Sqlite.SqliteDataReader r)
        => new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3));
}
