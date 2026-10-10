using System.Windows.Input;
using ParentMail.Core;

namespace ParentMail.App.Features.SyncCache;

/// <summary>
/// 入力のない時間の監視。WPF の InputManager.PreProcessInput で最後の入力の時刻を控える（先読みの開始・停止の判断用）。
/// Attach は UI スレッドから 1 回呼ぶ。
/// </summary>
public sealed class InputIdleMonitor : IIdleMonitor
{
    private readonly Func<long> _tickMs;
    private long _lastInputMs;
    private bool _attached;

    public InputIdleMonitor(Func<long>? tickMs = null)
    {
        _tickMs = tickMs ?? (() => Environment.TickCount64);
        _lastInputMs = _tickMs();
    }

    public TimeSpan IdleFor => TimeSpan.FromMilliseconds(Math.Max(0, _tickMs() - Interlocked.Read(ref _lastInputMs)));

    public event EventHandler? UserActivity;

    /// <summary>入力の監視を始める（2 回目以降は何もしない）。</summary>
    public void Attach()
    {
        if (_attached) return;
        _attached = true;
        InputManager.Current.PreProcessInput += OnPreProcessInput;
    }

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is KeyboardEventArgs or MouseEventArgs or TouchEventArgs or StylusEventArgs)
            NotifyActivity();
    }

    /// <summary>操作があったことを記録し、購読者へ知らせる。</summary>
    public void NotifyActivity()
    {
        Interlocked.Exchange(ref _lastInputMs, _tickMs());
        UserActivity?.Invoke(this, EventArgs.Empty);
    }
}
