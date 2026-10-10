using Microsoft.Data.Sqlite;
using ParentMail.Core.Data;
using ParentMail.Core.Models;

namespace ParentMail.Tests.BlockList;

// settings.db の block_entries を使う実リポジトリのテスト（日本語を含む保存先）。
public sealed class BlockRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "受信拒否_" + Guid.NewGuid().ToString("N"));
    private readonly SettingsDb _db;
    private readonly BlockRepository _repo;

    public BlockRepositoryTests()
    {
        _db = new SettingsDb(Path.Combine(_dir, "設定", "settings.db"));
        _db.Migrate();
        AddAccount("a1");
        AddAccount("a2");
        _repo = new BlockRepository(_db);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private void AddAccount(string id)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO accounts(id,email,display_name,sort_order,created_utc) VALUES($i,$e,'母',0,0)";
        cmd.Parameters.AddWithValue("$i", id);
        cmd.Parameters.AddWithValue("$e", id + "@gmail.com");
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void Add_get_remove_roundtrip_per_account()
    {
        var a = _repo.Add("a1", BlockKind.Address, "taro@example.com");
        _repo.Add("a1", BlockKind.Domain, "example.org");
        _repo.Add("a2", BlockKind.Domain, "example.org");

        var list = _repo.GetAll("a1");
        Assert.Equal(2, list.Count);
        Assert.Equal(BlockKind.Address, list[0].Kind);
        Assert.Equal("example.org", list[1].Value);
        Assert.Single(_repo.GetAll("a2"));

        _repo.Remove(a.Id);
        Assert.Single(_repo.GetAll("a1"));
    }

    [Fact]
    public void Duplicate_in_same_account_throws_but_other_account_is_fine()
    {
        _repo.Add("a1", BlockKind.Domain, "example.com");
        Assert.Throws<DuplicateBlockEntryException>(() => _repo.Add("a1", BlockKind.Domain, "EXAMPLE.com"));
        _repo.Add("a2", BlockKind.Domain, "example.com");
        _repo.Add("a1", BlockKind.Address, "example.com");
    }

    [Fact]
    public void Entries_are_removed_with_the_account()
    {
        _repo.Add("a1", BlockKind.Domain, "example.com");
        using (var c = _db.Open())
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM accounts WHERE id='a1'";
            cmd.ExecuteNonQuery();
        }
        Assert.Empty(_repo.GetAll("a1"));
    }
}
