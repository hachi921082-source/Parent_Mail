using ParentMail.Core;

namespace ParentMail.App.Features.Shell;

/// <summary>
/// メイン窓の「×」で、終了を確認してから窓を閉じる流れ。
/// Closing イベントの中で Close() を呼ぶと WPF が例外を出すため、閉じる処理は必ずあとで行う。
/// 例外は記録するだけで、モーダルの通知にも「閉じられない窓」にもしない。
/// </summary>
public sealed class QuitCloseRunner
{
    private readonly Func<Task<bool>> _guard;
    private readonly Action _markApproved;
    private readonly Action _close;
    private readonly IAppLog? _log;
    private bool _running;

    public QuitCloseRunner(Func<Task<bool>> guard, Action markApproved, Action close, IAppLog? log)
    {
        _guard = guard;
        _markApproved = markApproved;
        _close = close;
        _log = log;
    }

    public async Task RunAsync()
    {
        // 確認中・閉じる途中の「×」連打では、確認を二重に走らせない
        if (_running) return;
        _running = true;
        var closing = false;
        try
        {
            bool approved;
            try { approved = await _guard().ConfigureAwait(true); }
            catch (Exception ex)
            {
                // 終了中の例外で閉じられなくならないようにする
                Log("Quit guard failed", ex);
                approved = true;
            }
            if (!approved) return; // 「戻る」: 次の「×」でまた確認する

            _markApproved();
            closing = true;
            // 同期で終わった場合でも Closing イベントの外で閉じる
            await Task.Yield();
            _close();
        }
        catch (Exception ex)
        {
            Log("Quit close failed", ex);
        }
        finally
        {
            // 閉じる段階に入っていなければ、次の「×」を受け付ける
            if (!closing) _running = false;
        }
    }

    private void Log(string message, Exception ex)
    {
        try
        {
            // 種類・メッセージ・先頭の数フレームだけ。メール内容は含めない
            var frames = string.Join(" | ", (ex.StackTrace ?? "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(3).Select(s => s.Trim()));
            _log?.Error("Quit", $"{message}: {ex.GetType().Name}: {ex.Message} @ {frames}", ex);
        }
        catch { /* 記録の失敗で終了を止めない */ }
    }
}
