namespace ParentMail.App.Services;

/// <summary>
/// 単一起動ガード。名前付き Mutex で 1 つ目の起動を判定し、2 つ目の起動は名前付きイベントで
/// 1 つ目に知らせて終了する（受け取った側が窓を前面に出す）。
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    public const string MutexName = @"Global\Parent_Mail_SingleInstance";
    public const string EventName = @"Global\Parent_Mail_Activate";

    private readonly string _mutexName;
    private readonly string _eventName;
    private Mutex? _mutex;
    private EventWaitHandle? _signal;
    private RegisteredWaitHandle? _registration;

    public SingleInstanceGuard(string mutexName = MutexName, string eventName = EventName)
    {
        _mutexName = mutexName;
        _eventName = eventName;
    }

    /// <summary>1 つ目の起動なら true。onSignaled は 2 つ目の起動から知らされたときに呼ばれる（別スレッド）。</summary>
    public bool TryAcquire(Action onSignaled)
    {
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, _eventName);
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _signal, (_, _) => onSignaled(), null, Timeout.Infinite, executeOnlyOnce: false);
        return true;
    }

    /// <summary>先に起動している窓へ「前面に出して」と知らせる。</summary>
    public bool SignalExisting()
    {
        try
        {
            using var existing = EventWaitHandle.OpenExisting(_eventName);
            return existing.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _signal?.Dispose();
        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
            _mutex.Dispose();
        }
    }
}
