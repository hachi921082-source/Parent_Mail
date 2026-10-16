using NSubstitute;
using ParentMail.Core;
using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AccountsAuth;

// アカウント追加（重複・ログイン確認・上限）のテスト。ゲートウェイはフェイク。
public sealed class AccountServiceTests
{
    private readonly InMemoryAccountRepository _repo = new();
    private readonly FakeCredentialStore _cred = new();
    private readonly LoginFactory _factory;
    private readonly FakeSyncService _sync = new();
    private readonly AccountService _svc;

    public AccountServiceTests()
    {
        _factory = new LoginFactory(_cred);
        _svc = new AccountService(_repo, _cred, _factory, _sync);
    }

    // ログイン確認の時点で資格情報に入っているパスワード（本物のゲートウェイが読む想定）を記録する工場
    private sealed class LoginFactory(ICredentialStore store) : IImapGatewayFactory
    {
        public LoginResult Result { get; set; } = LoginResult.Ok;
        public int CreateCount { get; private set; }
        public string? PasswordSeenAtLogin { get; private set; }
        public Account? Created { get; private set; }

        public IImapGateway Create(Account account)
        {
            CreateCount++;
            Created = account;
            var gw = Substitute.For<IImapGateway>();
            gw.TestLoginAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                PasswordSeenAtLogin = store.Get(account.Id);
                return Task.FromResult(Result);
            });
            return gw;
        }
    }

    private sealed class ThrowingFactory : IImapGatewayFactory
    {
        public IImapGateway Create(Account account) => throw new IOException("offline");
    }

    [Fact]
    public async Task Success_SavesRowAndCredential_AndTriggersInitialSync()
    {
        var added = new List<string>();
        _svc.AccountAdded += (_, e) => added.Add(e.AccountId);

        var r = await _svc.ValidateAndAddAsync("Mama@Gmail.com", "pw-dummy", "お母さん");

        Assert.Equal(AddAccountResult.Ok, r);
        var acc = Assert.Single(_repo.GetAll());
        Assert.Equal("お母さん", acc.DisplayName);
        Assert.Equal("Mama@Gmail.com", acc.Email);
        Assert.Equal("pw-dummy", _cred.Get(acc.Id));
        Assert.Equal("pw-dummy", _factory.PasswordSeenAtLogin);
        Assert.Equal(new[] { acc.Id }, added);
        Assert.Contains((acc.Id, SyncTrigger.AccountAdded), _sync.Requests);
    }

    [Fact]
    public async Task Password_WhitespaceIsStripped()
    {
        await _svc.ValidateAndAddAsync("a@gmail.com", " abcd efgh	ijkl ", "A");
        Assert.Equal("abcdefghijkl", _cred.Get(_repo.GetAll()[0].Id));
    }

    [Fact]
    public async Task Duplicate_CaseInsensitive_IsRejected_NothingChanges()
    {
        await _svc.ValidateAndAddAsync("a@gmail.com", "p", "A");
        var count = _factory.CreateCount;

        var r = await _svc.ValidateAndAddAsync("A@GMAIL.COM", "p2", "B");

        Assert.Equal(AddAccountResult.Duplicate, r);
        Assert.Single(_repo.GetAll());
        Assert.Equal(count, _factory.CreateCount);
    }

    [Theory]
    [InlineData(LoginResult.AuthFailed, AddAccountResult.AuthFailed)]
    [InlineData(LoginResult.NetworkError, AddAccountResult.NetworkError)]
    public async Task LoginFailure_SavesNothing_AndRemovesTemporaryCredential(LoginResult login, AddAccountResult expected)
    {
        _factory.Result = login;
        var added = false;
        _svc.AccountAdded += (_, _) => added = true;

        var r = await _svc.ValidateAndAddAsync("a@gmail.com", "bad", "A");

        Assert.Equal(expected, r);
        Assert.Empty(_repo.GetAll());
        Assert.Null(_cred.Get(_factory.Created!.Id));
        Assert.False(added);
        Assert.Empty(_sync.Requests);
    }

    [Fact]
    public async Task LoginThrows_IsTreatedAsNetworkError_AndSavesNothing()
    {
        var svc = new AccountService(_repo, _cred, new ThrowingFactory(), _sync);
        var r = await svc.ValidateAndAddAsync("a@gmail.com", "p", "A");
        Assert.Equal(AddAccountResult.NetworkError, r);
        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void AccountLimitConstants_MatchSpec()
    {
        Assert.Equal(5, Constants.MaxAccounts);
        Assert.Equal(300, Constants.AccountButtonMaxWidthDip);
        Assert.Equal(220, Constants.AccountButtonMaxWidthManyDip);
        Assert.Equal(120, Constants.AccountButtonMinWidthDip);
        Assert.Equal(56, Constants.AccountScrollArrowDip);
    }

    [Fact]
    public async Task FifthAccount_IsAccepted_SixthIsRejected_AsLimitReached_WithoutLoginAttempt()
    {
        for (var i = 1; i <= 5; i++)
        {
            Assert.True(_svc.CanAddMore);
            Assert.Equal(AddAccountResult.Ok, await _svc.ValidateAndAddAsync($"u{i}@gmail.com", "p", $"N{i}"));
        }
        Assert.False(_svc.CanAddMore);
        var count = _factory.CreateCount;

        var r = await _svc.ValidateAndAddAsync("u6@gmail.com", "p", "N6");

        Assert.Equal(AddAccountResult.LimitReached, r);
        Assert.Equal(5, _repo.GetAll().Count);
        Assert.Equal(count, _factory.CreateCount);
    }

    [Fact]
    public async Task CanAddMore_TrueBelowLimit()
    {
        Assert.True(_svc.CanAddMore);
        await _svc.ValidateAndAddAsync("a@gmail.com", "p", "A");
        Assert.True(_svc.CanAddMore);
    }

    [Fact]
    public async Task SortOrder_FollowsAddOrder()
    {
        await _svc.ValidateAndAddAsync("a@gmail.com", "p", "A");
        await _svc.ValidateAndAddAsync("b@gmail.com", "p", "B");
        Assert.Equal(new[] { "A", "B" }, _repo.GetAll().Select(a => a.DisplayName));
    }

    [Theory]
    [InlineData("a@gmail.com", true)]
    [InlineData("  a@gmail.com ", true)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    [InlineData("a@", false)]
    [InlineData("@gmail.com", false)]
    [InlineData("a b@gmail.com", false)]
    [InlineData("a@gmail", false)]
    public void Validator_Email(string input, bool ok) => Assert.Equal(ok, AccountInputValidator.IsValidEmail(input));

    [Fact]
    public void Validator_DefaultDisplayName_IsLocalPart()
    {
        Assert.Equal("taro.yamada", AccountInputValidator.DefaultDisplayName("taro.yamada@gmail.com"));
        Assert.Equal("", AccountInputValidator.DefaultDisplayName("not-an-address"));
    }
}
