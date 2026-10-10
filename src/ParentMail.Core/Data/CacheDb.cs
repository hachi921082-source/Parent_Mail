using Microsoft.Data.Sqlite;

namespace ParentMail.Core.Data;

/// <summary>アカウント別 cache.db の接続工場。開くたびにフォルダ作成・WAL・マイグレーションを済ませる。</summary>
public sealed class CacheDb : ICacheDb
{
    private readonly CachePaths _paths;

    public CacheDb(CachePaths paths) => _paths = paths;

    public SqliteConnection Open(string accountId)
    {
        Directory.CreateDirectory(_paths.AccountDir(accountId));
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = _paths.DbFile(accountId),
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
        }.ToString();
        var c = new SqliteConnection(cs);
        try
        {
            c.Open();
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
                cmd.ExecuteNonQuery();
            }
            CacheMigrations.Migrate(c);
            return c;
        }
        catch
        {
            c.Dispose();
            throw;
        }
    }
}
