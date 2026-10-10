using Microsoft.Data.Sqlite;
using ParentMail.Core.Data;
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
    private readonly IOutboxService? _outbox;
    private readonly CachePaths? _paths;
    private readonly IAppLog? _log;

    public AccountService(IAccountRepository accounts, ICredentialStore credentials, IImapGatewayFactory gateways,
        ISyncService? sync = null, IOutboxService? outbox = null, CachePaths? paths = null, IAppLog? log = null)
    {
        _accounts = accounts;
        _credentials = credentials;
        _gateways = gateways;
        _sync = sync;
        _outbox = outbox;
        _paths = paths;
        _log = log;
    }

    /// <summary>登録に成功した直後（初回同期を依頼したあと）に出る。</summary>
    public event EventHandler<AccountEventArgs>? AccountAdded;

    /// <summary>名前変更に成功した直後に出る。</summary>
    public event EventHandler<AccountEventArgs>? AccountRenamed;

    /// <summary>削除（資格情報・設定の行・キャッシュの掃除）が済んだ直後に出る。</summary>
    public event EventHandler<AccountEventArgs>? AccountRemoved;

    /// <summary>パスワードの入れ直しに成功した直後に出る。</summary>
    public event EventHandler<AccountEventArgs>? PasswordReplaced;

    /// <summary>名前を変える。確認ダイアログは付けない（利用者の決定 2026-10-08）。空は拒否。</summary>
    public RenameResult Rename(string accountId, string displayName)
    {
        displayName = displayName.Trim();
        if (displayName.Length == 0) return RenameResult.EmptyName;
        if (_accounts.Get(accountId) is null) return RenameResult.NotFound;
        _accounts.Rename(accountId, displayName);
        AccountRenamed?.Invoke(this, new AccountEventArgs(accountId));
        return RenameResult.Ok;
    }

    /// <summary>このアカウントに送信待ちのメールが残っているか（残っていれば削除できない）。</summary>
    public bool HasPendingOutbox(string accountId) => _outbox is not null && _outbox.PendingCount(accountId) > 0;

    /// <summary>
    /// アカウントを完全に削除する。順序: 資格情報 → 設定の行（連絡先・拒否・振り分けは連鎖削除）→
    /// アカウントのフォルダ（cache.db・添付・送信箱・下書き）。送信待ちが残っていれば何も消さない。
    /// </summary>
    public async Task<DeleteResult> DeleteAsync(string accountId)
    {
        if (_accounts.Get(accountId) is null) return DeleteResult.NotFound;
        if (HasPendingOutbox(accountId)) return DeleteResult.OutboxPending;

        _credentials.Delete(accountId);
        // 行を先に消すので、以降の定期同期はこのアカウントを対象にしない
        _accounts.Delete(accountId);
        await PurgeFilesAsync(accountId).ConfigureAwait(false);
        AccountRemoved?.Invoke(this, new AccountEventArgs(accountId));
        return DeleteResult.Ok;
    }

    private async Task PurgeFilesAsync(string accountId)
    {
        if (_paths is null) return;
        var dir = _paths.AccountDir(accountId);
        // 開いたままの接続が cache.db を掴んでいるとフォルダを消せないので、プールを閉じてから消す。
        // 同期が途中で掴んでいる場合に備えて、少し待って数回やり直す。
        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 5)
                {
                    _log?.Warn("Accounts", "Account folder could not be removed: " + ex.GetType().Name);
                    return;
                }
                await Task.Delay(300).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// アプリパスワードを入れ直す。ログインを確かめてから置き換え、失敗なら元のパスワードに戻す。
    /// 成功したら同期の「止め」を外して同期を再開する。
    /// </summary>
    public async Task<ReplacePasswordResult> ReplacePasswordAsync(string accountId, string password,
        CancellationToken ct = default)
    {
        password = new string(password.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (password.Length == 0) return ReplacePasswordResult.EmptyPassword;
        if (_accounts.Get(accountId) is not { } account) return ReplacePasswordResult.NotFound;

        var previous = _credentials.Get(accountId);
        _credentials.Save(accountId, password);
        var replaced = false;
        try
        {
            var login = await TestLoginAsync(account, ct).ConfigureAwait(false);
            if (login != LoginResult.Ok)
            {
                return login == LoginResult.AuthFailed ? ReplacePasswordResult.AuthFailed : ReplacePasswordResult.NetworkError;
            }
            replaced = true;
        }
        finally
        {
            // 失敗・例外・取り消しのときは、元のパスワードをそのまま残す
            if (!replaced)
            {
                if (previous is null) _credentials.Delete(accountId);
                else _credentials.Save(accountId, previous);
            }
        }

        if (_sync is not null)
        {
            _sync.ClearAuthFailed(accountId);
            try
            {
                _ = _sync.SyncNowAsync(accountId, SyncTrigger.Reconnect)
                    .ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch
            {
                // 同期側がログ・帯で扱う
            }
        }
        PasswordReplaced?.Invoke(this, new AccountEventArgs(accountId));
        return ReplacePasswordResult.Ok;
    }

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
