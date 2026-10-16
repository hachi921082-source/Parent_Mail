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

    [Fact]
    public async Task SyncAll_AccountListFailure_IsLoggedNotThrown()
    {
        var log = new FakeAppLog();
        var c = new SyncCoordinator(new FakeSyncService(), new ThrowingAccounts(), log);
        await c.SyncAllAsync(SyncTrigger.Timer);
        Assert.Contains(log.Entries, e => e.StartsWith("E:Sync"));
    }

    // 呼ばれた同期を手動で完了させられる偽の同期。同時に走った最大数も数える
    private sealed class GatedSync : ISyncService
    {
        private readonly object _lock = new();
        public List<(string AccountId, SyncTrigger Trigger)> Started { get; } = new();
        public Dictionary<string, TaskCompletionSource> Gates { get; } = new();
        public int MaxConcurrent { get; private set; }
        private int _running;

        public async Task SyncNowAsync(string accountId, SyncTrigger trigger, CancellationToken ct = default)
        {
            TaskCompletionSource gate;
            lock (_lock)
            {
                Started.Add((accountId, trigger));
                gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Gates[accountId] = gate;
                _running++;
                MaxConcurrent = Math.Max(MaxConcurrent, _running);
            }
            await gate.Task;
            lock (_lock) _running--;
        }

        public void Release(string accountId) { lock (_lock) Gates[accountId].TrySetResult(); }
        public string[] StartedIds() { lock (_lock) return Started.Select(x => x.AccountId).ToArray(); }

        public SyncState GetState(string accountId) => SyncState.Idle;
        public void MarkAuthFailed(string accountId) { }
        public void ClearAuthFailed(string accountId) { }
        public event EventHandler<AccountEventArgs>? StateChanged { add { } remove { } }
        public event EventHandler<AccountEventArgs>? SyncCompleted { add { } remove { } }
        public event EventHandler<NewMailEventArgs>? NewMailProcessed { add { } remove { } }
    }

    private static (SyncCoordinator Coord, GatedSync Sync, FakeAppState State) MakeGated(int count = 5)
    {
        var accounts = new InMemoryAccountRepository();
        for (var i = 1; i <= count; i++) accounts.Add(new Account($"a{i}", $"u{i}@gmail.com", $"N{i}", i - 1));
        var sync = new GatedSync();
        var state = new FakeAppState();
        return (new SyncCoordinator(sync, accounts, new FakeAppLog(), null, state), sync, state);
    }

    private static async Task WaitUntil(Func<bool> cond)
    {
        for (var i = 0; i < 200 && !cond(); i++) await Task.Delay(10);
        Assert.True(cond());
    }

    [Fact]
    public async Task Round_SyncsOneAtATime_DisplayedFirst_ThenRegistrationOrder()
    {
        var (c, sync, state) = MakeGated();
        state.CurrentAccountId = "a3";
        var round = c.SyncAllAsync(SyncTrigger.Timer);

        foreach (var expected in new[] { "a3", "a1", "a2", "a4", "a5" })
        {
            await WaitUntil(() => sync.StartedIds().Length > 0 && sync.StartedIds()[^1] == expected);
            // 前の 1 件が終わるまで次は始まらない
            Assert.Equal(Array.IndexOf(new[] { "a3", "a1", "a2", "a4", "a5" }, expected) + 1, sync.StartedIds().Length);
            sync.Release(expected);
        }
        await round;
        Assert.Equal(1, sync.MaxConcurrent);
    }

    [Fact]
    public async Task Round_NoDisplayedAccount_UsesRegistrationOrder()
    {
        var (c, sync, _) = MakeGated(3);
        var round = c.SyncAllAsync(SyncTrigger.Startup);
        foreach (var id in new[] { "a1", "a2", "a3" })
        {
            await WaitUntil(() => sync.StartedIds().Contains(id));
            sync.Release(id);
        }
        await round;
        Assert.Equal(new[] { "a1", "a2", "a3" }, sync.StartedIds());
    }

    [Fact]
    public async Task TickWhileRoundRuns_IsSkippedCompletely()
    {
        var (c, sync, _) = MakeGated(2);
        var round = c.SyncAllAsync(SyncTrigger.Startup);
        await WaitUntil(() => sync.StartedIds().Length == 1);

        await c.SyncAllAsync(SyncTrigger.Timer);          // 実行中なので即座に戻る
        sync.Release("a1");
        await WaitUntil(() => sync.StartedIds().Length == 2);
        sync.Release("a2");
        await round;

        Assert.Equal(new[] { "a1", "a2" }, sync.StartedIds());
        Assert.All(sync.Started, s => Assert.Equal(SyncTrigger.Startup, s.Trigger));
    }

    [Fact]
    public async Task ManualRefresh_OfQueuedAccount_MovesToFront()
    {
        var (c, sync, state) = MakeGated(4);
        state.CurrentAccountId = "a1";
        var round = c.SyncAllAsync(SyncTrigger.Timer);
        await WaitUntil(() => sync.StartedIds().Length == 1);   // a1 実行中。残りは a2 a3 a4

        c.RequestRefresh("a4");                                  // a4 を先頭へ
        sync.Release("a1");
        await WaitUntil(() => sync.StartedIds().Length == 2);
        Assert.Equal("a4", sync.StartedIds()[1]);
        Assert.Equal(SyncTrigger.Manual, sync.Started[1].Trigger);
        sync.Release("a4");
        foreach (var id in new[] { "a2", "a3" })
        {
            await WaitUntil(() => sync.StartedIds().Contains(id));
            sync.Release(id);
        }
        await round;
        Assert.Equal(new[] { "a1", "a4", "a2", "a3" }, sync.StartedIds());
    }

    [Fact]
    public async Task ManualRefresh_OfAccountAlreadySyncing_IsMerged()
    {
        var (c, sync, state) = MakeGated(2);
        state.CurrentAccountId = "a1";
        var round = c.SyncAllAsync(SyncTrigger.Timer);
        await WaitUntil(() => sync.StartedIds().Length == 1);

        c.RequestRefresh("a1");                                  // 実行中の同じ口座: 合流（2 回目は走らない）
        sync.Release("a1");
        await WaitUntil(() => sync.StartedIds().Length == 2);
        sync.Release("a2");
        await round;

        Assert.Equal(new[] { "a1", "a2" }, sync.StartedIds());
    }

    [Fact]
    public async Task ManualRefresh_WhenIdle_RunsThatAccountOnly_AndTickIsSkippedWhileItRuns()
    {
        var (c, sync, _) = MakeGated(3);
        c.RequestRefresh("a2");
        await WaitUntil(() => sync.StartedIds().Length == 1);

        await c.SyncAllAsync(SyncTrigger.Timer);                 // 手動の同期中は丸ごと見送り
        Assert.Equal(new[] { "a2" }, sync.StartedIds());
        sync.Release("a2");
        await Task.Delay(100);
        Assert.Equal(new[] { "a2" }, sync.StartedIds());
        Assert.Equal(1, sync.MaxConcurrent);
    }

    private sealed class ThrowingAccounts : IAccountRepository
    {
        public IReadOnlyList<Account> GetAll() => throw new IOException();
        public Account? Get(string accountId) => null;
        public Account Add(Account account) => account;
        public void Rename(string accountId, string displayName) { }
        public void Delete(string accountId) { }
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
