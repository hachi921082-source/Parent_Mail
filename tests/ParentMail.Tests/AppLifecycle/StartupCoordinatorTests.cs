using ParentMail.App.Features.AppLifecycle;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AppLifecycle;

// 起動手順（0 / 1 / 2 アカウント）のテスト。最後に使ったアカウントの選択は AccountSwitchViewModelTests が受け持つ。
public sealed class StartupCoordinatorTests
{
    private sealed class RecordingSteps : IStartupSteps
    {
        public List<string> Calls { get; } = new();
        public void ShowMainWindow() => Calls.Add("show");
        public void InitializeAccounts() => Calls.Add("init");
        public void StartBackground() => Calls.Add("start");
    }

    private readonly InMemoryAccountRepository _repo = new();
    private readonly RecordingSteps _steps = new();
    private readonly FakeAppLog _log = new();

    private async Task Run(int accounts)
    {
        for (var i = 0; i < accounts; i++) _repo.Add(new Account("a" + i, $"a{i}@gmail.com", "name" + i, i));
        await new StartupCoordinator(_repo, _steps, _log).RunAsync();
    }

    [Fact]
    public async Task Zero_accounts_shows_window_and_starts_background_without_selecting_an_account()
    {
        await Run(0);
        Assert.Equal(new[] { "show", "start" }, _steps.Calls);
        Assert.Contains(_log.Entries, e => e.Contains("first launch"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task One_or_two_accounts_select_account_after_window_then_start_background(int accounts)
    {
        await Run(accounts);
        Assert.Equal(new[] { "show", "init", "start" }, _steps.Calls);
        Assert.DoesNotContain(_log.Entries, e => e.Contains("first launch"));
    }

    [Fact]
    public async Task Second_run_does_nothing()
    {
        var c = new StartupCoordinator(_repo, _steps, _log);
        await c.RunAsync();
        await c.RunAsync();
        Assert.Equal(2, _steps.Calls.Count);
    }

    [Fact]
    public async Task Failure_in_background_start_is_logged_and_not_thrown()
    {
        var c = new StartupCoordinator(_repo, new ThrowingSteps(), _log);
        await c.RunAsync();
        Assert.Contains(_log.Entries, e => e.StartsWith("E:Startup"));
    }

    private sealed class ThrowingSteps : IStartupSteps
    {
        public void ShowMainWindow() { }
        public void InitializeAccounts() { }
        public void StartBackground() => throw new InvalidOperationException("boom");
    }
}
