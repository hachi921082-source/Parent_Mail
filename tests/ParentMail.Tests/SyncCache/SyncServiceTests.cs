using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;
using static ParentMail.Tests.SyncCache.SyncHarness;

namespace ParentMail.Tests.SyncCache;

public class SyncServiceTests
{
    // ---- 初回同期 ----

    [Fact]
    public async Task InitialSync_PopulatesHeadersAndBodies_WithoutRunningRules()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        h.Put(Inbox, 2, 102, read: true);
        h.Put(Sent, 1, 201);

        await h.RunAsync(SyncTrigger.Startup);

        Assert.Equal(SyncState.Idle, h.Sync.GetState(h.AccountId));
        Assert.Equal(2, h.Cache.Mail.GetList(h.AccountId, FolderKind.Inbox).Count);
        Assert.Single(h.Cache.Mail.GetList(h.AccountId, FolderKind.Sent));
        Assert.Equal("本文101", h.Cache.Mail.Get(h.AccountId, 101)!.BodyText);
        Assert.True(h.Cache.Mail.Get(h.AccountId, 102)!.IsRead);
        Assert.True(h.Store.IsInitialSyncDone(h.AccountId));
        Assert.Empty(h.Rule.Applied);          // 初回同期では規則を動かさない（A43）
        Assert.Empty(h.NewMails);
        Assert.Equal(1u, h.Cache.Mail.GetFolderState(h.AccountId, FolderKind.Inbox)!.UidValidity);
    }

    [Fact]
    public async Task Sync_StoresReplyToFromHeader()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        h.Put(Inbox, 2, 102);
        h.Gateway.Headers[Inbox][0] = h.Gateway.Headers[Inbox][0] with { ReplyTo = "返信係 <reply@example.org>" };

        await h.RunAsync();

        Assert.Equal("返信係 <reply@example.org>", h.Cache.Mail.Get(h.AccountId, 101)!.ReplyTo);
        Assert.Null(h.Cache.Mail.Get(h.AccountId, 102)!.ReplyTo);
    }

    [Fact]
    public async Task InitialSync_FetchesBodiesOnlyWithin12Months_AndSkipsAllMailDuplicates()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        h.Put(Inbox, 2, 102, date: h.Now.AddMonths(-14));   // 古い: ヘッダだけ
        h.Put(AllMail, 11, 101);                            // Inbox と重複: 本文は 1 回だけ
        h.Put(AllMail, 12, 301);                            // AllMail だけ: 本文あり

        await h.RunAsync();

        Assert.Equal(BodyState.Fetched, h.Cache.Mail.Get(h.AccountId, 101)!.BodyState);
        Assert.Equal(BodyState.None, h.Cache.Mail.Get(h.AccountId, 102)!.BodyState);
        Assert.Equal(BodyState.Fetched, h.Cache.Mail.Get(h.AccountId, 301)!.BodyState);
        Assert.DoesNotContain($"FetchBody:{AllMail}:11", h.Gateway.Calls);
        Assert.Contains($"FetchBody:{AllMail}:12", h.Gateway.Calls);
        Assert.Equal(2, h.Cache.Mail.GetMemberships(h.AccountId, 101).Count);
        // 他フォルダにない AllMail だけのメールも検索対象に入る
        Assert.Contains(h.Cache.Mail.Search(h.AccountId, "山田", 10), m => m.GmailMsgId == 301);
    }

    [Fact]
    public async Task InitialSync_ReportsProgressPerBatch_SoListFillsGradually()
    {
        using var h = new SyncHarness();
        for (uint i = 1; i <= 450; i++) h.Put(Inbox, i, 1000 + i, date: h.Now.AddMinutes(-i), withBody: false);

        await h.RunAsync();

        // 450 通 = 3 バッチ。開始と終了の状態変化に加えて、バッチごとの通知がある
        Assert.True(h.StateChangedCount >= 5);
        Assert.Equal(450, h.Cache.Mail.GetList(h.AccountId, FolderKind.Inbox).Count);
    }

    // ---- 差分同期 ----

    [Fact]
    public async Task PeriodicSync_NewInboxMail_RunsRulesAndRaisesNewMail()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        await h.RunAsync();

        h.Put(Inbox, 2, 102, subject: "新着のお知らせ");
        await h.RunAsync(SyncTrigger.Timer);

        Assert.Single(h.Rule.Applied);
        Assert.Equal(102, h.Rule.Applied[0].GmailMsgId);
        Assert.Equal("本文102", h.Rule.Applied[0].BodyText);     // 規則には本文まで取得済みのメールを渡す
        Assert.Single(h.NewMails);

        await h.RunAsync(SyncTrigger.Timer);                      // もう一度回しても二重には適用しない
        Assert.Single(h.Rule.Applied);
    }

    [Fact]
    public async Task PeriodicSync_NonInboxNewMail_NeverEntersPipeline()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        await h.RunAsync();

        h.Put(Sent, 5, 501);
        h.Put(Spam, 6, 601);
        h.Put(AllMail, 7, 701);
        await h.RunAsync();

        Assert.Empty(h.Rule.Applied);
        Assert.Empty(h.NewMails);
        Assert.NotNull(h.Cache.Mail.Get(h.AccountId, 501));
    }

    [Fact]
    public async Task PeriodicSync_RemoteReadChangeAndExpunge_AreMirrored()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        h.Put(Inbox, 2, 102);
        await h.RunAsync();

        h.Put(Inbox, 1, 101, read: true);     // 他の端末で既読
        h.Remove(Inbox, 2);                   // Gmail 側で完全削除
        await h.RunAsync();

        Assert.True(h.Cache.Mail.Get(h.AccountId, 101)!.IsRead);
        Assert.Null(h.Cache.Mail.Get(h.AccountId, 102));
    }

    [Fact]
    public async Task PeriodicSync_MoveToTrash_IsMirroredAsMembershipChange_KeepingBody()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        h.Put(AllMail, 11, 101);
        await h.RunAsync();

        // Gmail: 受信トレイとすべてのメールから外れ、ごみ箱に入る（UID は変わる）
        h.Remove(Inbox, 1);
        h.Remove(AllMail, 11);
        h.Put(Trash, 21, 101, withBody: false);
        await h.RunAsync();

        var mems = h.Cache.Mail.GetMemberships(h.AccountId, 101);
        Assert.Single(mems);
        Assert.Equal(FolderKind.Trash, mems[0].Folder);
        Assert.Equal("本文101", h.Cache.Mail.Get(h.AccountId, 101)!.BodyText);
        Assert.Empty(h.Cache.Mail.GetList(h.AccountId, FolderKind.Inbox));
        Assert.Single(h.Cache.Mail.GetList(h.AccountId, FolderKind.Trash));
        Assert.Empty(h.Rule.Applied);          // ごみ箱への移動は新着ではない
    }

    [Fact]
    public async Task PeriodicSync_SkipsFolderWhenNothingChanged()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        h.Gateway.Statuses[Inbox] = new ImapFolderStatus(Inbox, 1, 2, 50, 1);
        await h.RunAsync();
        h.Gateway.Calls.Clear();

        await h.RunAsync();

        Assert.DoesNotContain("SearchUids:" + Inbox, h.Gateway.Calls);   // 変更番号・次の UID・件数が同じなら一覧を取り直さない
    }

    // ---- UIDVALIDITY ----

    [Fact]
    public async Task UidValidityChange_KeepsBodies_RebuildsMemberships_AndRunsNoRules()
    {
        using var h = new SyncHarness();
        h.SetStatus(Inbox, validity: 1);
        h.Put(Inbox, 10, 101);
        await h.RunAsync();
        Assert.Empty(h.Rule.Applied);

        // UIDVALIDITY が変わり UID が振り直された。ついでに知らないメールも 1 通ある
        h.Remove(Inbox, 10);
        h.Put(Inbox, 110, 101, withBody: false);
        h.Put(Inbox, 111, 999);
        h.SetStatus(Inbox, validity: 2);
        await h.RunAsync(SyncTrigger.Timer);

        var m = h.Cache.Mail.Get(h.AccountId, 101)!;
        Assert.Equal("本文101", m.BodyText);                               // 本文は残る
        Assert.Equal(110u, h.Cache.Mail.GetMemberships(h.AccountId, 101).Single(x => x.Folder == FolderKind.Inbox).Uid);
        Assert.NotNull(h.Cache.Mail.Get(h.AccountId, 999));
        Assert.Empty(h.Rule.Applied);                                     // 規則は動かさない
        Assert.Empty(h.NewMails);
        Assert.Equal(2u, h.Cache.Mail.GetFolderState(h.AccountId, FolderKind.Inbox)!.UidValidity);
        Assert.DoesNotContain($"FetchBody:{Inbox}:110", h.Gateway.Calls); // 既知メールの本文は取り直さない
    }

    // ---- 排他・状態 ----

    [Fact]
    public async Task MutualExclusion_TimerTickSkipped_ManualRefreshMerged()
    {
        var inner = new FakeImapGateway();
        var gated = new GatedGateway(inner);
        using var h = new SyncHarness(new DelegateGatewayFactory(_ => gated));
        inner.Folders.AddRange(h.Gateway.Folders);

        var first = h.RunAsync(SyncTrigger.Startup);
        await Task.Delay(100);
        Assert.Equal(SyncState.Syncing, h.Sync.GetState(h.AccountId));

        var timer = h.RunAsync(SyncTrigger.Timer);
        var manual = h.RunAsync(SyncTrigger.Manual);
        Assert.True(timer.IsCompletedSuccessfully);        // 実行中のタイマーは捨てる
        Assert.False(manual.IsCompleted);                  // 手動は合流して待つ

        gated.Gate.SetResult();
        await Task.WhenAll(first, manual);

        Assert.Equal(1, gated.Logins);                     // 同期は 1 回だけ走った
        Assert.Equal(SyncState.Idle, h.Sync.GetState(h.AccountId));
    }

    [Fact]
    public async Task Offline_SetsOfflineState_AndRecoversOnNextSuccess()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        h.Gateway.Offline = true;

        await h.RunAsync();
        Assert.Equal(SyncState.Offline, h.Sync.GetState(h.AccountId));
        Assert.Empty(h.Outbox.RetryAccounts);              // オフラインでは送信箱の再送もしない

        h.Gateway.Offline = false;
        await h.RunAsync();
        Assert.Equal(SyncState.Idle, h.Sync.GetState(h.AccountId));
    }

    [Fact]
    public async Task NetworkLoginError_IsOffline()
    {
        using var h = new SyncHarness();
        h.Gateway.LoginResult = LoginResult.NetworkError;
        await h.RunAsync();
        Assert.Equal(SyncState.Offline, h.Sync.GetState(h.AccountId));
    }

    [Fact]
    public async Task AuthFailure_StopsSync_UntilCleared()
    {
        using var h = new SyncHarness();
        h.Gateway.LoginResult = LoginResult.AuthFailed;
        await h.RunAsync();

        Assert.Equal(SyncState.Error, h.Sync.GetState(h.AccountId));
        Assert.True(h.Sync.IsAuthFailed(h.AccountId));

        h.Gateway.Calls.Clear();
        await h.RunAsync();
        Assert.Empty(h.Gateway.Calls);                     // 入れ直しまで接続しない

        h.Gateway.LoginResult = LoginResult.Ok;
        h.Sync.ClearAuthFailed(h.AccountId);
        await h.RunAsync();
        Assert.Equal(SyncState.Idle, h.Sync.GetState(h.AccountId));
    }

    [Fact]
    public async Task MarkAuthFailed_SetsErrorStateWithoutConnecting()
    {
        using var h = new SyncHarness();
        h.Sync.MarkAuthFailed(h.AccountId);
        Assert.Equal(SyncState.Error, h.Sync.GetState(h.AccountId));
        await h.RunAsync();
        Assert.Empty(h.Gateway.Calls);
    }

    [Fact]
    public async Task OtherFailure_IsErrorState_AndLogDoesNotLeakContent()
    {
        var inner = new FakeImapGateway();
        using var h = new SyncHarness(new DelegateGatewayFactory(_ => new ThrowingGateway(inner)));
        inner.Folders.AddRange(h.Gateway.Folders);

        await h.RunAsync();

        Assert.Equal(SyncState.Error, h.Sync.GetState(h.AccountId));
        Assert.False(h.Sync.IsAuthFailed(h.AccountId));
        Assert.All(h.Log.Entries, e => Assert.DoesNotContain("秘密", e));
    }

    // ---- 受信パイプライン ----

    [Fact]
    public async Task Pipeline_RuleMovesMailToSpam_ViaGatewayAndMembership_NoNewMailEvent()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        await h.RunAsync();

        h.Rule.OnApply = async (m, ctx) => { await ctx.Actions.MoveAsync(m.GmailMsgId, FolderKind.Spam); return true; };
        h.Put(Inbox, 2, 102);
        await h.RunAsync();

        Assert.Contains($"Move:{Inbox}->{Spam}", h.Gateway.Calls);
        Assert.DoesNotContain(h.Cache.Mail.GetMemberships(h.AccountId, 102), x => x.Folder == FolderKind.Inbox);
        Assert.Empty(h.NewMails);
    }

    [Fact]
    public async Task Pipeline_RuleActionFails_MailStaysInInbox_AndIsLogged()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        await h.RunAsync();

        h.Rule.OnApply = (_, _) => throw new IOException("network");
        h.Put(Inbox, 2, 102);
        await h.RunAsync();

        Assert.Contains(h.Cache.Mail.GetMemberships(h.AccountId, 102), x => x.Folder == FolderKind.Inbox);
        Assert.Contains(h.Log.Entries, e => e.StartsWith("W:Sync"));
        Assert.Single(h.NewMails);
        Assert.Equal(SyncState.Idle, h.Sync.GetState(h.AccountId));
    }

    [Fact]
    public async Task Pipeline_RulesRunInRegisteredOrder_AndStopAtFirstHandled()
    {
        var block = new ScriptedRule { Handled = true };
        var filter = new ScriptedRule();
        using var h = new SyncHarness(rules: new IIncomingRule[] { block, filter });
        h.Put(Inbox, 1, 101);
        await h.RunAsync();

        h.Put(Inbox, 2, 102);
        await h.RunAsync();

        Assert.Single(block.Applied);
        Assert.Empty(filter.Applied);
    }

    // ---- 既読の同期 ----

    [Fact]
    public async Task SetSeen_Online_WritesToAllMemberships()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        h.Put(AllMail, 11, 101);
        await h.RunAsync();
        h.Gateway.Calls.Clear();

        await h.Sync.SetSeenAsync(h.AccountId, 101, true);

        Assert.True(h.Cache.Mail.Get(h.AccountId, 101)!.IsRead);
        Assert.Contains($"SetSeen:{Inbox}:True", h.Gateway.Calls);
        Assert.Contains($"SetSeen:{AllMail}:True", h.Gateway.Calls);
        Assert.Empty(h.Store.GetPending(h.AccountId));
    }

    [Fact]
    public async Task SetSeen_Offline_QueuesAndFlushesAtNextSync()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        await h.RunAsync();

        h.Gateway.Offline = true;
        await h.Sync.SetSeenAsync(h.AccountId, 101, true);
        Assert.True(h.Cache.Mail.Get(h.AccountId, 101)!.IsRead);         // 画面にはすぐ反映
        Assert.Single(h.Store.GetPending(h.AccountId));

        h.Gateway.Offline = false;
        h.Gateway.Calls.Clear();
        await h.RunAsync();

        Assert.Contains($"SetSeen:{Inbox}:True", h.Gateway.Calls);
        Assert.Empty(h.Store.GetPending(h.AccountId));
    }

    [Fact]
    public async Task SetSeen_DuringSync_IsQueued_AndNotOverwrittenByRemoteFlags()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);          // Gmail 側は未読のまま
        await h.RunAsync();

        // 次の同期中（新着 102 の処理中）にユーザーが 101 を開いて既読にした
        h.Rule.OnApply = async (_, _) => { await h.Sync.SetSeenAsync(h.AccountId, 101, true); return false; };
        h.Put(Inbox, 2, 102);
        await h.RunAsync();

        Assert.True(h.Cache.Mail.Get(h.AccountId, 101)!.IsRead);          // 古い Gmail の状態で戻されない
        Assert.Single(h.Store.GetPending(h.AccountId));

        h.Rule.OnApply = null;
        await h.RunAsync();                                                // 次回の同期で反映される
        Assert.Empty(h.Store.GetPending(h.AccountId));
        Assert.Contains($"SetSeen:{Inbox}:True", h.Gateway.Calls);
    }

    // ---- 送信箱の再送ほか ----

    [Fact]
    public async Task OutboxRetry_IsCalledAfterSuccessfulSync()
    {
        using var h = new SyncHarness();
        await h.RunAsync();
        Assert.Equal(new[] { h.AccountId }, h.Outbox.RetryAccounts);
    }

    [Fact]
    public async Task SyncWorksWithoutOutboxService()
    {
        using var h = new SyncHarness(withOutbox: false);
        h.Put(Inbox, 1, 101);
        await h.RunAsync();
        Assert.Equal(SyncState.Idle, h.Sync.GetState(h.AccountId));
    }

    [Fact]
    public async Task SyncCompleted_IsRaisedOnSuccess()
    {
        using var h = new SyncHarness();
        var raised = 0;
        h.Sync.SyncCompleted += (_, _) => raised++;
        await h.RunAsync();
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task NewMail_BodyFetchFails_IsNotSkipped_ButProcessedOnNextSync()
    {
        var inner = new FakeImapGateway();
        var flaky = new FlakyBodyGateway(inner);
        using var h = new SyncHarness(new DelegateGatewayFactory(_ => flaky));
        inner.Folders.AddRange(h.Gateway.Folders);
        PutInner(inner, h, 1, 101);
        await h.RunAsync();

        PutInner(inner, h, 2, 102);
        flaky.FailBodies = true;
        await h.RunAsync();
        Assert.Equal(SyncState.Offline, h.Sync.GetState(h.AccountId));
        Assert.Null(h.Cache.Mail.Get(h.AccountId, 102));      // 中途半端に保存しない
        Assert.Empty(h.Rule.Applied);

        flaky.FailBodies = false;
        await h.RunAsync();
        Assert.Single(h.Rule.Applied);                         // 次回に規則と通知が漏れなく動く
        Assert.Equal("本文102", h.Cache.Mail.Get(h.AccountId, 102)!.BodyText);
        Assert.Single(h.NewMails);
    }

    [Fact]
    public async Task NewMailHandlerThrowing_DoesNotFailSync()
    {
        using var h = new SyncHarness();
        h.Put(Inbox, 1, 101);
        await h.RunAsync();
        h.Sync.NewMailProcessed += (_, _) => throw new InvalidOperationException();
        h.Put(Inbox, 2, 102);

        await h.RunAsync();

        Assert.Equal(SyncState.Idle, h.Sync.GetState(h.AccountId));
        Assert.Contains(h.Log.Entries, e => e.StartsWith("W:Sync"));
    }

    private static void PutInner(FakeImapGateway gw, SyncHarness h, uint uid, long id)
    {
        var hd = new ImapHeader(uid, id, $"<{id}@example.com>", null, "件名", "a@example.com", "山田", new[] { "me@example.com" },
            Array.Empty<string>(), h.Now.AddDays(-1), false, false);
        if (!gw.Headers.TryGetValue(Inbox, out var list)) gw.Headers[Inbox] = list = new List<ImapHeader>();
        list.Add(hd);
        gw.Bodies[(Inbox, uid)] = new ImapBody(uid, "本文" + id, null, Array.Empty<AttachmentInfo>());
    }
}
