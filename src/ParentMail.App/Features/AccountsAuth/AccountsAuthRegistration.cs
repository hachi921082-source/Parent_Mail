using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParentMail.App.Features.Shell;
using ParentMail.Core;
using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>accounts-auth の DI 登録（アカウント保存、資格情報、現在の状態、追加窓、切替ボタン）。</summary>
public static class AccountsAuthRegistration
{
    public static IServiceCollection AddAccountsAuth(this IServiceCollection services)
    {
        services.AddSingleton<IAccountRepository, AccountRepository>();
        services.AddSingleton<ICredentialStore, CredentialStore>();
        services.AddSingleton<IAppState, AppState>();
        // sync-cache-2 が MailKit 実装を登録するまでの仮実装（登録は「接続できません」扱いになる）。
        services.TryAddSingleton<IImapGatewayFactory, UnavailableImapGatewayFactory>();
        services.AddSingleton<AccountService>();
        services.AddSingleton<AccountAddLauncher>();
        services.AddSingleton<AccountSwitchViewModel>();
        services.AddSingleton<ShellAccountBinder>();

        // 上部バーのスロットは app-shell が持つ既定の表示を使い、MainViewModel が作られた時点で切替ボタンをつなぐ。
        // 後から登録した MainViewModel が優先される。コンストラクタ引数は DI に任せる。
        services.AddSingleton<MainViewModel>(sp =>
        {
            var shell = ActivatorUtilities.CreateInstance<MainViewModel>(sp);
            sp.GetRequiredService<ShellAccountBinder>().Attach(shell);
            return shell;
        });
        return services;
    }

    private sealed class UnavailableImapGatewayFactory : IImapGatewayFactory
    {
        public IImapGateway Create(Account account) => throw new NotSupportedException("IMAP gateway is not registered yet");
    }
}
