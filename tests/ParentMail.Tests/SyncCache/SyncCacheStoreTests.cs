using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Tests.Data;

namespace ParentMail.Tests.SyncCache;

public class SyncCacheStoreTests
{
    private const string Acc = CacheFixture.AccountId;

    [Fact]
    public void InitialSyncFlag_DefaultsFalse_AndPersists()
    {
        using var f = new CacheFixture();
        var s = new SyncCacheStore(f.Db);
        Assert.False(s.IsInitialSyncDone(Acc));
        s.SetInitialSyncDone(Acc);
        s.SetInitialSyncDone(Acc);                                  // 二重でも壊れない
        Assert.True(s.IsInitialSyncDone(Acc));
        Assert.False(s.IsInitialSyncDone(CacheFixture.OtherAccountId));   // アカウントごとに別
    }

    [Fact]
    public void PendingSeen_LatestOperationPerMessageWins_AndCanBeRemoved()
    {
        using var f = new CacheFixture();
        var s = new SyncCacheStore(f.Db);
        s.EnqueueSeen(Acc, 5, true);
        s.EnqueueSeen(Acc, 5, false);
        s.EnqueueSeen(Acc, 6, true);

        var list = s.GetPending(Acc);
        Assert.Equal(2, list.Count);
        Assert.Equal(SyncCacheStore.OpUnseen, list.Single(p => p.GmailMsgId == 5).Op);

        s.RemovePending(Acc, list[0].Id);
        Assert.Single(s.GetPending(Acc));
    }

    [Fact]
    public void BodylessInFolder_RespectsWindow_AndAllMailOnlyRule()
    {
        using var f = new CacheFixture();
        var s = new SyncCacheStore(f.Db);
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        f.Mail.Upsert(Acc, CacheFixture.Msg(1, now.AddDays(-1)), new[] { CacheFixture.In(1, FolderKind.Inbox, 10), CacheFixture.In(1, FolderKind.AllMail, 11) });
        f.Mail.Upsert(Acc, CacheFixture.Msg(2, now.AddDays(-2)), new[] { CacheFixture.In(2, FolderKind.AllMail, 12) });
        f.Mail.Upsert(Acc, CacheFixture.Msg(3, now.AddMonths(-14)), new[] { CacheFixture.In(3, FolderKind.Inbox, 13) });
        f.Mail.SetBody(Acc, 1, "x", null, null);

        var since = now.AddMonths(-12);
        Assert.Empty(s.GetBodylessInFolder(Acc, FolderKind.Inbox, since));    // 1 は取得済み、3 は古い
        var all = s.GetBodylessInFolder(Acc, FolderKind.AllMail, since);
        Assert.Equal(new[] { (2L, 12u) }, all);                               // AllMail だけに属する 2 のみ
    }

    [Fact]
    public void ReadStatesAndIds_ListEveryMessage()
    {
        using var f = new CacheFixture();
        f.Add(1, FolderKind.Inbox);
        f.Add(2, FolderKind.Sent);
        f.Mail.SetRead(Acc, 2, true);
        var s = new SyncCacheStore(f.Db);
        Assert.Equal(new long[] { 1, 2 }, s.GetMessageIds(Acc).OrderBy(x => x));
        Assert.False(s.GetReadStates(Acc)[1]);
        Assert.True(s.GetReadStates(Acc)[2]);
    }
}
