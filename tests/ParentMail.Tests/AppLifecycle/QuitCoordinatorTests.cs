using ParentMail.App.Features.AppLifecycle;
using ParentMail.Core;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AppLifecycle;

// 終了の判定表（作成窓の確認 -> 送信待ちの 3 択 -> 後片付け）のテスト。
public sealed class QuitCoordinatorTests
{
    private sealed class FakeCompose : IQuitComposeWindows
    {
        public int Count { get; set; }
        public bool CloseAllResult { get; set; } = true;
        public int CloseCalls { get; private set; }
        public Task<bool> CloseAllAsync() { CloseCalls++; return Task.FromResult(CloseAllResult); }
    }

    private sealed class FakePrompt : IQuitPrompt
    {
        public QuitChoice Choice { get; set; } = QuitChoice.Back;
        public int AskCalls { get; private set; }
        public int BusyShown { get; private set; }
        public int BusyDisposed { get; private set; }
        public QuitChoice AskUnsentMail() { AskCalls++; return Choice; }
        public IDisposable ShowBusy()
        {
            BusyShown++;
            return new Releaser(this);
        }
        private sealed class Releaser : IDisposable
        {
            private readonly FakePrompt _owner;
            public Releaser(FakePrompt owner) => _owner = owner;
            public void Dispose() => _owner.BusyDisposed++;
        }
    }

    private sealed class FakeFinalizer : IQuitFinalizer
    {
        public int Calls { get; private set; }
        public void FinishQuit() => Calls++;
    }

    private readonly FakeCompose _compose = new();
    private readonly FakePrompt _prompt = new();
    private readonly FakeFinalizer _final = new();
    private readonly FakeOutboxService _outbox = new();
    private readonly FakeAppLog _log = new();

    private QuitCoordinator Create() => new(_compose, _outbox, _prompt, _final, _log);

    [Theory]
    [InlineData(QuitReason.MainWindowClosed)]
    [InlineData(QuitReason.SessionEnding)]
    public async Task Nothing_to_warn_quits_without_dialog(QuitReason reason)
    {
        Assert.True(await Create().RequestQuitAsync(reason));
        Assert.Equal(0, _prompt.AskCalls);
        Assert.Equal(1, _final.Calls);
    }

    [Fact]
    public async Task Compose_back_aborts_quit_before_outbox_is_asked()
    {
        _compose.Count = 1;
        _compose.CloseAllResult = false;
        _outbox.Pending["a1"] = 2;

        Assert.False(await Create().RequestQuitAsync(QuitReason.MainWindowClosed));
        Assert.Equal(0, _prompt.AskCalls);
        Assert.Equal(0, _final.Calls);
    }

    [Fact]
    public async Task Compose_closed_then_empty_outbox_quits()
    {
        _compose.Count = 2;
        Assert.True(await Create().RequestQuitAsync(QuitReason.MainWindowClosed));
        Assert.Equal(1, _compose.CloseCalls);
        Assert.Equal(1, _final.Calls);
    }

    [Fact]
    public async Task Pending_back_aborts()
    {
        _outbox.Pending["a1"] = 1;
        _prompt.Choice = QuitChoice.Back;

        Assert.False(await Create().RequestQuitAsync(QuitReason.MainWindowClosed));
        Assert.Equal(1, _prompt.AskCalls);
        Assert.Equal(0, _final.Calls);
    }

    [Fact]
    public async Task Pending_close_as_is_quits_without_sending()
    {
        _outbox.Pending["a1"] = 1;
        _prompt.Choice = QuitChoice.CloseAsIs;

        Assert.True(await Create().RequestQuitAsync(QuitReason.MainWindowClosed));
        Assert.Equal(0, _prompt.BusyShown);
        Assert.Equal(1, _final.Calls);
    }

    [Fact]
    public async Task Pending_send_all_sent_shows_busy_then_quits()
    {
        _outbox.Pending["a1"] = 1;
        _prompt.Choice = QuitChoice.SendAndClose;
        _outbox.SendAllResult = SendAllResult.AllSent;

        Assert.True(await Create().RequestQuitAsync(QuitReason.MainWindowClosed));
        Assert.Equal(1, _prompt.BusyShown);
        Assert.Equal(1, _prompt.BusyDisposed);
        Assert.Equal(1, _final.Calls);
    }

    [Fact]
    public async Task Pending_send_failure_keeps_app_open()
    {
        _outbox.Pending["a1"] = 1;
        _prompt.Choice = QuitChoice.SendAndClose;
        _outbox.SendAllResult = SendAllResult.SomeFailed;

        Assert.False(await Create().RequestQuitAsync(QuitReason.MainWindowClosed));
        Assert.Equal(1, _prompt.BusyDisposed);
        Assert.Equal(0, _final.Calls);
    }

    [Fact]
    public async Task Pending_is_counted_across_both_accounts()
    {
        _outbox.Pending["a1"] = 0;
        _outbox.Pending["a2"] = 1;
        await Create().RequestQuitAsync(QuitReason.MainWindowClosed);
        Assert.Equal(1, _prompt.AskCalls);
    }

    [Fact]
    public async Task Second_request_while_running_is_refused()
    {
        var gate = new TaskCompletionSource<bool>();
        var slow = new SlowCompose(gate.Task);
        var coordinator = new QuitCoordinator(slow, _outbox, _prompt, _final, _log);

        var first = coordinator.RequestQuitAsync(QuitReason.MainWindowClosed);
        Assert.False(await coordinator.RequestQuitAsync(QuitReason.MainWindowClosed));
        gate.SetResult(true);
        Assert.True(await first);
        Assert.Equal(1, _final.Calls);
    }

    private sealed class SlowCompose : IQuitComposeWindows
    {
        private readonly Task<bool> _task;
        public SlowCompose(Task<bool> task) => _task = task;
        public int Count => 1;
        public Task<bool> CloseAllAsync() => _task;
    }

    [Fact]
    public async Task Request_can_be_retried_after_back()
    {
        _outbox.Pending["a1"] = 1;
        var coordinator = Create();
        _prompt.Choice = QuitChoice.Back;
        Assert.False(await coordinator.RequestQuitAsync(QuitReason.MainWindowClosed));
        _prompt.Choice = QuitChoice.CloseAsIs;
        Assert.True(await coordinator.RequestQuitAsync(QuitReason.MainWindowClosed));
    }

    [Fact]
    public async Task Exception_during_quit_is_logged_and_does_not_trap_the_user()
    {
        _outbox.Pending["a1"] = 1;
        _prompt.Choice = QuitChoice.CloseAsIs;
        var coordinator = new QuitCoordinator(_compose, _outbox, _prompt, new ThrowingFinalizer(), _log);

        Assert.True(await coordinator.RequestQuitAsync(QuitReason.MainWindowClosed));
        Assert.Contains(_log.Entries, e => e.StartsWith("E:Quit"));
    }

    private sealed class ThrowingFinalizer : IQuitFinalizer
    {
        public void FinishQuit() => throw new InvalidOperationException("boom");
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(0, 1, true)]
    [InlineData(2, 3, true)]
    public void NeedsAttention_is_true_with_open_compose_or_pending_mail(int composeCount, int pending, bool expected)
    {
        _compose.Count = composeCount;
        if (pending > 0) _outbox.Pending["a1"] = pending;
        Assert.Equal(expected, Create().NeedsAttention);
    }
}
