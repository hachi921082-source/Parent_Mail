using Microsoft.Data.Sqlite;

namespace ParentMail.Core.Data;

// settings.db の接続工場とマイグレーション（PRAGMA user_version）。
// v1 で全機能（アカウント・設定・アドレス帳・拒否・振り分け）のテーブルを作る。
// 以降のスキーマ変更は Migrations に追記する（この連鎖の所有者は settings のみ）。
public sealed class SettingsDb : ISettingsDb
{
    private readonly string _connectionString;

    /// <summary>日本語を含むパスも UTF-16 のまま渡す。親フォルダが無ければ作る。</summary>
    public SettingsDb(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();
    }

    /// <summary>最新バージョン。Migrations の件数と一致する。</summary>
    public static int LatestVersion => Migrations.Length;

    private static readonly string[] Migrations =
    {
        // v1
        """
        CREATE TABLE accounts(
            id TEXT PRIMARY KEY,
            email TEXT NOT NULL UNIQUE COLLATE NOCASE,
            display_name TEXT NOT NULL,
            sort_order INTEGER NOT NULL,
            created_utc INTEGER NOT NULL);
        CREATE TABLE app_settings(
            key TEXT PRIMARY KEY,
            value TEXT);
        CREATE TABLE contacts(
            id INTEGER PRIMARY KEY,
            account_id TEXT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
            name TEXT NOT NULL,
            reading TEXT NOT NULL DEFAULT '',
            memo TEXT NOT NULL DEFAULT '');
        CREATE TABLE contact_emails(
            id INTEGER PRIMARY KEY,
            contact_id INTEGER NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
            email TEXT NOT NULL COLLATE NOCASE);
        CREATE TABLE block_entries(
            id INTEGER PRIMARY KEY,
            account_id TEXT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
            kind INTEGER NOT NULL,
            value TEXT NOT NULL COLLATE NOCASE);
        CREATE TABLE filter_rules(
            id INTEGER PRIMARY KEY,
            account_id TEXT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
            sort_order INTEGER NOT NULL,
            enabled INTEGER NOT NULL,
            sender_contains TEXT NULL,
            subject_contains TEXT NULL,
            body_contains TEXT NULL,
            action INTEGER NOT NULL);
        CREATE INDEX ix_contacts_account ON contacts(account_id);
        CREATE INDEX ix_contact_emails_contact ON contact_emails(contact_id);
        CREATE INDEX ix_contact_emails_email ON contact_emails(email);
        CREATE INDEX ix_block_entries_account ON block_entries(account_id);
        CREATE INDEX ix_filter_rules_account ON filter_rules(account_id, sort_order);
        """,
    };

    /// <summary>user_version が古ければ順に適用する。新しい DB（未来版）はそのまま使う。</summary>
    public void Migrate()
    {
        using var conn = Open();
        var current = Convert.ToInt32(Scalar(conn, "PRAGMA user_version;"));
        for (var v = current; v < Migrations.Length; v++)
        {
            using var tx = conn.BeginTransaction();
            Exec(conn, Migrations[v], tx);
            // PRAGMA はパラメータ化できないため整数を直接埋め込む
            Exec(conn, $"PRAGMA user_version = {v + 1};", tx);
            tx.Commit();
        }
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        // journal_mode=WAL は DB に永続するが、foreign_keys は接続ごとに必要
        Exec(conn, "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;", null);
        return conn;
    }

    private static void Exec(SqliteConnection conn, string sql, SqliteTransaction? tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
}
