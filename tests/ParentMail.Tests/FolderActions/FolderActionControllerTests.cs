using NSubstitute;
using ParentMail.App.Features.FolderActions;
using ParentMail.App.Features.MailList;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Features.FolderActions;
using ParentMail.Core.Features.MailDisplay;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.FolderActions;

// 一覧の操作（削除・元に戻す・迷惑メール・既読・アドレス帳・受信拒否・下書き）の制御のテスト。
// 業務サービスは差し替え、一覧は実物（ListViewModel）と偽のリポジトリで動かす。日本語の件名・差出人を使う。
public sealed class FolderActionControllerTests
{
    private const string Acc = "acc1";

    private sealed class RecordingNotifier : INotifier
    {
        public List<(string Text, string? Label, Action? Action, int Seconds)> Notices { get; } = new();
        public void ShowBanner(BannerKind kind, string text, (string Label, Action Action)? action = null) { }
        public void HideBanner(BannerKind kind) { }
        public void ShowNotice(string text, string? actionLabel = null, Action? action = null, int seconds = Constants.TransientNoticeSeconds)
            => Notices.Add((text, actionLabel, action, seconds));
    }

    private readonly InMemoryMailRepository _mail = new();
    private readonly FakeAppState _state = new() { CurrentAccountId = Acc };
    private readonly FakeClock _clock = new() { UtcNow = new DateTime(2026, 10, 10, 6, 0, 0, DateTimeKind.Utc) };
    private readonly RecordingNotifier _notifier = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLog _log = new();
    private readonly IFolderActionService _service = Substitute.For<IFolderActionService>();
    private readonly IAddressBookService _book = Substitute.For<IAddressBookService>();
    private readonly IBlockListService _block = Substitute.For<IBlockListService>();
    private readonly IDraftService _drafts = Substitute.For<IDraftService>();
    private readonly IComposeWindowFactory _compose = Substitute.For<IComposeWindowFactory>();
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();
    private readonly IReplyForwardHandler _reply = Substitute.For<IReplyForwardHandler>();
    private readonly PreviewSelectionHints _hints = new();
    private readonly InMemoryDraftRepository _draftRepo = new();
    private readonly ListViewModel _list;
    private readonly FolderActionController _controller;
    private readonly List<(long? Id, SelectionSource Source, bool HintAuto)> _selections = new();

    public FolderActionControllerTests()
    {
        _list = new ListViewModel(_mail, new InMemoryOutboxRepository(), _draftRepo, _state, _clock, null, null, null, _dialogs, a => a())
        {
            Hints = _hints,
        };
        _state.SelectedMessageChanged += (_, _) => _selections.Add((_state.SelectedMessageId, SelectionSource.Auto, _hints.IsAutoSelection));
        _controller = new FolderActionController(_service, _list, _state, _notifier, _dialogs, _clock, _mail, _log,
            _book, _block, _drafts, _compose, _outbox, _reply);

        _service.IsOffline(Arg.Any<string>()).Returns(false);
        _service.FindUid(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<FolderKind>()).Returns(ci => (uint?)(uint)ci.ArgAt<long>(1));
        // 既定: 削除はすべて成功（cache.db の代わりに偽リポジトリの所属も更新する）
        _service.DeleteAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<DeleteTarget>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Succeeded(ci.ArgAt<IReadOnlyList<DeleteTarget>>(1))));
    }

    private DeleteResult Succeeded(IReadOnlyList<DeleteTarget> targets, params long[] failedIds)
    {
        var moved = new List<UndoEntry>();
        foreach (var t in targets.Where(t => t.FromFolder != FolderKind.Trash))
        {
            if (failedIds.Contains(t.GmMsgId)) continue;
            _mail.RemoveMembership(Acc, t.GmMsgId, t.FromFolder);
            _mail.AddMembership(Acc, new MessageFolder(t.GmMsgId, FolderKind.Trash, 900, 0));
            moved.Add(new UndoEntry(t.GmMsgId, t.FromFolder, t.Uid));
        }
        var status = failedIds.Length == 0 ? MoveStatus.Ok : moved.Count > 0 ? MoveStatus.Partial : MoveStatus.Failed;
        var token = moved.Count > 0 ? new UndoToken(Acc, moved, new DateTimeOffset(_clock.UtcNow, TimeSpan.Zero).AddSeconds(10)) : null;
        return new DeleteResult(new MoveResult(status, moved, failedIds.Length), token);
    }

    private DateTime T(int minutesAgo) => _clock.UtcNow.AddMinutes(-minutesAgo);

    // id が小さいほど新しい（先頭が id 1）
    private void AddMails(int count, FolderKind folder = FolderKind.Inbox, bool read = true)
    {
        for (var i = 1; i <= count; i++)
        {
            _mail.Upsert(Acc, new MailMessage
            {
                GmailMsgId = i, Subject = "件名" + i, FromName = "山田 太郎", FromAddress = "taro@example.jp", DateUtc = T(i), IsRead = read,
            }, new[] { new MessageFolder(i, folder, (uint)i, 1) });
        }
        _list.LoadFolder(folder);
    }

    private MailListItem Row(long id) => _list.Items.First(i => i.GmMsgId == id);
    private long?[] Ids() => _list.Items.Select(i => i.GmMsgId).ToArray();

    // ---- 単一削除 ----

    [Fact]
    public async Task Delete_Selected_MovesToTrash_RemovesRow_AndSelectsNextOlder()
    {
        AddMails(3);
        _list.SelectedItem = Row(2);
        _selections.Clear();
        await _controller.DeleteAsync();

        await _service.Received(1).DeleteAsync(Acc, Arg.Is<IReadOnlyList<DeleteTarget>>(t => t.Count == 1 && t[0] == new DeleteTarget(2, FolderKind.Inbox, 2)),
            Arg.Any<CancellationToken>());
        Assert.Equal(new long?[] { 1, 3 }, Ids());
        Assert.Equal(3, _list.SelectedItem?.GmMsgId); // 1 つ古いメール
    }

    [Fact]
    public async Task Delete_AutoSelectedMail_IsNotMarkedRead_BecauseSelectionIsFlaggedAuto()
    {
        AddMails(3, read: false);
        _list.SelectedItem = Row(1);
        _selections.Clear();
        await _controller.DeleteAsync();

        // 選び直しは「自動」としてプレビューに伝わる（既読にしない、A58）
        var sel = Assert.Single(_selections);
        Assert.Equal(2, sel.Id);
        Assert.True(sel.HintAuto);
        // 利用者がクリックした選択は「自動」ではない
        _selections.Clear();
        _list.SelectedItem = Row(3);
        Assert.False(Assert.Single(_selections).HintAuto);
    }

    [Fact]
    public async Task Delete_LastRow_SelectsPrevious_AndEmptyListSelectsNothing()
    {
        AddMails(2);
        _list.SelectedItem = Row(2); // いちばん古い
        await _controller.DeleteAsync();
        Assert.Equal(1, _list.SelectedItem?.GmMsgId);

        await _controller.DeleteAsync();
        Assert.Null(_list.SelectedItem);
        Assert.Empty(_list.Items);
    }

    [Fact]
    public async Task Delete_ShowsUndoNotice_ForTenSeconds_WithJapaneseTextAndLabel()
    {
        AddMails(2);
        _list.SelectedItem = Row(1);
        await _controller.DeleteAsync();

        var n = Assert.Single(_notifier.Notices);
        Assert.Equal("ごみ箱に移動しました", n.Text);
        Assert.Equal("元に戻す", n.Label);
        Assert.Equal(Constants.UndoNoticeSeconds, n.Seconds);
        Assert.NotNull(_controller.LatestUndo);
    }

    [Fact]
    public async Task Delete_NothingSelectedOrChecked_DoesNothing()
    {
        AddMails(2);
        await _controller.DeleteAsync();
        await _service.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
        Assert.Empty(_dialogs.Notices);
    }

    [Theory]
    [InlineData(FolderKind.Trash)]
    [InlineData(FolderKind.Outbox)]
    public async Task Delete_NotAvailableInTrashOrOutbox(FolderKind folder)
    {
        AddMails(1, folder);
        _list.SelectedItem = _list.Items.FirstOrDefault();
        await _controller.DeleteAsync();
        await _service.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Delete_Offline_IsRefusedWithModalNotice_AndNothingChanges()
    {
        AddMails(2);
        _list.SelectedItem = Row(1);
        _service.IsOffline(Acc).Returns(true);
        await _controller.DeleteAsync();

        Assert.Equal(new long?[] { 1, 2 }, Ids());
        Assert.Equal((Strings.WD_10, Strings.BN_05), Assert.Single(_dialogs.Notices));
        await _service.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
        Assert.Empty(_notifier.Notices);
    }

    [Fact]
    public async Task Delete_FailedMove_RevertsList_AndShowsErrorNotice()
    {
        AddMails(2);
        _list.SelectedItem = Row(1);
        _service.DeleteAsync(Acc, Arg.Any<IReadOnlyList<DeleteTarget>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteResult(new MoveResult(MoveStatus.Failed, Array.Empty<UndoEntry>(), 1), null)));
        await _controller.DeleteAsync();

        Assert.Equal(new long?[] { 1, 2 }, Ids()); // 元に戻って見える
        Assert.Equal((Strings.WD_10, Strings.BN_06), Assert.Single(_dialogs.Notices));
        Assert.Empty(_notifier.Notices);
    }

    [Fact]
    public async Task Delete_ServiceThrows_RevertsList_AndShowsErrorNotice()
    {
        AddMails(2);
        _list.SelectedItem = Row(1);
        _service.DeleteAsync(Acc, Arg.Any<IReadOnlyList<DeleteTarget>>(), Arg.Any<CancellationToken>())
            .Returns<DeleteResult>(_ => throw new InvalidOperationException("壊れた"));
        await _controller.DeleteAsync();

        Assert.Equal(new long?[] { 1, 2 }, Ids());
        Assert.Equal(Strings.BN_06, Assert.Single(_dialogs.Notices).Message);
    }

    [Fact]
    public async Task Delete_ConnectionLostDuringMove_ShowsOfflineNotice_AndRestoresRows()
    {
        AddMails(2);
        _list.SelectedItem = Row(1);
        _service.DeleteAsync(Acc, Arg.Any<IReadOnlyList<DeleteTarget>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new DeleteResult(new MoveResult(MoveStatus.Offline, Array.Empty<UndoEntry>(), 1), null)));
        await _controller.DeleteAsync();

        Assert.Equal(new long?[] { 1, 2 }, Ids());
        Assert.Equal(Strings.BN_05, Assert.Single(_dialogs.Notices).Message);
    }

    // ---- 複数選択の削除 ----

    [Fact]
    public async Task MultiDelete_DeletesExactlyTheCheckedRows_IgnoringSelectionUnlessChecked()
    {
        AddMails(5);
        _list.SelectedItem = Row(1); // 選択行はチェックされていない
        Row(3).IsChecked = true;
        Row(4).IsChecked = true;
        await _controller.DeleteAsync();

        await _service.Received(1).DeleteAsync(Acc,
            Arg.Is<IReadOnlyList<DeleteTarget>>(t => t.Select(x => x.GmMsgId).OrderBy(x => x).SequenceEqual(new long[] { 3, 4 })),
            Arg.Any<CancellationToken>());
        Assert.Equal(new long?[] { 1, 2, 5 }, Ids());
        Assert.Equal(1, _list.SelectedItem?.GmMsgId); // 選択行はそのまま
        Assert.Equal(0, _list.CheckedCount);
        Assert.Equal("2通をごみ箱に移動しました", Assert.Single(_notifier.Notices).Text);
    }

    [Fact]
    public async Task MultiDelete_SelectedRowChecked_AutoSelectsNextOlderAfterTheOldestDeleted()
    {
        AddMails(5);
        _list.SelectedItem = Row(2);
        Row(2).IsChecked = true;
        Row(3).IsChecked = true;
        await _controller.DeleteAsync();

        Assert.Equal(new long?[] { 1, 4, 5 }, Ids());
        Assert.Equal(4, _list.SelectedItem?.GmMsgId);
    }

    [Fact]
    public async Task MultiDelete_MoreThan200_ShowsLimitNotice_AndDoesNothing()
    {
        AddMails(201);
        _list.LoadMore(); // 一覧は 200 通ずつ読むので、続きを読んで 201 通にする
        foreach (var item in _list.Items) item.IsChecked = true;
        Assert.Equal(201, _list.CheckedCount);
        await _controller.DeleteAsync();

        Assert.Equal((Strings.WD_10, Strings.BN_22), Assert.Single(_dialogs.Notices));
        await _service.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
        Assert.Equal(201, _list.Items.Count);
    }

    [Fact]
    public async Task MultiDelete_Exactly200_IsAllowed()
    {
        AddMails(200);
        _list.SelectAllVisible();
        await _controller.DeleteAsync();

        await _service.Received(1).DeleteAsync(Acc, Arg.Is<IReadOnlyList<DeleteTarget>>(t => t.Count == 200), Arg.Any<CancellationToken>());
        Assert.Empty(_list.Items);
        Assert.Empty(_dialogs.Notices);
    }

    [Fact]
    public async Task MultiDelete_PartialFailure_MovedStayMoved_FailedComeBack_ModalHasBothCounts()
    {
        AddMails(4);
        _list.SelectedItem = Row(1);
        foreach (var id in new long[] { 2, 3, 4 }) Row(id).IsChecked = true;
        _service.DeleteAsync(Acc, Arg.Any<IReadOnlyList<DeleteTarget>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Succeeded(ci.ArgAt<IReadOnlyList<DeleteTarget>>(1), failedIds: 3)));
        await _controller.DeleteAsync();

        Assert.Equal(new long?[] { 1, 3 }, Ids()); // 3 だけ戻る
        Assert.Equal((Strings.WD_10, "3通のうち1通を移せませんでした。もう一度お試しください。"), Assert.Single(_dialogs.Notices));
        // 元に戻す通知は移せた 2 通だけ
        var n = Assert.Single(_notifier.Notices);
        Assert.Equal("2通をごみ箱に移動しました", n.Text);
        Assert.Equal(new long[] { 2, 4 }, _controller.LatestUndo!.Entries.Select(e => e.GmMsgId).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task RightClickDelete_OnCheckedRow_DeletesAllChecked_OnUncheckedRowOnlyThatRow()
    {
        AddMails(5);
        Row(2).IsChecked = true;
        Row(3).IsChecked = true;

        Assert.Equal(new long?[] { 2, 3 }, _controller.ResolveDeleteRows(Row(2)).Select(r => r.GmMsgId).OrderBy(x => x).ToArray());
        Assert.Equal(new long?[] { 5 }, _controller.ResolveDeleteRows(Row(5)).Select(r => r.GmMsgId).ToArray());

        await _controller.InvokeAsync(FolderActionKind.Delete, Row(5));
        Assert.Equal(new long?[] { 1, 2, 3, 4 }, Ids());
        Assert.Equal(2, _list.CheckedCount); // ほかのチェックは残る
    }

    [Fact]
    public async Task DeleteTargets_FromSearchResults_SkipsTrashRows_AndKeepsThemInTheList()
    {
        AddMails(3);
        var targets = new[]
        {
            new DeleteTarget(1, FolderKind.Inbox, 1),
            new DeleteTarget(2, FolderKind.Trash, 2),
            new DeleteTarget(3, FolderKind.AllMail, 3),
        };
        await _controller.DeleteTargetsAsync(targets);

        Assert.Equal(new long?[] { 2 }, Ids());
        var t = _controller.LatestUndo!.Entries;
        Assert.Contains(t, e => e.GmMsgId == 3 && e.FromFolder == FolderKind.AllMail);
        Assert.DoesNotContain(t, e => e.GmMsgId == 2);
    }

    // ---- 元に戻す ----

    [Fact]
    public async Task Undo_ViaNoticeAction_MovesBack_AndReloadsList()
    {
        AddMails(2);
        _list.SelectedItem = Row(1);
        await _controller.DeleteAsync();
        var token = _controller.LatestUndo!;
        _service.UndoDeleteAsync(token, Arg.Any<CancellationToken>()).Returns(ci =>
        {
            foreach (var e in token.Entries)
            {
                _mail.RemoveMembership(Acc, e.GmMsgId, FolderKind.Trash);
                _mail.AddMembership(Acc, new MessageFolder(e.GmMsgId, e.FromFolder, e.Uid, 1));
            }
            return Task.FromResult(new MoveResult(MoveStatus.Ok, token.Entries, 0));
        });

        await _controller.UndoAsync(token);

        await _service.Received(1).UndoDeleteAsync(token, Arg.Any<CancellationToken>());
        Assert.Equal(new long?[] { 1, 2 }, Ids());
        Assert.Null(_controller.LatestUndo);
    }

    [Fact]
    public async Task Undo_AfterExpiry_IsNotOffered()
    {
        AddMails(2);
        _list.SelectedItem = Row(1);
        await _controller.DeleteAsync();
        var token = _controller.LatestUndo!;
        _clock.Advance(TimeSpan.FromSeconds(11));
        await _controller.UndoAsync(token);

        await _service.DidNotReceiveWithAnyArgs().UndoDeleteAsync(default!, default);
    }

    [Fact]
    public async Task Undo_Offline_IsRefusedWithModalNotice()
    {
        AddMails(2);
        _list.SelectedItem = Row(1);
        await _controller.DeleteAsync();
        _service.IsOffline(Acc).Returns(true);
        await _controller.UndoAsync(_controller.LatestUndo!);

        Assert.Equal(Strings.BN_05, Assert.Single(_dialogs.Notices).Message);
        await _service.DidNotReceiveWithAnyArgs().UndoDeleteAsync(default!, default);
    }

    [Fact]
    public async Task Undo_OnlyTheLatestDeleteCanBeUndone()
    {
        AddMails(3);
        _list.SelectedItem = Row(1);
        await _controller.DeleteAsync();
        var first = _controller.LatestUndo!;
        _list.SelectedItem = Row(3);
        await _controller.DeleteAsync();
        Assert.NotSame(first, _controller.LatestUndo);

        await _controller.UndoAsync(first);
        await _service.DidNotReceiveWithAnyArgs().UndoDeleteAsync(default!, default);
    }

    [Fact]
    public async Task Undo_NoticeActionTriggersUndo()
    {
        AddMails(2);
        _list.SelectedItem = Row(1);
        _service.UndoDeleteAsync(Arg.Any<UndoToken>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new MoveResult(MoveStatus.Ok, ci.Arg<UndoToken>().Entries, 0)));
        await _controller.DeleteAsync();

        _notifier.Notices.Single().Action!.Invoke();
        await _service.Received(1).UndoDeleteAsync(Arg.Any<UndoToken>(), Arg.Any<CancellationToken>());
    }

    // ---- 迷惑メール・復元 ----

    [Fact]
    public async Task MoveToSpam_RemovesRow_SelectsNext_NoNotice()
    {
        AddMails(3);
        _list.SelectedItem = Row(1);
        _service.MoveToSpamAsync(Acc, Arg.Any<DeleteTarget>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new MoveResult(MoveStatus.Ok, new[] { new UndoEntry(1, FolderKind.Inbox, 1) }, 0)));
        await _controller.InvokeAsync(FolderActionKind.MoveToSpam, Row(1));

        Assert.Equal(new long?[] { 2, 3 }, Ids());
        Assert.Equal(2, _list.SelectedItem?.GmMsgId);
        Assert.Empty(_notifier.Notices);
        Assert.Empty(_dialogs.Notices);
    }

    [Fact]
    public async Task NotSpam_InSpamFolder_CallsNotSpam()
    {
        AddMails(2, FolderKind.Spam);
        _service.NotSpamAsync(Acc, Arg.Any<DeleteTarget>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new MoveResult(MoveStatus.Ok, new[] { new UndoEntry(1, FolderKind.Spam, 1) }, 0)));
        await _controller.InvokeAsync(FolderActionKind.NotSpam, Row(1));

        await _service.Received(1).NotSpamAsync(Acc, new DeleteTarget(1, FolderKind.Spam, 1), Arg.Any<CancellationToken>());
        Assert.Equal(new long?[] { 2 }, Ids());
    }

    [Fact]
    public async Task Restore_InTrash_CallsRestoreAndRemovesRow()
    {
        AddMails(2, FolderKind.Trash);
        _service.RestoreAsync(Acc, Arg.Any<DeleteTarget>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new MoveResult(MoveStatus.Ok, new[] { new UndoEntry(2, FolderKind.Trash, 2) }, 0)));
        await _controller.InvokeAsync(FolderActionKind.Restore, Row(2));

        await _service.Received(1).RestoreAsync(Acc, new DeleteTarget(2, FolderKind.Trash, 2), Arg.Any<CancellationToken>());
        Assert.Equal(new long?[] { 1 }, Ids());
    }

    [Fact]
    public async Task MoveActions_Offline_AreRefused()
    {
        AddMails(2);
        _service.IsOffline(Acc).Returns(true);
        await _controller.InvokeAsync(FolderActionKind.MoveToSpam, Row(1));

        Assert.Equal(Strings.BN_05, Assert.Single(_dialogs.Notices).Message);
        Assert.Equal(new long?[] { 1, 2 }, Ids());
        await _service.DidNotReceiveWithAnyArgs().MoveToSpamAsync(default!, default!, default);
    }

    [Fact]
    public async Task MoveToSpam_Failure_RevertsRow_AndShowsErrorNotice()
    {
        AddMails(2);
        _service.MoveToSpamAsync(Acc, Arg.Any<DeleteTarget>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new MoveResult(MoveStatus.Failed, Array.Empty<UndoEntry>(), 1)));
        await _controller.InvokeAsync(FolderActionKind.MoveToSpam, Row(1));

        Assert.Equal(new long?[] { 1, 2 }, Ids());
        Assert.Equal(Strings.BN_06, Assert.Single(_dialogs.Notices).Message);
    }

    // ---- 既読・未読 ----

    [Fact]
    public async Task ToggleRead_CallsServiceAndRefreshesRow_EvenWhenOffline()
    {
        AddMails(2, read: false);
        _service.IsOffline(Acc).Returns(true);
        _service.ToggleReadAsync(Acc, 1, Arg.Any<CancellationToken>()).Returns(ci =>
        {
            _mail.SetRead(Acc, 1, true);
            return Task.FromResult<bool?>(true);
        });
        Assert.True(Row(1).IsUnread);
        await _controller.InvokeAsync(FolderActionKind.ToggleRead, Row(1));

        Assert.False(Row(1).IsUnread);
        Assert.Empty(_dialogs.Notices);
    }

    // ---- アドレス帳・受信拒否 ----

    [Fact]
    public async Task AddSender_OpensAddressBookDialogWithJapaneseNameAndAddress()
    {
        AddMails(1);
        await _controller.InvokeAsync(FolderActionKind.AddToAddressBook, Row(1));
        _book.Received(1).AddFromSender(Acc, "山田 太郎", "taro@example.jp");
    }

    [Theory]
    [InlineData(0, BlockKind.Address)]
    [InlineData(1, BlockKind.Domain)]
    public async Task BlockSender_AddsAddressOrDomain_AndShowsTransientNotice(int choice, BlockKind kind)
    {
        AddMails(2);
        _dialogs.ChooseAnswer = choice;
        _block.AddFromSender(Acc, "taro@example.jp", kind).Returns(BlockAddResult.Added);
        await _controller.InvokeAsync(FolderActionKind.AddToBlockList, Row(1));

        _block.Received(1).AddFromSender(Acc, "taro@example.jp", kind);
        Assert.Equal(Strings.FA_11, Assert.Single(_notifier.Notices).Text);
        // 既存のメールは動かさない（A25）
        Assert.Equal(new long?[] { 1, 2 }, Ids());
        await _service.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task BlockSender_Cancel_AddsNothing()
    {
        AddMails(1);
        _dialogs.ChooseAnswer = 2;
        await _controller.InvokeAsync(FolderActionKind.AddToBlockList, Row(1));
        _block.DidNotReceiveWithAnyArgs().AddFromSender(default!, default!, default);
        Assert.Empty(_notifier.Notices);
    }

    [Fact]
    public async Task BlockSender_Duplicate_ShowsModalNotice()
    {
        AddMails(1);
        _dialogs.ChooseAnswer = 0;
        _block.AddFromSender(Acc, "taro@example.jp", BlockKind.Address).Returns(BlockAddResult.Duplicate);
        await _controller.InvokeAsync(FolderActionKind.AddToBlockList, Row(1));
        Assert.Equal(Strings.BL_05, Assert.Single(_dialogs.Notices).Message);
    }

    // ---- 返信・下書き・送信箱 ----

    [Fact]
    public async Task ReplyForward_GoThroughTheHandler()
    {
        AddMails(1);
        await _controller.InvokeAsync(FolderActionKind.Reply, Row(1));
        await _controller.InvokeAsync(FolderActionKind.ReplyAll, Row(1));
        await _controller.InvokeAsync(FolderActionKind.Forward, Row(1));
        await _reply.Received(1).ReplyAsync(Acc, 1);
        await _reply.Received(1).ReplyAllAsync(Acc, 1);
        await _reply.Received(1).ForwardAsync(Acc, 1);
    }

    [Fact]
    public async Task DeleteLocalDraftRow_IsLocalOnly_NoImapAndNoUndoNotice()
    {
        var draft = _draftRepo.Save(new DraftItem { AccountId = Acc, Subject = "ご挨拶の下書き", To = new[] { "孫 <mago@example.jp>" } });
        _list.LoadFolder(FolderKind.Drafts);
        var row = _list.Items.Single(i => i.DraftId == draft.LocalId);
        _service.DeleteLocalDraftAsync(draft.LocalId, Arg.Any<CancellationToken>()).Returns(ci =>
        {
            _draftRepo.Remove(draft.LocalId);
            return Task.CompletedTask;
        });
        await _controller.InvokeAsync(FolderActionKind.Delete, row);

        await _service.Received(1).DeleteLocalDraftAsync(draft.LocalId, Arg.Any<CancellationToken>());
        await _service.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
        Assert.Empty(_list.Items);
        Assert.Empty(_notifier.Notices);
    }

    [Fact]
    public async Task EditDraft_OpensComposeWithTheDraftInit()
    {
        var init = new ComposeInit { AccountId = Acc, Subject = "下書きの件名" };
        _drafts.OpenForEditByMessageAsync(Acc, 1, Arg.Any<CancellationToken>()).Returns(init);
        AddMails(1, FolderKind.Drafts);
        await _controller.InvokeAsync(FolderActionKind.Edit, Row(1));
        _compose.Received(1).Open(init);
    }

    [Fact]
    public async Task EditDraft_WhenComposeWindowIsOpen_ActivatesItAndDoesNotEvenReadTheDraft()
    {
        _compose.TryActivateExisting().Returns(true);
        AddMails(1, FolderKind.Drafts);
        await _controller.InvokeAsync(FolderActionKind.Edit, Row(1));

        await _drafts.DidNotReceiveWithAnyArgs().OpenForEditByMessageAsync(default!, default, default);
        await _drafts.DidNotReceiveWithAnyArgs().OpenForEditAsync(default, default);
        _compose.DidNotReceiveWithAnyArgs().Open(default!);
    }

    [Fact]
    public async Task DoubleClick_InDrafts_OpensThatRowForEditing()
    {
        var init = new ComposeInit { AccountId = Acc, Subject = "ダブルクリックの下書き" };
        _drafts.OpenForEditByMessageAsync(Acc, 2, Arg.Any<CancellationToken>()).Returns(init);
        AddMails(2, FolderKind.Drafts);
        await _controller.DoubleClickAsync(Row(2));
        _compose.Received(1).Open(init);
    }

    [Fact]
    public async Task DoubleClick_InDrafts_IgnoresOtherCheckedRows()
    {
        var init = new ComposeInit { AccountId = Acc, Subject = "対象の行" };
        _drafts.OpenForEditByMessageAsync(Acc, 2, Arg.Any<CancellationToken>()).Returns(init);
        AddMails(2, FolderKind.Drafts);
        Row(1).IsChecked = true;
        await _controller.DoubleClickAsync(Row(2));

        await _drafts.Received(1).OpenForEditByMessageAsync(Acc, 2, Arg.Any<CancellationToken>());
        await _drafts.DidNotReceive().OpenForEditByMessageAsync(Acc, 1, Arg.Any<CancellationToken>());
        _compose.Received(1).Open(init);
    }

    [Theory]
    [InlineData(FolderKind.Inbox)]
    [InlineData(FolderKind.Sent)]
    [InlineData(FolderKind.Trash)]
    [InlineData(FolderKind.Spam)]
    public async Task DoubleClick_OutsideDrafts_DoesNothing(FolderKind folder)
    {
        AddMails(1, folder);
        await _controller.DoubleClickAsync(Row(1));

        await _drafts.DidNotReceiveWithAnyArgs().OpenForEditByMessageAsync(default!, default, default);
        _compose.DidNotReceiveWithAnyArgs().TryActivateExisting();
        _compose.DidNotReceiveWithAnyArgs().Open(default!);
    }

    [Fact]
    public async Task DoubleClick_InDrafts_WhenComposeWindowIsOpen_OnlyActivatesIt()
    {
        _compose.TryActivateExisting().Returns(true);
        AddMails(1, FolderKind.Drafts);
        await _controller.DoubleClickAsync(Row(1));

        await _drafts.DidNotReceiveWithAnyArgs().OpenForEditByMessageAsync(default!, default, default);
        _compose.DidNotReceiveWithAnyArgs().Open(default!);
    }

    [Fact]
    public async Task OutboxActions_CallOutboxService()
    {
        var item = new OutboxItem { AccountId = Acc, Subject = "送信待ち" };
        var repo = new InMemoryOutboxRepository();
        var saved = repo.Add(item);
        var list = new ListViewModel(_mail, repo, _draftRepo, _state, _clock, null, null, null, _dialogs, a => a());
        var controller = new FolderActionController(_service, list, _state, _notifier, _dialogs, _clock, _mail, _log, outbox: _outbox);
        list.LoadFolder(FolderKind.Outbox);
        var row = list.Items.Single();

        await controller.InvokeAsync(FolderActionKind.SendNow, row);
        await controller.InvokeAsync(FolderActionKind.ReturnToDrafts, row);
        await _outbox.Received(1).SendNowAsync(saved.Id, Arg.Any<CancellationToken>());
        await _outbox.Received(1).ReturnToDraftsAsync(saved.Id, Arg.Any<CancellationToken>());
    }
}
