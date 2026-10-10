using Microsoft.Data.Sqlite;
using ParentMail.Core.Data;
using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Models;

namespace ParentMail.Tests.AddressBook;

// settings.db の連絡先テーブルを使う実リポジトリのテスト（日本語を含む保存先で試す）。
public sealed class ContactRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "アドレス帳_" + Guid.NewGuid().ToString("N"));
    private readonly SettingsDb _db;
    private readonly ContactRepository _repo;

    public ContactRepositoryTests()
    {
        _db = new SettingsDb(Path.Combine(_dir, "設定", "settings.db"));
        _db.Migrate();
        AddAccount("a1", "a1@gmail.com");
        AddAccount("a2", "a2@gmail.com");
        _repo = new ContactRepository(_db);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private void AddAccount(string id, string email)
    {
        using var c = _db.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO accounts(id,email,display_name,sort_order,created_utc) VALUES($i,$e,'x',0,0)";
        cmd.Parameters.AddWithValue("$i", id);
        cmd.Parameters.AddWithValue("$e", email);
        cmd.ExecuteNonQuery();
    }

    private long CountEmails()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM contact_emails";
        return (long)cmd.ExecuteScalar()!;
    }

    private static Contact New(string acc, string name, string reading, params string[] emails)
        => new(0, acc, name, reading, emails, "メモ");

    [Fact]
    public void Add_and_GetAll_roundtrip_japanese()
    {
        var added = _repo.Add(New("a1", "山田 太郎", "やまだ たろう", "taro@example.com", "T2@Example.com"));
        Assert.True(added.Id > 0);
        var got = Assert.Single(_repo.GetAll("a1"));
        Assert.Equal("山田 太郎", got.Name);
        Assert.Equal("やまだ たろう", got.Reading);
        Assert.Equal(new[] { "taro@example.com", "T2@Example.com" }, got.Emails);
        Assert.Equal("メモ", got.Memo);
        Assert.Empty(_repo.GetAll("a2"));
    }

    [Fact]
    public void GetAll_is_sorted_by_reading()
    {
        _repo.Add(New("a1", "山田", "ヤマダ", "y@example.com"));
        _repo.Add(New("a1", "阿部", "あべ", "a@example.com"));
        _repo.Add(New("a1", "無名", "", "n@example.com"));
        Assert.Equal(new[] { "阿部", "山田", "無名" }, _repo.GetAll("a1").Select(c => c.Name).ToArray());
    }

    [Fact]
    public void Add_rejects_duplicate_address_case_insensitive_in_same_account()
    {
        _repo.Add(New("a1", "A", "あ", "taro@example.com"));
        Assert.Throws<DuplicateContactEmailException>(() => _repo.Add(New("a1", "B", "い", "TARO@EXAMPLE.COM")));
        Assert.Throws<DuplicateContactEmailException>(() => _repo.Add(New("a1", "B", "い", "ｔａｒｏ@example.com")));
        Assert.Single(_repo.GetAll("a1"));
    }

    [Fact]
    public void Same_address_allowed_in_other_account()
    {
        _repo.Add(New("a1", "A", "あ", "taro@example.com"));
        _repo.Add(New("a2", "A", "あ", "taro@example.com"));
        Assert.Single(_repo.GetAll("a2"));
    }

    [Fact]
    public void Update_replaces_fields_and_emails_and_allows_own_address()
    {
        var c = _repo.Add(New("a1", "A", "あ", "x@example.com"));
        _repo.Add(New("a1", "B", "い", "y@example.com"));
        _repo.Update(c with { Name = "A2", Emails = new[] { "X@example.com", "z@example.com" } });
        var got = _repo.GetAll("a1").Single(x => x.Id == c.Id);
        Assert.Equal("A2", got.Name);
        Assert.Equal(2, got.Emails.Count);
        Assert.Throws<DuplicateContactEmailException>(() => _repo.Update(c with { Emails = new[] { "y@example.com" } }));
        // 失敗した更新は元の内容を変えない
        Assert.Equal(2, _repo.GetAll("a1").Single(x => x.Id == c.Id).Emails.Count);
    }

    [Fact]
    public void Delete_removes_contact_and_emails()
    {
        var c = _repo.Add(New("a1", "A", "あ", "x@example.com"));
        _repo.Delete(c.Id);
        Assert.Empty(_repo.GetAll("a1"));
        Assert.Equal(0L, CountEmails());
        _repo.Add(New("a1", "B", "い", "x@example.com")); // 削除後は同じアドレスを再登録できる
    }

    [Fact]
    public void FindByEmail_is_case_and_width_insensitive()
    {
        var c = _repo.Add(New("a1", "A", "あ", "Taro@Example.com"));
        Assert.Equal(c.Id, _repo.FindByEmail("a1", "taro@example.com")!.Id);
        Assert.Equal(c.Id, _repo.FindByEmail("a1", " ＴＡＲＯ@example.com ")!.Id);
        Assert.Null(_repo.FindByEmail("a2", "taro@example.com"));
        Assert.Null(_repo.FindByEmail("a1", "other@example.com"));
    }

    [Fact]
    public void Search_matches_name_reading_email_with_normalization()
    {
        _repo.Add(New("a1", "山田 太郎", "やまだ たろう", "taro@example.com"));
        _repo.Add(New("a1", "佐藤 花子", "サトウ ハナコ", "hanako@example.jp"));
        string[] Names(string p) => _repo.Search("a1", p).Select(c => c.Name).ToArray();

        Assert.Equal(new[] { "山田 太郎" }, Names("山田"));
        Assert.Equal(new[] { "山田 太郎" }, Names("やま"));
        Assert.Equal(new[] { "山田 太郎" }, Names("ヤマ"));        // カタカナ入力でひらがな読みに一致
        Assert.Equal(new[] { "佐藤 花子" }, Names("さとう"));      // ひらがな入力でカタカナ読みに一致
        Assert.Equal(new[] { "佐藤 花子" }, Names("ｻﾄｳ"));         // 半角カタカナ
        Assert.Equal(new[] { "佐藤 花子" }, Names("HANA"));        // メールは大文字小文字無視
        Assert.Equal(new[] { "佐藤 花子" }, Names("ｈａｎａ"));     // 全角英字
        Assert.Empty(Names("たろうx"));
        Assert.Empty(Names(""));
        Assert.Empty(_repo.Search("a2", "山田"));
    }

    [Fact]
    public void Deleting_account_cascades_contacts()
    {
        _repo.Add(New("a1", "A", "あ", "x@example.com"));
        using (var conn = _db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM accounts WHERE id='a1'";
            cmd.ExecuteNonQuery();
        }
        Assert.Empty(_repo.GetAll("a1"));
        Assert.Equal(0L, CountEmails());
    }

    [Fact]
    public void ContactLookup_IsKnown_uses_repository()
    {
        _repo.Add(New("a1", "A", "あ", "known@example.com"));
        var lookup = new ContactLookup(_repo);
        Assert.True(lookup.IsKnown("a1", "KNOWN@example.com"));
        Assert.False(lookup.IsKnown("a1", "other@example.com"));
        Assert.False(lookup.IsKnown("a2", "known@example.com"));
        Assert.False(lookup.IsKnown("a1", ""));
    }
}
