using System.Text;
using ParentMail.App.Features.SyncCache;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;
using static ParentMail.Tests.SyncCache.SyncHarness;

namespace ParentMail.Tests.SyncCache;

/// <summary>パート本文を返し、本文取得の前に処理を差し込めるゲートウェイ。</summary>
public sealed class ScriptGateway(FakeImapGateway inner) : PassThroughGateway(inner), IImapGateway
{
    public Dictionary<(string Folder, uint Uid, string Part), byte[]> Parts { get; } = new();
    public int PartCalls;
    public Func<Task>? OnFetchBody { get; set; }

    public override async Task<ImapBody?> FetchBodyAsync(string p, uint u, CancellationToken ct = default)
    {
        if (OnFetchBody is { } f) await f();
        return await base.FetchBodyAsync(p, u, ct);
    }

    Task<Stream?> IImapGateway.FetchPartAsync(string p, uint u, string part, CancellationToken ct)
    {
        Interlocked.Increment(ref PartCalls);
        return Task.FromResult<Stream?>(Parts.TryGetValue((p, u, part), out var b) ? new MemoryStream(b) : null);
    }
}

/// <summary>オンデマンド取得のテスト基盤。本文なしの古いメールを同期で作り、取得クラスを組む。</summary>
public sealed class FetchFixture : IDisposable
{
    public SyncHarness H { get; }
    public ScriptGateway Gw { get; }
    public FetchSession Session { get; }
    public PendingFetchTracker Pending { get; } = new();
    public BodyFetcher Fetcher { get; }
    public AttachmentService Attachments { get; }
    public List<long> Fetched { get; } = new();
    public string Acc => H.AccountId;

    public FetchFixture()
    {
        var inner = new FakeImapGateway();
        Gw = new ScriptGateway(inner);
        var factory = new DelegateGatewayFactory(_ => Gw);
        H = new SyncHarness(factory);
        // ハーネスが持つフェイクの中身をそのまま使う（フォルダ一覧など）
        inner.Folders.AddRange(H.Gateway.Folders);
        Session = new FetchSession(H.Accounts, factory, H.Log);
        Fetcher = new BodyFetcher(H.Cache.Mail, H.Cache.Files, Session, Pending, H.Log);
        Fetcher.BodyFetched += (_, e) => { lock (Fetched) Fetched.Add(e.GmailMsgId); };
        Attachments = new AttachmentService(H.Store, H.Cache.Mail, H.Cache.Files, Session);
        Inner = inner;
    }

    public FakeImapGateway Inner { get; }

    /// <summary>ヘッダと本文をフェイクに置く（ハーネスの Put は H.Gateway へ書くため、実際に使うフェイクへ移す）。</summary>
    public void Put(string folder, uint uid, long gmId, DateTime? date = null, string subject = "件名テスト", string? body = "既定の本文",
        IReadOnlyList<AttachmentInfo>? attachments = null)
    {
        H.Put(folder, uid, gmId, date, subject: subject, withBody: false);
        Inner.Headers[folder] = H.Gateway.Headers[folder].ToList();
        if (body is not null) Inner.Bodies[(folder, uid)] = new ImapBody(uid, body, null, attachments ?? Array.Empty<AttachmentInfo>());
    }

    public Task SyncAsync() => H.RunAsync();
    public int Calls(string prefix) => Inner.Calls.Count(c => c.StartsWith(prefix, StringComparison.Ordinal));
    public MailMessage Msg(long id) => H.Cache.Mail.Get(Acc, id)!;

    public void Dispose() => H.Dispose();
}

public class OnDemandFetchTests
{
    private static readonly DateTime Old = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // ---- 本文 ----

    [Fact]
    public async Task EnsureBody_Online_FetchesStoresAndNotifies()
    {
        using var f = new FetchFixture();
        f.Put(Inbox, 1, 101, date: Old, subject: "古いお知らせ", body: "日本語の本文です");
        await f.SyncAsync();
        Assert.Equal(BodyState.None, f.Msg(101).BodyState);

        var r = await f.Fetcher.EnsureBodyAsync(f.Acc, 101);

        Assert.Equal(BodyFetchResult.Ready, r);
        Assert.Equal(BodyState.Fetched, f.Msg(101).BodyState);
        Assert.Equal("日本語の本文です", f.Msg(101).BodyText);
        Assert.Equal(new long[] { 101 }, f.Fetched);
        Assert.Equal(0, f.Pending.Count(f.Acc));
    }

    [Fact]
    public async Task EnsureBody_AlreadyFetched_DoesNotTouchGateway()
    {
        using var f = new FetchFixture();
        f.Put(Inbox, 1, 101);
        await f.SyncAsync(); // 12 か月以内: 同期で本文まで取る
        var before = f.Inner.Calls.Count;

        var r = await f.Fetcher.EnsureBodyAsync(f.Acc, 101);

        Assert.Equal(BodyFetchResult.Ready, r);
        Assert.Equal(before, f.Inner.Calls.Count);
        Assert.Empty(f.Fetched);
    }

    [Fact]
    public async Task EnsureBody_Offline_ReturnsOffline_RegistersPending_ThenReconnectFetches()
    {
        using var f = new FetchFixture();
        f.Put(Inbox, 1, 101, date: Old, body: "つながったあとの本文");
        await f.SyncAsync();

        f.Inner.Offline = true;
        var r = await f.Fetcher.EnsureBodyAsync(f.Acc, 101);
        Assert.Equal(BodyFetchResult.Offline, r);
        Assert.True(f.Pending.Contains(f.Acc, 101));
        Assert.Equal(BodyState.None, f.Msg(101).BodyState);

        // まだ切れたまま同期が成功した扱いになっても、控えは積み直される
        var fake = new FakeSyncService();
        using var runner = new PendingFetchRunner(fake, f.Fetcher, f.Pending, f.H.Log);
        await runner.RunAsync(f.Acc);
        Assert.True(f.Pending.Contains(f.Acc, 101));

        f.Inner.Offline = false;
        await runner.RunAsync(f.Acc);

        Assert.Equal(BodyState.Fetched, f.Msg(101).BodyState);
        Assert.Equal("つながったあとの本文", f.Msg(101).BodyText);
        Assert.Equal(new long[] { 101 }, f.Fetched);
        Assert.Equal(0, f.Pending.Count(f.Acc));
    }

    [Fact]
    public async Task PendingFetchRunner_RunsAutomaticallyWhenSyncCompletes()
    {
        using var f = new FetchFixture();
        f.Put(Inbox, 1, 101, date: Old);
        await f.SyncAsync();
        f.Pending.Add(f.Acc, 101);

        var fake = new FakeSyncService();
        using var runner = new PendingFetchRunner(fake, f.Fetcher, f.Pending, f.H.Log);
        runner.Attach();
        await fake.SyncNowAsync(f.Acc, SyncTrigger.Reconnect); // SyncCompleted が上がる

        await WaitAsync(() => f.Msg(101).BodyState == BodyState.Fetched);
        Assert.Equal(BodyState.Fetched, f.Msg(101).BodyState);
    }

    [Fact]
    public async Task EnsureBody_NetworkErrorAtLogin_IsOffline()
    {
        using var f = new FetchFixture();
        f.Put(Inbox, 1, 101, date: Old);
        await f.SyncAsync();

        f.Inner.LoginResult = LoginResult.NetworkError;
        Assert.Equal(BodyFetchResult.Offline, await f.Fetcher.EnsureBodyAsync(f.Acc, 101));
        Assert.True(f.Pending.Contains(f.Acc, 101));
    }

    [Fact]
    public async Task EnsureBody_AuthFailedOrMissingOnServer_IsFailed_AndNotPending()
    {
        using var f = new FetchFixture();
        f.Put(Inbox, 1, 101, date: Old);
        f.Put(Inbox, 2, 102, date: Old, body: null); // サーバーに本文がない
        await f.SyncAsync();

        Assert.Equal(BodyFetchResult.Failed, await f.Fetcher.EnsureBodyAsync(f.Acc, 102));
        f.Inner.LoginResult = LoginResult.AuthFailed;
        Assert.Equal(BodyFetchResult.Failed, await f.Fetcher.EnsureBodyAsync(f.Acc, 101));
        Assert.Equal(BodyFetchResult.Failed, await f.Fetcher.EnsureBodyAsync(f.Acc, 99999)); // キャッシュにない
        Assert.Equal(0, f.Pending.Count(f.Acc));
        Assert.Empty(f.Fetched);
        // 本文を取れなかった内容はログに出ない
        Assert.DoesNotContain(f.H.Log.Entries, e => e.Contains("件名テスト"));
    }

    [Fact]
    public async Task EnsureBody_ConcurrentRequestsForSameMessage_FetchOnce()
    {
        using var f = new FetchFixture();
        f.Put(Inbox, 1, 101, date: Old);
        await f.SyncAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Gw.OnFetchBody = () => gate.Task;
        var before = f.Calls("FetchBody:");

        var t1 = f.Fetcher.EnsureBodyAsync(f.Acc, 101);
        var t2 = f.Fetcher.EnsureBodyAsync(f.Acc, 101);
        await Task.Delay(100);
        gate.SetResult();

        Assert.Equal(new[] { BodyFetchResult.Ready, BodyFetchResult.Ready }, await Task.WhenAll(t1, t2));
        Assert.Equal(before + 1, f.Calls("FetchBody:"));
    }

    [Fact]
    public async Task EnsureBody_FallsBackToAnotherFolderMembership()
    {
        using var f = new FetchFixture();
        f.Put(AllMail, 5, 101, date: Old, body: "すべてのメール側の本文");
        await f.SyncAsync();

        Assert.Equal(BodyFetchResult.Ready, await f.Fetcher.EnsureBodyAsync(f.Acc, 101));
        Assert.Equal("すべてのメール側の本文", f.Msg(101).BodyText);
    }

    // ---- 埋め込み画像 ----

    [Fact]
    public async Task EnsureBody_FetchesInlineParts_UpTo5MB_AsFiles()
    {
        using var f = new FetchFixture();
        var small = new AttachmentInfo { PartId = "2", FileName = "写真.png", ContentType = "image/png", Size = 4, IsInline = true, ContentId = "<c1>" };
        var big = new AttachmentInfo { PartId = "3", FileName = "大きな画像.png", ContentType = "image/png", Size = 6 * 1024 * 1024, IsInline = true, ContentId = "<c2>" };
        var doc = new AttachmentInfo { PartId = "4", FileName = "資料.pdf", ContentType = "application/pdf", Size = 4, IsInline = false };
        f.Put(Inbox, 1, 101, date: Old, attachments: new[] { small, big, doc });
        f.Gw.Parts[(Inbox, 1, "2")] = new byte[] { 1, 2, 3, 4 };
        f.Gw.Parts[(Inbox, 1, "3")] = new byte[] { 9 };
        f.Gw.Parts[(Inbox, 1, "4")] = new byte[] { 7, 7, 7, 7 };
        await f.SyncAsync();

        await f.Fetcher.EnsureBodyAsync(f.Acc, 101);

        var list = f.H.Cache.Mail.GetAttachments(f.Acc, 101);
        Assert.Equal(3, list.Count);
        var s = list.Single(a => a.PartId == "2");
        Assert.NotNull(s.LocalPath);
        Assert.True(f.H.Cache.Files.Exists(f.Acc, s.Id));
        Assert.Null(list.Single(a => a.PartId == "3").LocalPath); // 5 MB 超は取らない
        Assert.Null(list.Single(a => a.PartId == "4").LocalPath); // 通常の添付は押したときだけ
        Assert.Equal(1, f.Gw.PartCalls);
    }

    // ---- 添付 ----

    [Fact]
    public async Task EnsureLocal_DownloadsOnce_ThenServesFromCache()
    {
        using var f = new FetchFixture();
        var att = new AttachmentInfo { PartId = "2", FileName = "請求書_令和8年.pdf", ContentType = "application/pdf", Size = 5 };
        f.Put(Inbox, 1, 101, attachments: new[] { att });
        f.Gw.Parts[(Inbox, 1, "2")] = Encoding.UTF8.GetBytes("あいうえお");
        await f.SyncAsync();
        var id = f.H.Cache.Mail.GetAttachments(f.Acc, 101).Single().Id;

        var r1 = await f.Attachments.EnsureLocalAsync(f.Acc, id);
        Assert.Equal(AttachmentFetchStatus.Ready, r1.Status);
        Assert.Equal("あいうえお", await File.ReadAllTextAsync(r1.LocalPath!));
        Assert.Contains("親メール_テスト_", r1.LocalPath); // 日本語を含むパスでも動く
        Assert.Equal(1, f.Gw.PartCalls);
        var logins = f.Calls("TestLogin");

        var r2 = await f.Attachments.EnsureLocalAsync(f.Acc, id);

        Assert.Equal(AttachmentFetchStatus.Ready, r2.Status);
        Assert.Equal(r1.LocalPath, r2.LocalPath);
        Assert.Equal(1, f.Gw.PartCalls);              // 2 回目はゲートウェイを呼ばない
        Assert.Equal(logins, f.Calls("TestLogin"));
    }

    [Fact]
    public async Task EnsureLocal_Offline_Failed_AndMissingFileIsRefetched()
    {
        using var f = new FetchFixture();
        var att = new AttachmentInfo { PartId = "2", FileName = "写真.jpg", ContentType = "image/jpeg", Size = 3 };
        f.Put(Inbox, 1, 101, attachments: new[] { att, new AttachmentInfo { PartId = "3", FileName = "消えた.txt", ContentType = "text/plain", Size = 1 } });
        f.Gw.Parts[(Inbox, 1, "2")] = new byte[] { 1, 2, 3 };
        await f.SyncAsync();
        var list = f.H.Cache.Mail.GetAttachments(f.Acc, 101);
        var id = list.Single(a => a.PartId == "2").Id;
        var missing = list.Single(a => a.PartId == "3").Id;

        f.Inner.Offline = true;
        var off = await f.Attachments.EnsureLocalAsync(f.Acc, id);
        Assert.Equal(AttachmentFetchStatus.Offline, off.Status);
        Assert.Null(off.LocalPath);
        Assert.Equal(0, f.Pending.Count(f.Acc)); // 添付は再接続後に自動取得しない

        f.Inner.Offline = false;
        Assert.Equal(AttachmentFetchStatus.Failed, (await f.Attachments.EnsureLocalAsync(f.Acc, missing)).Status); // サーバーにパートがない
        Assert.Equal(AttachmentFetchStatus.Failed, (await f.Attachments.EnsureLocalAsync(f.Acc, 99999)).Status);

        var ok = await f.Attachments.EnsureLocalAsync(f.Acc, id);
        Assert.Equal(AttachmentFetchStatus.Ready, ok.Status);
        File.Delete(ok.LocalPath!);
        var again = await f.Attachments.EnsureLocalAsync(f.Acc, id);
        Assert.Equal(AttachmentFetchStatus.Ready, again.Status);
        Assert.True(File.Exists(again.LocalPath));
        Assert.Equal(2, f.Gw.PartCalls - 1 /* missing の試行 1 回 */);
    }

    // ---- 先読み ----

    private static (BackfillWorker Worker, FakeIdleMonitor Idle, FakeSyncService Sync) NewWorker(FetchFixture f, int perCycle = 20)
    {
        var idle = new FakeIdleMonitor { IdleFor = TimeSpan.FromSeconds(61) };
        var sync = new FakeSyncService();
        return (new BackfillWorker(idle, sync, f.H.Accounts, f.H.Store, f.Fetcher, f.H.Log, perCycle: perCycle), idle, sync);
    }

    private static async Task PutOldAsync(FetchFixture f, int count)
    {
        for (uint i = 1; i <= count; i++)
            f.Put(Inbox, i, 100 + i, date: Old.AddDays(i), body: "本文" + i);
        await f.SyncAsync();
    }

    [Fact]
    public async Task Backfill_FetchesNewestFirst_UpToPerCycle_AndSkipsFetchedBodies()
    {
        using var f = new FetchFixture();
        await PutOldAsync(f, 5);
        await f.Fetcher.EnsureBodyAsync(f.Acc, 105); // 最新は取得済み
        var (w, _, _) = NewWorker(f, perCycle: 2);
        var before = f.Calls("FetchBody:");
        var callIndex = f.Inner.Calls.Count;

        var n = await w.RunCycleAsync();

        Assert.Equal(2, n);
        Assert.Equal(before + 2, f.Calls("FetchBody:"));
        Assert.Equal(BodyState.Fetched, f.Msg(104).BodyState);
        Assert.Equal(BodyState.Fetched, f.Msg(103).BodyState);
        Assert.Equal(BodyState.None, f.Msg(102).BodyState);
        Assert.DoesNotContain($"FetchBody:{Inbox}:5", f.Inner.Calls.Skip(callIndex)); // 取得済みは要求しない
        Assert.Equal(0, f.Pending.Count(f.Acc));
    }

    [Fact]
    public async Task Backfill_DoesNothing_WhenNotIdleLongEnough_OrSyncRunning_OrOffline()
    {
        using var f = new FetchFixture();
        await PutOldAsync(f, 3);
        var (w, idle, sync) = NewWorker(f);
        var before = f.Calls("FetchBody:");

        idle.IdleFor = TimeSpan.FromSeconds(59);
        Assert.Equal(0, await w.RunCycleAsync());

        idle.IdleFor = TimeSpan.FromSeconds(60);
        sync.States[f.Acc] = SyncState.Syncing;
        Assert.Equal(0, await w.RunCycleAsync());

        sync.States[f.Acc] = SyncState.Offline;
        Assert.Equal(0, await w.RunCycleAsync());

        Assert.Equal(before, f.Calls("FetchBody:"));
        sync.States[f.Acc] = SyncState.Idle;
        Assert.Equal(3, await w.RunCycleAsync()); // 条件が揃えば動く
    }

    [Fact]
    public async Task Backfill_StopsOnUserActivity()
    {
        using var f = new FetchFixture();
        await PutOldAsync(f, 4);
        var (w, idle, _) = NewWorker(f);
        // 最初の本文を取っている最中に利用者が操作する
        f.Gw.OnFetchBody = () => { idle.RaiseUserActivity(); return Task.CompletedTask; };
        var before = f.Calls("FetchBody:");

        var n = await w.RunCycleAsync();

        Assert.True(n <= 1);
        Assert.Equal(before + 1, f.Calls("FetchBody:")); // 2 通目には進まない
    }

    [Fact]
    public async Task Backfill_StopsWhenSyncStarts()
    {
        using var f = new FetchFixture();
        await PutOldAsync(f, 4);
        var (w, _, sync) = NewWorker(f);
        f.Gw.OnFetchBody = () => { sync.RaiseStateChanged(f.Acc, SyncState.Syncing); return Task.CompletedTask; };
        var before = f.Calls("FetchBody:");

        await w.RunCycleAsync();

        Assert.Equal(before + 1, f.Calls("FetchBody:"));
    }

    [Fact]
    public async Task Backfill_OfflineEndsCycle_WithoutPending_AndFailedMessagesAreNotRetried()
    {
        using var f = new FetchFixture();
        await PutOldAsync(f, 3);
        f.Put(Inbox, 9, 109, date: Old.AddDays(100), body: null); // 最新だがサーバーに本文がない
        await f.SyncAsync();
        var (w, _, _) = NewWorker(f);

        f.Inner.Offline = true;
        Assert.Equal(0, await w.RunCycleAsync());
        Assert.Equal(0, f.Pending.Count(f.Acc));
        f.Inner.Offline = false;

        Assert.Equal(3, await w.RunCycleAsync()); // 109 は失敗、他の 3 通は取れる
        var before = f.Calls("FetchBody:");
        Assert.Equal(0, await w.RunCycleAsync());  // 失敗したものは次の周期では要求しない
        Assert.Equal(before, f.Calls("FetchBody:"));
    }

    [Fact]
    public async Task Backfill_StartStop_RunsOnTimerWhenIdle()
    {
        using var f = new FetchFixture();
        await PutOldAsync(f, 2);
        var idle = new FakeIdleMonitor { IdleFor = TimeSpan.FromSeconds(120) };
        using var w = new BackfillWorker(idle, new FakeSyncService(), f.H.Accounts, f.H.Store, f.Fetcher, f.H.Log,
            tick: TimeSpan.FromMilliseconds(30));

        w.Start();
        await WaitAsync(() => f.Msg(101).BodyState == BodyState.Fetched && f.Msg(102).BodyState == BodyState.Fetched);
        w.Stop();

        Assert.Equal(BodyState.Fetched, f.Msg(101).BodyState);
    }

    // ---- 入力監視・控え ----

    [Fact]
    public void InputIdleMonitor_TracksIdleTimeAndRaisesActivity()
    {
        long now = 1_000;
        var m = new InputIdleMonitor(() => now);
        var raised = 0;
        m.UserActivity += (_, _) => raised++;

        now = 61_000;
        Assert.Equal(TimeSpan.FromSeconds(60), m.IdleFor);
        m.NotifyActivity();
        Assert.Equal(TimeSpan.Zero, m.IdleFor);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void PendingFetchTracker_AddDrain_IsPerAccountAndDeduplicated()
    {
        var t = new PendingFetchTracker();
        t.Add("a", 1);
        t.Add("a", 1);
        t.Add("a", 2);
        t.Add("b", 3);

        Assert.Equal(2, t.Count("a"));
        Assert.Equal(new long[] { 1, 2 }, t.Drain("a").OrderBy(x => x));
        Assert.Empty(t.Drain("a"));
        Assert.True(t.Contains("b", 3));
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(30);
    }
}
