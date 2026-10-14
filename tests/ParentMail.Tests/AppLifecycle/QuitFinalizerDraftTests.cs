using System.Diagnostics;
using NSubstitute;
using ParentMail.App.Features.AppLifecycle;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AppLifecycle;

// 終了の後片付けは、閉じた直後の下書き反映を上限つきで待ってから同期を止める (Issue #4)。
public sealed class QuitFinalizerDraftTests
{
    private static SyncCoordinator Coordinator()
        => new(Substitute.For<ISyncService>(), new InMemoryAccountRepository(), new FakeAppLog());

    [Fact]
    public async Task Quit_waits_until_background_drafts_finish()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drafts = new FakeDraftService { OnWait = _ => release.Task };
        var finalizer = new QuitFinalizer(Coordinator(), drafts, TimeSpan.FromSeconds(30));

        var finishing = Task.Run(finalizer.FinishQuit);
        await Task.Delay(300);
        Assert.False(finishing.IsCompleted); // 反映が終わるまで終了を進めない

        release.SetResult();
        Assert.Same(finishing, await Task.WhenAny(finishing, Task.Delay(TimeSpan.FromSeconds(10))));
    }

    [Fact]
    public void Quit_returns_after_the_limit_when_drafts_never_finish()
    {
        var drafts = new FakeDraftService { OnWait = _ => new TaskCompletionSource().Task }; // 永遠に終わらない
        var finalizer = new QuitFinalizer(Coordinator(), drafts, TimeSpan.FromMilliseconds(200));

        var sw = Stopwatch.StartNew();
        finalizer.FinishQuit();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Quit_continues_when_the_wait_throws()
    {
        var drafts = new FakeDraftService { OnWait = _ => throw new InvalidOperationException("boom") };
        new QuitFinalizer(Coordinator(), drafts, TimeSpan.FromSeconds(1)).FinishQuit();
    }

    [Fact]
    public void Quit_without_a_draft_service_just_stops_sync()
        => new QuitFinalizer(Coordinator()).FinishQuit();
}
