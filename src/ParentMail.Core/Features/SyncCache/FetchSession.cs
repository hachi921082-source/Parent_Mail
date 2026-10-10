using System.Collections.Concurrent;

namespace ParentMail.Core.Features.SyncCache;

/// <summary>オンデマンド取得 1 回の結果の種類。</summary>
public enum FetchOutcome { Ok, Offline, Failed }

/// <summary>
/// 本文・添付のオンデマンド取得用に、アカウントごとの IMAP 接続を 1 本借りて処理を流す。
/// 同期の接続とは別だが、取得側はアカウントごとに同時 1 本に絞る（同期と合わせて最大 2 本。Gmail の同時接続上限を超えない）。
/// 通信断（オフライン）と、それ以外の失敗（認証・メール消失など）を分けて返す。
/// </summary>
public sealed class FetchSession
{
    private readonly IAccountRepository _accounts;
    private readonly IImapGatewayFactory _gateways;
    private readonly IAppLog _log;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public FetchSession(IAccountRepository accounts, IImapGatewayFactory gateways, IAppLog log)
    {
        _accounts = accounts;
        _gateways = gateways;
        _log = log;
    }

    /// <summary>
    /// work をログイン済みのゲートウェイで実行する。待ちと実行中のキャンセルは OperationCanceledException のまま投げる。
    /// 通信断は Offline、ログイン失敗やその他の例外は Failed（ログには例外の型だけを残す）。
    /// </summary>
    public async Task<(FetchOutcome Outcome, T? Value)> RunAsync<T>(string accountId, Func<IImapGateway, Task<T>> work, CancellationToken ct)
    {
        var account = _accounts.Get(accountId);
        if (account is null) return (FetchOutcome.Failed, default);

        var gate = _gates.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var gw = _gateways.Create(account);
            var login = await gw.TestLoginAsync(ct).ConfigureAwait(false);
            if (login == LoginResult.NetworkError) return (FetchOutcome.Offline, default);
            if (login == LoginResult.AuthFailed)
            {
                _log.Warn("Fetch", "Auth failed during on-demand fetch");
                return (FetchOutcome.Failed, default);
            }
            var value = await work(gw).ConfigureAwait(false);
            return (FetchOutcome.Ok, value);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (SyncService.IsNetworkError(ex))
            {
                _log.Info("Fetch", "Offline: " + ex.GetType().Name);
                return (FetchOutcome.Offline, default);
            }
            _log.Warn("Fetch", "On-demand fetch failed: " + ex.GetType().Name);
            return (FetchOutcome.Failed, default);
        }
        finally
        {
            gate.Release();
        }
    }
}
