using Microsoft.Extensions.DependencyInjection;
using ParentMail.App;
using ParentMail.App.Features.AccountsAuth;
using ParentMail.Core;
using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AccountsAuth;

// DI 登録が解決できること（窓は作らない）。
public sealed class RegistrationTests
{
    [Fact]
    public void AddAccountsAuth_ResolvesCoreServices_AndRealAppState()
    {
        var s = new ServiceCollection();
        s.AddSingleton<ISettingsDb, FakeSettingsDb>();
        s.AddSingleton<ISettingsStore, FakeSettingsStore>();
        s.AddSingleton<IMailRepository, InMemoryMailRepository>();
        s.AddAccountsAuth();
        using var sp = s.BuildServiceProvider();

        Assert.IsType<AppState>(sp.GetRequiredService<IAppState>());
        Assert.IsType<CredentialStore>(sp.GetRequiredService<ICredentialStore>());
        Assert.NotNull(sp.GetRequiredService<AccountService>());
        Assert.NotNull(sp.GetRequiredService<AccountSwitchViewModel>());
    }

    [Fact]
    public async Task WithoutGateway_AddFailsAsNetworkError_NotCrash()
    {
        var s = new ServiceCollection();
        s.AddSingleton<ISettingsDb, FakeSettingsDb>();
        s.AddSingleton<ISettingsStore, FakeSettingsStore>();
        s.AddSingleton<IMailRepository, InMemoryMailRepository>();
        s.AddAccountsAuth();
        // 後から登録したフェイクが優先される
        s.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
        s.AddSingleton<ICredentialStore, FakeCredentialStore>();
        using var sp = s.BuildServiceProvider();
        var svc = sp.GetRequiredService<AccountService>();
        Assert.Equal(AddAccountResult.NetworkError, await svc.ValidateAndAddAsync("a@gmail.com", "p", "A"));
    }
}
