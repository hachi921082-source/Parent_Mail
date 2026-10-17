using ParentMail.App.Features.Shell;
using ParentMail.Core;

namespace ParentMail.Tests.AppLifecycle;

// メイン窓の「×」で終了を確認して閉じる流れ（QuitCloseRunner）のテスト。
public class QuitCloseRunnerTests
{
    private sealed class CaptureLog : IAppLog
    {
        public List<(string Area, string Message, Exception? Ex)> Errors { get; } = new();
        public void Info(string area, string message) { }
        public void Warn(string area, string message) { }
        public void Error(string area, string message, Exception? ex = null) => Errors.Add((area, message, ex));
    }

    private sealed class Probe
    {
        public int Approved;
        public int Closed;
        public Func<Task<bool>> Guard = () => Task.FromResult(true);
        public int GuardCalls;
        public CaptureLog Log = new();
        public QuitCloseRunner Create() => new(
            async () => { GuardCalls++; return await Guard(); },
            () => Approved++,
            () => Closed++,
            Log);
    }

    [Fact]
    public async Task 同期で完了する確認でも一度の呼び出しで閉じる()
    {
        var p = new Probe();
        await p.Create().RunAsync();
        Assert.Equal(1, p.Closed);
        Assert.Equal(1, p.Approved);
    }

    [Fact]
    public async Task 非同期で完了する確認でも閉じる()
    {
        var p = new Probe { Guard = async () => { await Task.Delay(30); return true; } };
        await p.Create().RunAsync();
        Assert.Equal(1, p.Closed);
    }

    [Fact]
    public async Task 確認がfalseなら閉じず承認も立てない()
    {
        var p = new Probe { Guard = () => Task.FromResult(false) };
        await p.Create().RunAsync();
        Assert.Equal(0, p.Closed);
        Assert.Equal(0, p.Approved);
    }

    [Fact]
    public async Task 戻るのあとでもう一度押せば確認し直す()
    {
        var p = new Probe { Guard = () => Task.FromResult(false) };
        var r = p.Create();
        await r.RunAsync();
        await r.RunAsync();
        Assert.Equal(2, p.GuardCalls);
    }

    [Fact]
    public async Task 確認中の連打では確認を二重に走らせない()
    {
        var gate = new TaskCompletionSource<bool>();
        var p = new Probe { Guard = () => gate.Task };
        var r = p.Create();
        var first = r.RunAsync();
        await r.RunAsync(); // 二回目はすぐ戻る
        await r.RunAsync();
        gate.SetResult(true);
        await first;
        Assert.Equal(1, p.GuardCalls);
        Assert.Equal(1, p.Closed);
    }

    [Fact]
    public async Task 閉じたあとの連打でも確認を走らせない()
    {
        var p = new Probe();
        var r = p.Create();
        await r.RunAsync();
        await r.RunAsync();
        Assert.Equal(1, p.GuardCalls);
        Assert.Equal(1, p.Closed);
    }

    [Fact]
    public async Task 確認が例外を投げても記録して閉じる()
    {
        var p = new Probe { Guard = () => throw new InvalidOperationException("boom") };
        await p.Create().RunAsync();
        Assert.Equal(1, p.Closed);
        Assert.Single(p.Log.Errors);
        Assert.IsType<InvalidOperationException>(p.Log.Errors[0].Ex);
    }

    [Fact]
    public async Task 閉じる処理の例外も記録し外へ出さない()
    {
        var log = new CaptureLog();
        var r = new QuitCloseRunner(() => Task.FromResult(true), () => { }, () => throw new InvalidOperationException("x"), log);
        await r.RunAsync(); // 例外が出ない
        Assert.Single(log.Errors);
    }

    [Fact]
    public async Task 閉じる処理は呼び出しが戻ったあとに実行される()
    {
        // Closing イベントの中で Close() を呼ばないための条件
        var inCaller = true;
        var closedInCaller = false;
        var r = new QuitCloseRunner(() => Task.FromResult(true), () => { }, () => closedInCaller = inCaller, new CaptureLog());
        var task = r.RunAsync();
        inCaller = false;
        await task;
        Assert.False(closedInCaller);
    }
}
