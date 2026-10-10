using Microsoft.Data.Sqlite;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AccountsAuth;

// 名前変更・完全削除・パスワード入れ直し（accounts-auth PLAN-2）の業務処理のテスト。
// 削除は日本語を含む一時フォルダで、実際の settings.db / cache.db / 添付ファイルを使って確かめる。
public sealed class AccountManageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "親メール_" + Guid.NewGuid().ToString("N"));
    private readonly SettingsDb _settings;
    private readonly AccountRepository _accounts;
    private readonly CachePaths _paths;
    private readonly CacheDb _cache;
    private readonly FakeCredentialStore _cred = new();
    private readonly FakeSyncService _sync = new();
    private readonly FakeOutboxService _outbox = new();
    private readonly FakeImapGatewayFactory _gateways = new();
    private readonly AccountService _svc;

    public AccountManageTests()
    {
        _settings = new SettingsDb(Path.Combine(_root, "設定", "settings.db"));
        _settings.Migrate();
        _accounts = new AccountRepository(_settings);
        _paths = new CachePaths(Path.Combine(_root, "データ"));
        _cache = new CacheDb(_paths);
        _svc = new AccountService(_accounts, _cred, _gateways, _sync, _outbox, _paths);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private void Seed(string id, string email, string name)
    {
        _accounts.Add(new Account(id, email, name, 0));
        _cred.Save(id, "pw-" + id);
        new ContactRepository(_settings).Add(new Contact(0, id, "山田 太郎", "やまだ たろう", new[] { "taro-" + id + "@example.com" }, "弟"));
        new BlockRepository(_settings).Add(id, BlockKind.Address, "spam-" + id + "@example.com");
        new FilterRepository(_settings).Save(new FilterRule { AccountId = id, SortOrder = 0, SubjectContains = "セール", Action = FilterAction.MoveToTrash });
        // cache.db を作って接続を返却し（プールに残る）、日本語名の添付ファイルを置く
        using (_cache.Open(id)) { }
        Directory.CreateDirectory(_paths.AttachmentsDir(id));
        File.WriteAllText(Path.Combine(_paths.AttachmentsDir(id), "請求書_令和.pdf"), "dummy");
    }

    [Fact]
    public async Task Delete_PurgesRowsCredentialAndFolder_KeepsTheOtherAccount()
    {
        Seed("a1", "one@gmail.com", "お母さん");
        Seed("a2", "two@gmail.com", "お父さん");
        var removed = new List<string>();
        _svc.AccountRemoved += (_, e) => removed.Add(e.AccountId);

        var r = await _svc.DeleteAsync("a1");

        Assert.Equal(DeleteResult.Ok, r);
        Assert.Null(_accounts.Get("a1"));
        Assert.Empty(new ContactRepository(_settings).GetAll("a1"));
        Assert.Empty(new BlockRepository(_settings).GetAll("a1"));
        Assert.Empty(new FilterRepository(_settings).GetAll("a1"));
        Assert.Null(_cred.Get("a1"));
        Assert.False(Directory.Exists(_paths.AccountDir("a1")));
        Assert.Equal(new[] { "a1" }, removed);

        // もう一方は何も消えない
        Assert.NotNull(_accounts.Get("a2"));
        Assert.Single(new ContactRepository(_settings).GetAll("a2"));
        Assert.Single(new BlockRepository(_settings).GetAll("a2"));
        Assert.Single(new FilterRepository(_settings).GetAll("a2"));
        Assert.Equal("pw-a2", _cred.Get("a2"));
        Assert.True(File.Exists(Path.Combine(_paths.AttachmentsDir("a2"), "請求書_令和.pdf")));
    }

    [Fact]
    public async Task Delete_WithPendingOutbox_ChangesNothing()
    {
        Seed("a1", "one@gmail.com", "お母さん");
        _outbox.Pending["a1"] = 1;
        var removed = false;
        _svc.AccountRemoved += (_, _) => removed = true;

        var r = await _svc.DeleteAsync("a1");

        Assert.Equal(DeleteResult.OutboxPending, r);
        Assert.NotNull(_accounts.Get("a1"));
        Assert.Equal("pw-a1", _cred.Get("a1"));
        Assert.True(Directory.Exists(_paths.AccountDir("a1")));
        Assert.False(removed);
    }

    [Fact]
    public async Task Delete_UnknownAccount_IsNotFound()
        => Assert.Equal(DeleteResult.NotFound, await _svc.DeleteAsync("nope"));

    [Fact]
    public void Rename_Persists_TrimsAndRaisesEvent()
    {
        Seed("a1", "one@gmail.com", "old");
        var renamed = new List<string>();
        _svc.AccountRenamed += (_, e) => renamed.Add(e.AccountId);

        var r = _svc.Rename("a1", "  おばあちゃんのメール ");

        Assert.Equal(RenameResult.Ok, r);
        Assert.Equal("おばあちゃんのメール", _accounts.Get("a1")!.DisplayName);
        Assert.Equal(new[] { "a1" }, renamed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   　")]
    public void Rename_Empty_IsRejected_NameKept(string input)
    {
        Seed("a1", "one@gmail.com", "old");
        Assert.Equal(RenameResult.EmptyName, _svc.Rename("a1", input));
        Assert.Equal("old", _accounts.Get("a1")!.DisplayName);
    }

    [Fact]
    public async Task ReplacePassword_Success_SavesStripsSpaces_ClearsAuthFailed_AndResumesSync()
    {
        Seed("a1", "one@gmail.com", "お母さん");
        _sync.MarkAuthFailed("a1");
        var replaced = new List<string>();
        _svc.PasswordReplaced += (_, e) => replaced.Add(e.AccountId);

        var r = await _svc.ReplacePasswordAsync("a1", "abcd efgh ijkl");

        Assert.Equal(ReplacePasswordResult.Ok, r);
        Assert.Equal("abcdefghijkl", _cred.Get("a1"));
        Assert.DoesNotContain("a1", _sync.AuthFailed);
        Assert.Contains(("a1", SyncTrigger.Reconnect), _sync.Requests);
        Assert.Equal(new[] { "a1" }, replaced);
    }

    [Theory]
    [InlineData(LoginResult.AuthFailed, ReplacePasswordResult.AuthFailed)]
    [InlineData(LoginResult.NetworkError, ReplacePasswordResult.NetworkError)]
    public async Task ReplacePassword_Failure_KeepsOldPassword_AndAuthFailedFlag(LoginResult login, ReplacePasswordResult expected)
    {
        Seed("a1", "one@gmail.com", "お母さん");
        _sync.MarkAuthFailed("a1");
        _gateways.Create(_accounts.Get("a1")!);
        _gateways.ByAccountId["a1"].LoginResult = login;

        var r = await _svc.ReplacePasswordAsync("a1", "new-pass");

        Assert.Equal(expected, r);
        Assert.Equal("pw-a1", _cred.Get("a1"));
        Assert.Contains("a1", _sync.AuthFailed);
        Assert.Empty(_sync.Requests);
    }

    [Fact]
    public async Task ReplacePassword_EmptyOrUnknown()
    {
        Seed("a1", "one@gmail.com", "お母さん");
        Assert.Equal(ReplacePasswordResult.EmptyPassword, await _svc.ReplacePasswordAsync("a1", "  "));
        Assert.Equal(ReplacePasswordResult.NotFound, await _svc.ReplacePasswordAsync("zz", "x"));
        Assert.Equal("pw-a1", _cred.Get("a1"));
    }
}
