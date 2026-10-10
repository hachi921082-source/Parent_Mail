using ParentMail.Core.Features.SyncCache;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>アカウントが「パスワード不正で同期を止めている」状態かを問い合わせる窓口（ISyncService の契約には無いため）。</summary>
public interface IAuthFailureProbe
{
    bool IsAuthFailed(string accountId);
}

/// <summary>実際の同期サービスに問い合わせる実装。</summary>
public sealed class SyncServiceAuthProbe : IAuthFailureProbe
{
    private readonly SyncService _sync;

    public SyncServiceAuthProbe(SyncService sync) => _sync = sync;

    public bool IsAuthFailed(string accountId) => _sync.IsAuthFailed(accountId);
}
