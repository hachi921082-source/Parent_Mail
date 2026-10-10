using ParentMail.Core.Models;

namespace ParentMail.Core.Features.AccountsAuth;

/// <summary>
/// アカウント追加の業務処理（PQ28）。重複・上限を調べ、ログインを確認してから保存する。
/// ゲートウェイはアカウント ID でパスワードを資格情報から読む前提なので、確認の間だけ仮に保存し、失敗なら必ず消す。
/// </summary>
public sealed class AccountService
{
    private readonly IAccountRepository _accounts;
    private readonly ICredentialStore _credentials;
    private readonly IImapGatewayFactory _gateways;
    private readonly ISyncService? _sync;

    public AccountService(IAccountRepository accounts, ICredentialStore credentials, IImapGatewayFactory gateways,
        ISyncService? sync = null)
    {
        _accounts = accounts;
        _credentials = credentials;
        _gateways = gateways;
        _sync = sync;
    }

    /// <summary>登録に成功した直後（初回同期を依頼したあと）に出る。</summary>
    public event EventHandler<AccountEventArgs>? AccountAdded;

    public bool CanAddMore => _accounts.GetAll().Count < Constants.MaxAccounts;

    public async Task<AddAccountResult> ValidateAndAddAsync(string email, string password, string displayName,
        CancellationToken ct = default)
    {
        email = email.Trim();
        // Gmail のアプリパスワードは「abcd efgh ...」と空白区切りで表示されるため、空白は取り除く
        password = new string(password.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var existing = _accounts.GetAll();
        if (existing.Count >= Constants.MaxAccounts) return AddAccountResult.LimitReached;
        if (existing.Any(a => string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase))) return AddAccountResult.Duplicate;

        var candidate = new Account(Guid.NewGuid().ToString("N"), email, displayName.Trim(), existing.Count);
        _credentials.Save(candidate.Id, password);
        var saved = false;
        try
        {
            var login = await TestLoginAsync(candidate, ct).ConfigureAwait(false);
            if (login != LoginResult.Ok)
            {
                return login == LoginResult.AuthFailed ? AddAccountResult.AuthFailed : AddAccountResult.NetworkError;
            }

            _accounts.Add(candidate);
            saved = true;
        }
        finally
        {
            // 失敗・例外・取り消しのときは仮保存のパスワードを残さない
            if (!saved) _credentials.Delete(candidate.Id);
        }

        TriggerInitialSync(candidate.Id);
        AccountAdded?.Invoke(this, new AccountEventArgs(candidate.Id));
        return AddAccountResult.Ok;
    }

    private async Task<LoginResult> TestLoginAsync(Account candidate, CancellationToken ct)
    {
        try
        {
            var gateway = _gateways.Create(candidate);
            await using (gateway.ConfigureAwait(false))
            {
                return await gateway.TestLoginAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // 接続中の例外は、ネットワークの問題として扱う（詳細は文言に出さない）
            return LoginResult.NetworkError;
        }
    }

    private void TriggerInitialSync(string accountId)
    {
        if (_sync is null) return;
        // 初回同期は待たない（失敗しても登録は成立済み。進捗は同期側が帯で知らせる）
        try
        {
            _ = _sync.SyncNowAsync(accountId, SyncTrigger.AccountAdded)
                .ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
        catch
        {
            // 同期側がログ・帯で扱う
        }
    }
}
