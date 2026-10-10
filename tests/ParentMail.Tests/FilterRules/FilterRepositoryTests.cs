using Microsoft.Data.Sqlite;
using ParentMail.Core.Data;
using ParentMail.Core.Models;

namespace ParentMail.Tests.FilterRules;

// settings.db の filter_rules を使う実リポジトリのテスト（日本語を含む保存先と条件）。
public sealed class FilterRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "振り分け_" + Guid.NewGuid().ToString("N"));
    private readonly SettingsDb _db;
    private readonly FilterRepository _repo;

    public FilterRepositoryTests()
    {
        _db = new SettingsDb(Path.Combine(_dir, "設定", "settings.db"));
        _db.Migrate();
        AddAccount("a1");
        AddAccount("a2");
        _repo = new FilterRepository(_db);
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

    private FilterRule Add(string account, string subject, FilterAction action = FilterAction.MoveToTrash)
        => _repo.Save(new FilterRule { AccountId = account, SubjectContains = subject, Action = action });

    [Fact]
    public void Save_new_rules_append_in_order_and_roundtrip_japanese_text()
    {
        var a = _repo.Save(new FilterRule
        {
            AccountId = "a1", SenderContains = "山田商事", SubjectContains = "請求書", BodyContains = "お振込み", Action = FilterAction.MoveToSpam,
        });
        var b = Add("a1", "二番目");

        Assert.NotEqual(0, a.Id);
        Assert.Equal(0, a.SortOrder);
        Assert.Equal(1, b.SortOrder);
        var list = _repo.GetAll("a1");
        Assert.Equal(new[] { a.Id, b.Id }, list.Select(r => r.Id).ToArray());
        Assert.Equal("山田商事", list[0].SenderContains);
        Assert.Equal("お振込み", list[0].BodyContains);
        Assert.Equal(FilterAction.MoveToSpam, list[0].Action);
        Assert.Null(list[1].SenderContains);
        Assert.True(list[1].Enabled);
    }

    [Fact]
    public void Rules_are_per_account()
    {
        Add("a1", "x");
        Add("a2", "y");
        Assert.Equal("x", Assert.Single(_repo.GetAll("a1")).SubjectContains);
        Assert.Equal("y", Assert.Single(_repo.GetAll("a2")).SubjectContains);
        Assert.Equal(0, _repo.GetAll("a2")[0].SortOrder);
    }

    [Fact]
    public void Update_changes_fields_and_keeps_sort_order()
    {
        var a = Add("a1", "一番");
        Add("a1", "二番");
        var saved = _repo.Save(a with { Enabled = false, SubjectContains = "一番目", Action = FilterAction.MarkRead });

        Assert.False(saved.Enabled);
        var first = _repo.GetAll("a1")[0];
        Assert.Equal(a.Id, first.Id);
        Assert.False(first.Enabled);
        Assert.Equal("一番目", first.SubjectContains);
        Assert.Equal(FilterAction.MarkRead, first.Action);
        Assert.Equal(0, first.SortOrder);
    }

    [Fact]
    public void Reorder_persists_across_reopen()
    {
        var a = Add("a1", "A");
        var b = Add("a1", "B");
        var c = Add("a1", "C");

        _repo.Reorder("a1", new[] { c.Id, a.Id, b.Id });

        var reopened = new FilterRepository(new SettingsDb(Path.Combine(_dir, "設定", "settings.db")));
        var list = reopened.GetAll("a1");
        Assert.Equal(new[] { c.Id, a.Id, b.Id }, list.Select(r => r.Id).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, list.Select(r => r.SortOrder).ToArray());
        // 並べ替えのあとの追加は末尾
        Assert.Equal(3, Add("a1", "D").SortOrder);
    }

    [Fact]
    public void Reorder_does_not_touch_other_account()
    {
        var a = Add("a1", "A");
        var other = Add("a2", "Z");
        _repo.Reorder("a1", new[] { other.Id, a.Id });
        Assert.Equal(0, _repo.GetAll("a2")[0].SortOrder);
    }

    [Fact]
    public void Remove_deletes_and_account_delete_cascades()
    {
        var a = Add("a1", "A");
        Add("a1", "B");
        _repo.Remove(a.Id);
        Assert.Single(_repo.GetAll("a1"));

        using (var c = _db.Open())
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM accounts WHERE id='a1'";
            cmd.ExecuteNonQuery();
        }
        Assert.Empty(_repo.GetAll("a1"));
    }
}
