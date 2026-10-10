using Microsoft.Data.Sqlite;

namespace ParentMail.Core.Data;

/// <summary>cache.db アクセスの小さな共通部品。</summary>
internal static class DbUtil
{
    /// <summary>日時を UTC の unix 秒にする。Kind 未指定は UTC とみなす（モデルの DateUtc は UTC 前提）。</summary>
    public static long ToUnix(DateTime d)
    {
        var utc = d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : DateTime.SpecifyKind(d, DateTimeKind.Utc);
        return new DateTimeOffset(utc).ToUnixTimeSeconds();
    }

    public static DateTime FromUnix(long s) => DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime;

    public static SqliteCommand Cmd(SqliteConnection c, string sql, params (string Name, object? Value)[] args)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd;
    }

    public static int Run(SqliteConnection c, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = Cmd(c, sql, args);
        return cmd.ExecuteNonQuery();
    }

    public static object? Scalar(SqliteConnection c, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = Cmd(c, sql, args);
        var v = cmd.ExecuteScalar();
        return v is DBNull ? null : v;
    }

    /// <summary>BEGIN IMMEDIATE ～ COMMIT。Commit しないまま破棄すると ROLLBACK する。</summary>
    public static Tx Begin(SqliteConnection c) => new(c);

    public sealed class Tx : IDisposable
    {
        private readonly SqliteConnection _c;
        private bool _done;

        public Tx(SqliteConnection c)
        {
            _c = c;
            Run(c, "BEGIN IMMEDIATE");
        }

        public void Commit()
        {
            Run(_c, "COMMIT");
            _done = true;
        }

        public void Dispose()
        {
            if (!_done) Run(_c, "ROLLBACK");
        }
    }
}
