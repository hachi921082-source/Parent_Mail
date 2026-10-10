using Microsoft.Data.Sqlite;
using ParentMail.Core.Data;
using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;

namespace ParentMail.Tests.AccountsAuth;

// アカウント行の保存（settings.db）のテスト。日本語を含むパスで実際に SQLite を開く。
public sealed class AccountRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "親メール_" + Guid.NewGuid().ToString("N"));

    private AccountRepository NewRepo()
    {
        var db = new SettingsDb(Path.Combine(_dir, "設定", "settings.db"));
        db.Migrate();
        return new AccountRepository(db);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [Fact]
    public void Add_ThenGetAll_ReturnsInSortOrder_WithJapaneseName()
    {
        var repo = NewRepo();
        repo.Add(new Account("a1", "one@gmail.com", "お母さん", 0));
        repo.Add(new Account("a2", "two@gmail.com", "お父さん", 0));

        var all = repo.GetAll();
        Assert.Equal(new[] { "お母さん", "お父さん" }, all.Select(a => a.DisplayName));
        Assert.True(all[0].SortOrder < all[1].SortOrder);
        Assert.Equal("two@gmail.com", repo.Get("a2")!.Email);
        Assert.Null(repo.Get("none"));
    }

    [Fact]
    public void Rename_ChangesDisplayName()
    {
        var repo = NewRepo();
        repo.Add(new Account("a1", "one@gmail.com", "old", 0));
        repo.Rename("a1", "新しい名前");
        Assert.Equal("新しい名前", repo.Get("a1")!.DisplayName);
    }

    [Fact]
    public void Delete_RemovesRow()
    {
        var repo = NewRepo();
        repo.Add(new Account("a1", "one@gmail.com", "x", 0));
        repo.Delete("a1");
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Persists_AcrossInstances()
    {
        NewRepo().Add(new Account("a1", "one@gmail.com", "x", 0));
        Assert.Single(NewRepo().GetAll());
    }

    [Fact]
    public void Add_SameEmailDifferentCase_Throws()
    {
        var repo = NewRepo();
        repo.Add(new Account("a1", "One@Gmail.com", "x", 0));
        Assert.ThrowsAny<Exception>(() => repo.Add(new Account("a2", "one@gmail.com", "y", 0)));
    }
}
