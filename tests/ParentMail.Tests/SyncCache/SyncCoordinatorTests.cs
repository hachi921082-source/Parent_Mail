using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.SyncCache;

public class SyncCoordinatorTests
{
    private static (SyncCoordinator Coord, FakeSyncService Sync) Make(TimeSpan? interval = null)
    {
        var accounts = new InMemoryAccountRepository();
        accounts.Add(new Account("a1", "a@gmail.com", "母", 0));
        accounts.Add(new Account("a2", "b@gmail.com", "父", 1));
        var sync = new FakeSyncService();
        return (new SyncCoordinator(sync, accounts, new FakeAppLog(), interval), sync);
    }

    [Fact]
    public async Task SyncAll_RunsEveryAccountWithGivenTrigger()
    {
        var (c, sync) = Make();
        await c.SyncAllAsync(SyncTrigger.Timer);
        Assert.Equal(new[] { ("a1", SyncTrigger.Timer), ("a2", SyncTrigger.Timer) }, sync.Requests.OrderBy(r => r.AccountId));
    }

    [Fact]
    public void SyncInterval_IsFiveMinutes() => Assert.Equal(5, Constants.SyncIntervalMinutes);

    [Fact]
    public async Task Start_SyncsAtStartup_ThenOnEveryTimerTick_UntilStopped()
    {
        var (c, sync) = Make(TimeSpan.FromMilliseconds(150));
        c.Start();
        c.Start();                                   // 二重開始は無視
        await Task.Delay(600);
        c.Stop();
        var count = sync.Requests.Count;

        Assert.Equal(1, sync.Requests.Count(r => r.AccountId == "a1" && r.Trigger == SyncTrigger.Startup));
        Assert.True(sync.Requests.Count(r => r.Trigger == SyncTrigger.Timer && r.AccountId == "a1") >= 2);

        await Task.Delay(400);
        Assert.Equal(count, sync.Requests.Count);    // 停止後は動かない
    }

    [Fact]
    public async Task RequestRefresh_StartsManualSyncForThatAccountOnly()
    {
        var (c, sync) = Make();
        c.RequestRefresh("a2");
        await Task.Delay(100);
        Assert.Equal(new[] { ("a2", SyncTrigger.Manual) }, sync.Requests);
    }

    [Fact]
    public async Task SyncFailure_IsLoggedNotThrown()
    {
        var accounts = new InMemoryAccountRepository();
        accounts.Add(new Account("a1", "a@gmail.com", "母", 0));
        var log = new FakeAppLog();
        var c = new SyncCoordinator(new ThrowingSync(), accounts, log);

        await c.SyncAllAsync(SyncTrigger.Timer);

        Assert.Contains(log.Entries, e => e.StartsWith("E:Sync"));
    }

    private sealed class ThrowingSync : ISyncService
    {
        public SyncState GetState(string accountId) => SyncState.Idle;
        public Task SyncNowAsync(string accountId, SyncTrigger trigger, CancellationToken ct = default) => throw new InvalidOperationException();
        public void MarkAuthFailed(string accountId) { }
        public void ClearAuthFailed(string accountId) { }
        public event EventHandler<AccountEventArgs>? StateChanged { add { } remove { } }
        public event EventHandler<AccountEventArgs>? SyncCompleted { add { } remove { } }
        public event EventHandler<NewMailEventArgs>? NewMailProcessed { add { } remove { } }
    }
}
