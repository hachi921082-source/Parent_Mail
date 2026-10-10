using ParentMail.App.Services;

namespace ParentMail.Tests.AppShell;

public class SingleInstanceGuardTests
{
    [Fact]
    public void 二つ目の起動は取得に失敗し一つ目へ通知が届く()
    {
        var id = Guid.NewGuid().ToString("N");
        using var first = new SingleInstanceGuard($@"Local\PM_Test_M_{id}", $@"Local\PM_Test_E_{id}");
        using var second = new SingleInstanceGuard($@"Local\PM_Test_M_{id}", $@"Local\PM_Test_E_{id}");
        using var signaled = new ManualResetEventSlim();

        Assert.True(first.TryAcquire(signaled.Set));
        Assert.False(second.TryAcquire(() => { }));
        Assert.True(second.SignalExisting());
        Assert.True(signaled.Wait(TimeSpan.FromSeconds(5)));
    }
}
