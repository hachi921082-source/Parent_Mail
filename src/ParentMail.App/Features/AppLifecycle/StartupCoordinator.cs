using ParentMail.Core;

namespace ParentMail.App.Features.AppLifecycle;

/// <summary>起動時に順に行う手順（実体は App 側）。</summary>
public interface IStartupSteps
{
    void ShowMainWindow();
    /// <summary>最後に使ったアカウント（無ければ先頭）を現在にする。</summary>
    void InitializeAccounts();
    /// <summary>同期（起動直後と 5 分ごと）と本文先読みを始める。</summary>
    void StartBackground();
}

/// <summary>
/// 起動手順。窓を出す -> アカウントを選ぶ -> 同期などを始める。
/// アカウントが 0 件のときのアカウント追加の窓は accounts-auth（ShellAccountBinder）が出し、追加後に現在のアカウントを選ぶ。
/// 設定 DB の用意とマイグレーション、単一起動の判定は、それぞれ settings と app-shell の登録が済ませている。
/// </summary>
public sealed class StartupCoordinator
{
    private readonly IAccountRepository _accounts;
    private readonly IStartupSteps _steps;
    private readonly IAppLog _log;
    private bool _done;

    public StartupCoordinator(IAccountRepository accounts, IStartupSteps steps, IAppLog log)
    {
        _accounts = accounts;
        _steps = steps;
        _log = log;
    }

    public Task RunAsync()
    {
        if (_done) return Task.CompletedTask;
        _done = true;

        var count = _accounts.GetAll().Count;
        if (count == 0) _log.Info("Startup", "first launch: no accounts");

        _steps.ShowMainWindow();
        // 0 件のときは、追加の窓のあとで accounts-auth が選ぶ
        if (count > 0) _steps.InitializeAccounts();

        try
        {
            // 同期は 0 件でも始める（5 分ごとの同期が、あとから追加されたアカウントも拾う）
            _steps.StartBackground();
        }
        catch (Exception ex)
        {
            _log.Error("Startup", "Failed to start background work", ex);
        }
        return Task.CompletedTask;
    }
}
