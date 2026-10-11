using NSubstitute;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.FolderActions;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Tests.Data;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.FolderActions;

// 削除・元に戻す・迷惑メール・復元・既読切り替えの業務処理テスト。
// 実物の cache.db（日本語を含む一時フォルダ）と NSubstitute の IMAP ゲートウェイで動かす。
public sealed class FolderActionServiceTests : IDisposable
{
    private const string Acc = CacheFixture.AccountId;
    private const string InboxPath = "INBOX";
    private const string TrashPath = "[Gmail]/ごみ箱";
    private const string SpamPath = "[Gmail]/迷惑メール";
    private const string DraftsPath = "[Gmail]/下書き";
    private const string AllMailPath = "[Gmail]/すべてのメール";

    private readonly CacheFixture _fx = new();
    private readonly IImapGateway _gw = Substitute.For<IImapGateway>();
    private readonly IReadStateSync _read = Substitute.For<IReadStateSync>();
    private readonly FakeSyncService _sync = new();
    private readonly FakeAppLog _log = new();
    private uint _nextTrashUid = 500;

    public FolderActionServiceTests()
    {
        _fx.Mail.SaveImapName(Acc, FolderKind.Inbox, InboxPath);
        _fx.Mail.SaveImapName(Acc, FolderKind.Trash, TrashPath);
        _fx.Mail.SaveImapName(Acc, FolderKind.Spam, SpamPath);
        _fx.Mail.SaveImapName(Acc, FolderKind.Drafts, DraftsPath);
        _fx.Mail.SaveImapName(Acc, FolderKind.AllMail, AllMailPath);
        _gw.TestLoginAsync(Arg.Any<CancellationToken>()).Returns(LoginResult.Ok);
        // 既定: 移動は成功し、移動先で新しい UID が付く
        _gw.MoveAsync(Arg.Any<string>(), Arg.Any<uint>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => (uint?)_nextTrashUid++);
    }

    public void Dispose() => _fx.Dispose();

    private FolderActionService Service(IDraftService? draftService = null)
    {
        var accounts = new InMemoryAccountRepository();
        accounts.Add(new Account(Acc, "oya@example.jp", "お父さん", 0));
        var factory = Substitute.For<IImapGatewayFactory>();
        factory.Create(Arg.Any<Account>()).Returns(_gw);
        var session = new FetchSession(accounts, factory, _log);
        return new FolderActionService(session, _fx.Mail, _fx.Drafts, draftService, _read, _sync, _fx.Clock, _log);
    }

    private DeleteTarget Target(long id, FolderKind folder = FolderKind.Inbox)
        => new(id, folder, _fx.Mail.GetMemberships(Acc, id).First(m => m.Folder == folder).Uid);

    private bool In(long id, FolderKind folder) => _fx.Mail.GetMemberships(Acc, id).Any(m => m.Folder == folder);

    // ---- 1 通の削除 ----

    [Fact]
    public async Task Delete_MovesToTrash_UpdatesMembership_AndReturnsToken()
    {
        _fx.Add(1, FolderKind.Inbox, FolderKind.AllMail);
        var result = await Service().DeleteAsync(Acc, new[] { Target(1) });

        Assert.Equal(MoveStatus.Ok, result.Move.Status);
        await _gw.Received(1).MoveAsync(InboxPath, 101u, TrashPath, Arg.Any<CancellationToken>());
        Assert.False(In(1, FolderKind.Inbox));
        Assert.True(In(1, FolderKind.Trash));
        Assert.True(In(1, FolderKind.AllMail)); // すべてのメールの所属はそのまま
        Assert.NotNull(result.Token);
        Assert.Equal(new UndoEntry(1, FolderKind.Inbox, 101u), Assert.Single(result.Token!.Entries));
        Assert.Equal(_fx.Clock.UtcNow.AddSeconds(Constants.UndoNoticeSeconds), result.Token.ExpiresUtc.UtcDateTime);
    }

    [Fact]
    public async Task Delete_NeverUsesImapDelete()
    {
        _fx.Add(1, FolderKind.Inbox);
        await Service().DeleteAsync(Acc, new[] { Target(1) });
        await _gw.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<uint>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_Offline_RefusesWithoutTouchingAnything()
    {
        _fx.Add(1, FolderKind.Inbox);
        _sync.States[Acc] = SyncState.Offline;
        var result = await Service().DeleteAsync(Acc, new[] { Target(1) });

        Assert.Equal(MoveStatus.Offline, result.Move.Status);
        Assert.Null(result.Token);
        Assert.True(In(1, FolderKind.Inbox));
        await _gw.DidNotReceive().MoveAsync(Arg.Any<string>(), Arg.Any<uint>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_FailedMove_KeepsLocalStateAsIs()
    {
        _fx.Add(1, FolderKind.Inbox);
        _gw.MoveAsync(Arg.Any<string>(), Arg.Any<uint>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<uint?>(_ => throw new InvalidOperationException("拒否された"));
        var result = await Service().DeleteAsync(Acc, new[] { Target(1) });

        Assert.Equal(MoveStatus.Failed, result.Move.Status);
        Assert.Equal(1, result.Move.Failed);
        Assert.Null(result.Token);
        Assert.True(In(1, FolderKind.Inbox));
        Assert.False(In(1, FolderKind.Trash));
    }

    [Fact]
    public async Task Delete_ConnectionDropsMidway_ReportsOfflineWhenNothingMoved()
    {
        _fx.Add(1, FolderKind.Inbox);
        _gw.MoveAsync(Arg.Any<string>(), Arg.Any<uint>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<uint?>(_ => throw new IOException("切断"));
        var result = await Service().DeleteAsync(Acc, new[] { Target(1) });

        Assert.Equal(MoveStatus.Offline, result.Move.Status);
        Assert.True(In(1, FolderKind.Inbox));
    }

    // ---- 複数選択の削除 ----

    [Fact]
    public async Task MultiDelete_GroupsBySourceFolder_AndMovesEach()
    {
        _fx.Add(1, FolderKind.Inbox);
        _fx.Add(2, FolderKind.Sent);
        _fx.Mail.SaveImapName(Acc, FolderKind.Sent, "[Gmail]/送信済みメール");
        _fx.Add(3, FolderKind.Inbox);
        var result = await Service().DeleteAsync(Acc, new[] { Target(1), Target(2, FolderKind.Sent), Target(3) });

        Assert.Equal(MoveStatus.Ok, result.Move.Status);
        Assert.Equal(3, result.Move.Moved.Count);
        await _gw.Received(2).MoveAsync(InboxPath, Arg.Any<uint>(), TrashPath, Arg.Any<CancellationToken>());
        await _gw.Received(1).MoveAsync("[Gmail]/送信済みメール", Arg.Any<uint>(), TrashPath, Arg.Any<CancellationToken>());
        // 接続は 1 回だけ使う
        await _gw.Received(1).TestLoginAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MultiDelete_OverLimit_Throws_AndMaxIsAccepted()
    {
        var over = Enumerable.Range(1, Constants.MaxDeleteBatch + 1).Select(i => new DeleteTarget(i, FolderKind.Inbox, (uint)i)).ToList();
        await Assert.ThrowsAsync<ArgumentException>(() => Service().DeleteAsync(Acc, over));
    }

    [Fact]
    public async Task MultiDelete_SkipsMailsAlreadyInTrash_AndCountsThem()
    {
        _fx.Add(1, FolderKind.Inbox);
        _fx.Add(2, FolderKind.Trash);
        var result = await Service().DeleteAsync(Acc, new[] { Target(1), Target(2, FolderKind.Trash) });

        Assert.Equal(MoveStatus.Ok, result.Move.Status);
        Assert.Equal(1, result.Move.Skipped);
        Assert.Single(result.Move.Moved);
        await _gw.Received(1).MoveAsync(Arg.Any<string>(), Arg.Any<uint>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MultiDelete_AllMailOnly_MovesFromAllMail_AndUndoReturnsToAllMail()
    {
        _fx.Add(1, FolderKind.AllMail);
        var svc = Service();
        var result = await svc.DeleteAsync(Acc, new[] { Target(1, FolderKind.AllMail) });
        await _gw.Received(1).MoveAsync(AllMailPath, 101u, TrashPath, Arg.Any<CancellationToken>());
        Assert.Equal(FolderKind.AllMail, result.Token!.Entries[0].FromFolder);

        var undo = await svc.UndoDeleteAsync(result.Token);
        Assert.Equal(MoveStatus.Ok, undo.Status);
        await _gw.Received(1).MoveAsync(TrashPath, 500u, AllMailPath, Arg.Any<CancellationToken>());
        Assert.True(In(1, FolderKind.AllMail));
        Assert.False(In(1, FolderKind.Trash));
    }

    [Fact]
    public async Task MultiDelete_PartialFailure_MovedStayMoved_AndCountsAreReported()
    {
        _fx.Add(1, FolderKind.Inbox);
        _fx.Add(2, FolderKind.Inbox);
        _fx.Add(3, FolderKind.Inbox);
        // 2 通目だけ失敗
        _gw.MoveAsync(Arg.Any<string>(), 102u, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<uint?>(_ => throw new InvalidOperationException("失敗"));
        var result = await Service().DeleteAsync(Acc, new[] { Target(1), Target(2), Target(3) });

        Assert.Equal(MoveStatus.Partial, result.Move.Status);
        Assert.Equal(2, result.Move.Moved.Count);
        Assert.Equal(1, result.Move.Failed);
        Assert.True(In(1, FolderKind.Trash));
        Assert.True(In(2, FolderKind.Inbox));
        Assert.True(In(3, FolderKind.Trash));
        // 元に戻す記録は移せた 2 通だけ
        Assert.Equal(new long[] { 1, 3 }, result.Token!.Entries.Select(e => e.GmMsgId).ToArray());
    }

    [Fact]
    public async Task MultiDelete_ConnectionDropsMidway_RestAreFailed_MovedStayMoved()
    {
        _fx.Add(1, FolderKind.Inbox);
        _fx.Add(2, FolderKind.Inbox);
        _fx.Add(3, FolderKind.Inbox);
        _gw.MoveAsync(Arg.Any<string>(), 102u, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<uint?>(_ => throw new IOException("切断"));
        var result = await Service().DeleteAsync(Acc, new[] { Target(1), Target(2), Target(3) });

        Assert.Equal(MoveStatus.Partial, result.Move.Status);
        Assert.Single(result.Move.Moved);
        Assert.Equal(2, result.Move.Failed);
        Assert.True(In(3, FolderKind.Inbox));
    }

    // ---- 元に戻す ----

    [Fact]
    public async Task Undo_MovesBackToViewedFolderOnly()
    {
        _fx.Add(1, FolderKind.Inbox, FolderKind.AllMail);
        var svc = Service();
        var del = await svc.DeleteAsync(Acc, new[] { Target(1) });
        var undo = await svc.UndoDeleteAsync(del.Token!);

        Assert.Equal(MoveStatus.Ok, undo.Status);
        await _gw.Received(1).MoveAsync(TrashPath, 500u, InboxPath, Arg.Any<CancellationToken>());
        Assert.True(In(1, FolderKind.Inbox));
        Assert.False(In(1, FolderKind.Trash));
    }

    [Fact]
    public async Task Undo_AfterExpiry_DoesNothing()
    {
        _fx.Add(1, FolderKind.Inbox);
        var svc = Service();
        var del = await svc.DeleteAsync(Acc, new[] { Target(1) });
        _fx.Clock.Advance(TimeSpan.FromSeconds(Constants.UndoNoticeSeconds + 1));
        var undo = await svc.UndoDeleteAsync(del.Token!);

        Assert.Equal(MoveStatus.Expired, undo.Status);
        Assert.True(In(1, FolderKind.Trash));
        await _gw.Received(1).MoveAsync(Arg.Any<string>(), Arg.Any<uint>(), Arg.Any<string>(), Arg.Any<CancellationToken>()); // 削除の 1 回だけ
    }

    [Fact]
    public async Task Undo_Offline_IsRefused()
    {
        _fx.Add(1, FolderKind.Inbox);
        var svc = Service();
        var del = await svc.DeleteAsync(Acc, new[] { Target(1) });
        _sync.States[Acc] = SyncState.Offline;
        var undo = await svc.UndoDeleteAsync(del.Token!);

        Assert.Equal(MoveStatus.Offline, undo.Status);
        Assert.True(In(1, FolderKind.Trash));
    }

    [Fact]
    public async Task Undo_WithoutTrashUid_CountsAsFailed()
    {
        _fx.Add(1, FolderKind.Inbox);
        // 移動後の UID が取れなかった場合（ごみ箱の所属が作られない）
        _gw.MoveAsync(Arg.Any<string>(), Arg.Any<uint>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((uint?)null);
        var svc = Service();
        var del = await svc.DeleteAsync(Acc, new[] { Target(1) });
        var undo = await svc.UndoDeleteAsync(del.Token!);

        Assert.Equal(MoveStatus.Failed, undo.Status);
        Assert.Equal(1, undo.Failed);
    }

    // ---- 迷惑メール・復元 ----

    [Fact]
    public async Task MoveToSpam_MovesInboxToSpam()
    {
        _fx.Add(1, FolderKind.Inbox);
        var r = await Service().MoveToSpamAsync(Acc, Target(1));
        Assert.Equal(MoveStatus.Ok, r.Status);
        await _gw.Received(1).MoveAsync(InboxPath, 101u, SpamPath, Arg.Any<CancellationToken>());
        Assert.True(In(1, FolderKind.Spam));
        Assert.False(In(1, FolderKind.Inbox));
    }

    [Fact]
    public async Task NotSpam_MovesSpamToInbox()
    {
        _fx.Add(1, FolderKind.Spam);
        var r = await Service().NotSpamAsync(Acc, Target(1, FolderKind.Spam));
        Assert.Equal(MoveStatus.Ok, r.Status);
        await _gw.Received(1).MoveAsync(SpamPath, 101u, InboxPath, Arg.Any<CancellationToken>());
        Assert.True(In(1, FolderKind.Inbox));
    }

    [Fact]
    public async Task Restore_AlwaysMovesTrashToInbox()
    {
        _fx.Add(1, FolderKind.Trash);
        var r = await Service().RestoreAsync(Acc, Target(1, FolderKind.Trash));
        Assert.Equal(MoveStatus.Ok, r.Status);
        await _gw.Received(1).MoveAsync(TrashPath, 101u, InboxPath, Arg.Any<CancellationToken>());
        Assert.True(In(1, FolderKind.Inbox));
        Assert.False(In(1, FolderKind.Trash));
    }

    [Theory]
    [InlineData("spam")]
    [InlineData("notspam")]
    [InlineData("restore")]
    public async Task MoveActions_Offline_AreRefused(string kind)
    {
        var folder = kind == "spam" ? FolderKind.Inbox : kind == "notspam" ? FolderKind.Spam : FolderKind.Trash;
        _fx.Add(1, folder);
        _sync.States[Acc] = SyncState.Offline;
        var svc = Service();
        var t = Target(1, folder);
        var r = kind == "spam" ? await svc.MoveToSpamAsync(Acc, t)
            : kind == "notspam" ? await svc.NotSpamAsync(Acc, t)
            : await svc.RestoreAsync(Acc, t);
        Assert.Equal(MoveStatus.Offline, r.Status);
        Assert.True(In(1, folder));
    }

    // ---- 下書き ----

    [Fact]
    public async Task DeleteDraftMail_MovesToTrash_AndRemovesLinkedLocalDraft()
    {
        _fx.Add(1, FolderKind.Drafts); // UID 101
        var local = _fx.Drafts.Save(new DraftItem { AccountId = Acc, Subject = "下書き", GmailUid = 101, Dirty = false });
        var other = _fx.Drafts.Save(new DraftItem { AccountId = Acc, Subject = "別の下書き", GmailUid = 999, Dirty = false });
        var r = await Service().DeleteAsync(Acc, new[] { Target(1, FolderKind.Drafts) });

        Assert.Equal(MoveStatus.Ok, r.Move.Status);
        await _gw.Received(1).MoveAsync(DraftsPath, 101u, TrashPath, Arg.Any<CancellationToken>());
        var left = _fx.Drafts.GetAll(Acc).Select(d => d.LocalId).ToList();
        Assert.DoesNotContain(local.LocalId, left);
        Assert.Contains(other.LocalId, left);
    }

    [Fact]
    public async Task DeleteLocalDraft_UsesDraftServiceWhenPresent_ElseRepository()
    {
        var draftService = Substitute.For<IDraftService>();
        await Service(draftService).DeleteLocalDraftAsync(7);
        await draftService.Received(1).DeleteAsync(7, Arg.Any<CancellationToken>());

        var saved = _fx.Drafts.Save(new DraftItem { AccountId = Acc, Subject = "ローカルだけ" });
        await Service().DeleteLocalDraftAsync(saved.LocalId);
        Assert.Empty(_fx.Drafts.GetAll(Acc));
        await _gw.DidNotReceive().MoveAsync(Arg.Any<string>(), Arg.Any<uint>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- 既読・未読 ----

    [Fact]
    public async Task ToggleRead_FlipsBothWays_AndWorksOffline()
    {
        var m = _fx.Add(1, FolderKind.Inbox); // 未読
        _sync.States[Acc] = SyncState.Offline;
        var svc = Service();

        Assert.True(await svc.ToggleReadAsync(Acc, m.GmailMsgId));
        await _read.Received(1).SetSeenAsync(Acc, 1, true, Arg.Any<CancellationToken>());

        _read.When(x => x.SetSeenAsync(Acc, 1, true, Arg.Any<CancellationToken>())).Do(_ => _fx.Mail.SetRead(Acc, 1, true));
        _read.SetSeenAsync(Acc, 1, true, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _fx.Mail.SetRead(Acc, 1, true);
        Assert.False(await svc.ToggleReadAsync(Acc, 1));
        await _read.Received(1).SetSeenAsync(Acc, 1, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleRead_UnknownMail_ReturnsNull()
    {
        Assert.Null(await Service().ToggleReadAsync(Acc, 12345));
    }
}
