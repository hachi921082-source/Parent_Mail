using ParentMail.Core;

namespace ParentMail.App.Features.AppLifecycle;

/// <summary>終わらせようとした理由。</summary>
public enum QuitReason { MainWindowClosed, SessionEnding }

/// <summary>送信待ちのメールがあるときの 3 択。</summary>
public enum QuitChoice { SendAndClose, CloseAsIs, Back }

/// <summary>開いている作成窓。終了時にそれぞれの閉じる確認を順に出す。</summary>
public interface IQuitComposeWindows
{
    int Count { get; }
    /// <summary>全部の作成窓の閉じる確認を通す。1 つでも「戻る」なら false。</summary>
    Task<bool> CloseAllAsync();
}

/// <summary>終了時の画面（送信待ちの確認と「送っています」）。</summary>
public interface IQuitPrompt
{
    QuitChoice AskUnsentMail();
    /// <summary>「送っています…」を出す。Dispose で消す。</summary>
    IDisposable ShowBusy();
}

/// <summary>終了が決まったあとの後片付け（同期の停止、設定の保存など）。</summary>
public interface IQuitFinalizer
{
    void FinishQuit();
}

/// <summary>
/// 終了の手順。作成窓の確認 -> 送信待ちの 3 択 -> 後片付けの順で進め、続けてよければ true を返す。
/// 窓を閉じる操作と Windows の終了の両方から、同じ手順を使う。
/// </summary>
public sealed class QuitCoordinator
{
    private readonly IQuitComposeWindows _compose;
    private readonly IOutboxService _outbox;
    private readonly IQuitPrompt _prompt;
    private readonly IQuitFinalizer _finalizer;
    private readonly IAppLog _log;
    private bool _running;

    public QuitCoordinator(IQuitComposeWindows compose, IOutboxService outbox, IQuitPrompt prompt,
        IQuitFinalizer finalizer, IAppLog log)
    {
        _compose = compose;
        _outbox = outbox;
        _prompt = prompt;
        _finalizer = finalizer;
        _log = log;
    }

    /// <summary>作成窓が開いているか、送信待ちがある（Windows の終了を止める理由を出すかの判定に使う）。</summary>
    public bool NeedsAttention => _compose.Count > 0 || _outbox.PendingCount() > 0;

    /// <summary>true = 終了してよい。false = 取りやめ（「戻る」、送信失敗、すでに確認中）。</summary>
    public async Task<bool> RequestQuitAsync(QuitReason reason)
    {
        // 確認中の再入（×の連打など）は無視する
        if (_running) return false;
        _running = true;
        try
        {
            // 1. 作成窓: どれか 1 つでも「戻る」なら取りやめ
            if (!await _compose.CloseAllAsync().ConfigureAwait(true)) return false;

            // 2. 送信待ち（全アカウント、最大 5 件分の合計）
            if (_outbox.PendingCount() > 0)
            {
                switch (_prompt.AskUnsentMail())
                {
                    case QuitChoice.SendAndClose:
                        SendAllResult result;
                        using (_prompt.ShowBusy())
                        {
                            result = await _outbox.SendAllPendingAsync().ConfigureAwait(true);
                        }
                        // 失敗の通知は outbox-send が出す。開いたままにして状況を見せる
                        if (result != SendAllResult.AllSent) return false;
                        break;
                    case QuitChoice.CloseAsIs:
                        break;
                    default:
                        return false;
                }
            }

            // 3. 後片付け
            _finalizer.FinishQuit();
            _log.Info("Quit", $"quit approved ({reason})");
            return true;
        }
        catch (Exception ex)
        {
            // 終了中の例外はログだけ。ユーザーを閉じられない状態にしない
            _log.Error("Quit", "Unhandled exception during quit", ex);
            return true;
        }
        finally
        {
            _running = false;
        }
    }
}
