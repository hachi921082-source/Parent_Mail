using NSubstitute;
using ParentMail.App.Features.MailList;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.MailList;

// 一覧の表示データ（行・ページング・選択・チェック・空の表示）のテスト。日本語の件名・差出人・宛先を使う。
public sealed class ListViewModelTests
{
    private const string Acc = "acc1";

    private readonly InMemoryMailRepository _mail = new();
    private readonly InMemoryOutboxRepository _outbox = new();
    private readonly InMemoryDraftRepository _drafts = new();
    private readonly FakeAppState _state = new() { CurrentAccountId = Acc };
    private readonly FakeClock _clock = new() { UtcNow = new DateTime(2026, 10, 10, 6, 0, 0, DateTimeKind.Utc) };
    private readonly FakeSyncService _sync = new();
    private readonly ISyncCacheStore _syncStore = Substitute.For<ISyncCacheStore>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private ListViewModel Vm(IMailRepository? mail = null)
        => new(mail ?? _mail, _outbox, _drafts, _state, _clock, _sync, _syncStore, null, _dialogs, a => a());

    private DateTime T(int minutesAgo) => _clock.UtcNow.AddMinutes(-minutesAgo);

    private void Add(long id, string subject, int minutesAgo, FolderKind folder = FolderKind.Inbox, bool read = true,
        string from = "山田 太郎", string fromAddr = "taro@example.jp", string[]? to = null)
        => _mail.Upsert(Acc, new MailMessage
        {
            GmailMsgId = id,
            Subject = subject,
            FromName = from,
            FromAddress = fromAddr,
            To = to ?? Array.Empty<string>(),
            DateUtc = T(minutesAgo),
            IsRead = read,
        }, new[] { new MessageFolder(id, folder, (uint)id, 1) });

    private void AddMany(int count, FolderKind folder = FolderKind.Inbox)
    {
        for (var i = 1; i <= count; i++) Add(i, "件名" + i, i, folder: folder); // id が小さいほど新しい
    }

    // ---- 行の表示 ----

    [Fact]
    public void LoadFolder_ShowsNewestFirst_WithJapaneseSenderSubjectAndDate()
    {
        Add(1, "ご案内", 120);
        Add(2, "お孫さんの写真", 10, read: false, from: "田中 花子");
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);

        Assert.Equal(new long?[] { 2, 1 }, vm.Items.Select(i => i.GmMsgId).ToArray());
        var first = vm.Items[0];
        Assert.Equal("田中 花子", first.Sender);
        Assert.Equal("お孫さんの写真", first.Subject);
        Assert.True(first.IsUnread);
        Assert.False(vm.Items[1].IsUnread);
        Assert.Equal(DateFormatter.Format(T(10), _clock.LocalNow), first.DateText);
    }

    [Fact]
    public void Sender_FallsBackToAddress_ThenPlaceholder_AndEmptySubjectShowsPlaceholder()
    {
        Add(1, "", 5, from: "", fromAddr: "no-name@example.jp");
        Add(2, "  ", 6, from: "", fromAddr: "");
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);

        Assert.Equal("no-name@example.jp", vm.Items[0].Sender);
        Assert.Equal(Strings.ML_13, vm.Items[0].Subject);
        Assert.Equal(Strings.ML_12, vm.Items[1].Sender);
    }

    [Fact]
    public void SentFolder_ShowsRecipientSummaryInSenderPosition()
    {
        Add(1, "お礼", 5, FolderKind.Sent, to: new[] { "孫 <mago@example.jp>", "b@example.jp", "c@example.jp" });
        var vm = Vm();
        vm.LoadFolder(FolderKind.Sent);
        Assert.Equal("宛先: 孫 <mago@example.jp> ほか 2 人", vm.Items[0].Sender);
    }

    [Fact]
    public void AttachmentFlag_IsCarriedToRow()
    {
        _mail.Upsert(Acc, new MailMessage { GmailMsgId = 1, Subject = "写真", DateUtc = T(1), HasAttachments = true, IsRead = true },
            new[] { new MessageFolder(1, FolderKind.Inbox, 1, 1) });
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        Assert.True(vm.Items[0].HasAttachment);
    }

    [Fact]
    public void OutboxRows_ComeFromOutboxTable_FailedOnesAreMarked_NoCheckbox()
    {
        _outbox.Add(new OutboxItem { AccountId = Acc, To = new[] { "母 <haha@example.jp>" }, Subject = "送信待ち", CreatedUtc = T(30) });
        _outbox.Add(new OutboxItem { AccountId = Acc, To = new[] { "a@example.jp", "b@example.jp" }, Subject = "", CreatedUtc = T(5), FailureNotified = true });
        var vm = Vm();
        vm.LoadFolder(FolderKind.Outbox);

        Assert.Equal(2, vm.Items.Count);
        Assert.True(vm.Items[0].IsFailed);
        Assert.Equal("宛先: a@example.jp ほか 1 人", vm.Items[0].Sender);
        Assert.Equal(Strings.ML_13, vm.Items[0].Subject);
        Assert.False(vm.Items[1].IsFailed);
        Assert.All(vm.Items, i => Assert.False(i.ShowCheckbox));
        Assert.False(vm.ShowCheckboxColumn);
    }

    [Fact]
    public void DraftsFolder_ShowsLocalDraftsFirst_ThenCachedDrafts()
    {
        Add(7, "下書きの手紙", 60, FolderKind.Drafts, to: new[] { "x@example.jp" });
        _drafts.Save(new DraftItem { AccountId = Acc, To = new[] { "母@example.jp" }, Subject = "書きかけ" });
        _drafts.Save(new DraftItem { AccountId = Acc, Subject = "同期済み", GmailUid = 3 });
        var vm = Vm();
        vm.LoadFolder(FolderKind.Drafts);

        Assert.Equal(2, vm.Items.Count);
        Assert.NotNull(vm.Items[0].DraftId);
        Assert.Equal("書きかけ", vm.Items[0].Subject);
        Assert.Equal(7, vm.Items[1].GmMsgId);
    }

    // ---- ページング ----

    [Fact]
    public void Paging_LoadsTwoHundredAtATime()
    {
        AddMany(450);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        Assert.Equal(200, vm.Items.Count);
        Assert.Equal(1, vm.Items[0].GmMsgId);

        vm.LoadMore();
        Assert.Equal(400, vm.Items.Count);
        vm.LoadMore();
        Assert.Equal(450, vm.Items.Count);
        vm.LoadMore();
        Assert.Equal(450, vm.Items.Count);
        Assert.Equal(450, vm.Items.Select(i => i.Key).Distinct().Count());
        Assert.Equal(450, vm.Items[^1].GmMsgId);
    }

    [Fact]
    public void Refresh_KeepsLoadedPages()
    {
        AddMany(450);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.LoadMore();
        vm.Refresh();
        Assert.Equal(400, vm.Items.Count);
    }

    // ---- 再読み込みと選択 ----

    [Fact]
    public void Refresh_KeepsSelectionAndInstances_WhenNewMailArrives()
    {
        Add(1, "古い", 100);
        Add(2, "新しい", 50);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.SelectedItem = vm.Items[1]; // 利用者の選択
        var selected = vm.SelectedItem;
        var events = new List<SelectionChangedArgs>();
        vm.SelectionChanged += (_, e) => events.Add(e);

        Add(3, "新着", 1, read: false);
        vm.Refresh();

        Assert.Equal(3, vm.Items.Count);
        Assert.Equal(3, vm.Items[0].GmMsgId);
        Assert.Same(selected, vm.SelectedItem);
        Assert.Equal(1, vm.SelectedItem!.GmMsgId);
        Assert.Empty(events); // 選択は変わっていないので通知しない
    }

    [Fact]
    public void Refresh_UpdatesReadStateInPlace()
    {
        Add(1, "未読", 10, read: false);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        var row = vm.Items[0];
        Assert.True(row.IsUnread);

        _mail.SetRead(Acc, 1, true);
        vm.Refresh();

        Assert.Same(row, vm.Items[0]);
        Assert.False(row.IsUnread);
    }

    [Fact]
    public void Refresh_WhenSelectedMailDisappears_ClearsSelectionAsAuto()
    {
        Add(1, "a", 10);
        Add(2, "b", 5);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.SelectedItem = vm.Items[0];
        var events = new List<SelectionChangedArgs>();
        vm.SelectionChanged += (_, e) => events.Add(e);

        _mail.DeleteMessage(Acc, 2);
        vm.Refresh();

        Assert.Null(vm.SelectedItem);
        Assert.Single(events);
        Assert.Equal(SelectionSource.Auto, events[0].Source);
        Assert.False(events[0].ShouldMarkRead);
    }

    [Fact]
    public void Selection_UpdatesAppStateMessageId()
    {
        Add(1, "a", 10);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.SelectedItem = vm.Items[0];
        Assert.Equal(1, _state.SelectedMessageId);
        vm.SelectAuto(null);
        Assert.Null(_state.SelectedMessageId);
    }

    [Fact]
    public async Task SyncCompleted_ForCurrentAccount_RefreshesList()
    {
        Add(1, "a", 10);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        Add(2, "新着", 1);

        await _sync.SyncNowAsync("other", SyncTrigger.Manual);
        Assert.Single(vm.Items);

        await _sync.SyncNowAsync(Acc, SyncTrigger.Manual);
        Assert.Equal(2, vm.Items.Count);
    }

    [Fact]
    public void FolderOrAccountChange_ReloadsAndClearsSelectionAndChecks()
    {
        Add(1, "受信", 10);
        Add(2, "送信済み", 10, FolderKind.Sent, to: new[] { "x@example.jp" });
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.SelectedItem = vm.Items[0];
        vm.ToggleCheck(vm.Items[0]);
        Assert.Equal(1, vm.CheckedCount);

        _state.CurrentFolder = FolderKind.Sent;

        Assert.Equal(FolderKind.Sent, vm.Folder);
        Assert.Equal(2, vm.Items.Single().GmMsgId);
        Assert.Null(vm.SelectedItem);
        Assert.Equal(0, vm.CheckedCount);

        _state.CurrentAccountId = null;
        Assert.Empty(vm.Items);
    }

    // ---- 選択後の次の行・既読の起点 ----

    private ListViewModel ThreeRows()
    {
        Add(1, "新", 10);
        Add(2, "中", 20);
        Add(3, "古", 30);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        return vm;
    }

    [Fact]
    public void AfterRemoval_SelectsNextOlder()
    {
        var vm = ThreeRows();
        vm.SelectedItem = vm.Items[1]; // 中
        var events = new List<SelectionChangedArgs>();
        vm.SelectionChanged += (_, e) => events.Add(e);

        vm.SelectNextAfterRemoval(new long[] { 2 });

        Assert.Equal(new long?[] { 1, 3 }, vm.Items.Select(i => i.GmMsgId).ToArray());
        Assert.Equal(3, vm.SelectedItem!.GmMsgId);
        Assert.Equal(SelectionSource.Auto, events.Last().Source);
        Assert.False(events.Last().ShouldMarkRead);
    }

    [Fact]
    public void AfterRemoval_OfOldest_SelectsPreviousNewer()
    {
        var vm = ThreeRows();
        vm.SelectedItem = vm.Items[2];
        vm.SelectNextAfterRemoval(new long[] { 3 });
        Assert.Equal(2, vm.SelectedItem!.GmMsgId);
    }

    [Fact]
    public void AfterRemoval_OfLastRemaining_SelectsNothing()
    {
        Add(1, "唯一", 10);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.SelectedItem = vm.Items[0];
        vm.SelectNextAfterRemoval(new long[] { 1 });
        Assert.Null(vm.SelectedItem);
        Assert.Empty(vm.Items);
        Assert.Null(_state.SelectedMessageId);
        Assert.True(vm.ShowEmptyMessage);
    }

    [Fact]
    public void AfterRemoval_SkipsOtherRemovedRows_WhenPickingNext()
    {
        var vm = ThreeRows();
        vm.SelectedItem = vm.Items[0];
        vm.SelectNextAfterRemoval(new long[] { 1, 2 });
        Assert.Equal(3, vm.SelectedItem!.GmMsgId);
    }

    [Fact]
    public void AfterRemoval_OfNonSelectedRows_KeepsSelection()
    {
        var vm = ThreeRows();
        vm.SelectedItem = vm.Items[0];
        var events = 0;
        vm.SelectionChanged += (_, _) => events++;
        vm.SelectNextAfterRemoval(new long[] { 3 });
        Assert.Equal(1, vm.SelectedItem!.GmMsgId);
        Assert.Equal(0, events);
        Assert.Equal(2, vm.Items.Count);
    }

    [Theory]
    [InlineData(FolderKind.Inbox, SelectionSource.User, true)]
    [InlineData(FolderKind.Spam, SelectionSource.User, true)]
    [InlineData(FolderKind.Sent, SelectionSource.User, false)]
    [InlineData(FolderKind.Trash, SelectionSource.User, false)]
    [InlineData(FolderKind.Drafts, SelectionSource.User, false)]
    [InlineData(FolderKind.Inbox, SelectionSource.Auto, false)]
    [InlineData(FolderKind.Spam, SelectionSource.Auto, false)]
    public void ShouldMarkRead_OnlyForUserSelectionInInboxAndSpam(FolderKind folder, SelectionSource source, bool expected)
    {
        Add(1, "x", 10, folder: folder, to: new[] { "a@example.jp" });
        var vm = Vm();
        vm.LoadFolder(folder);
        SelectionChangedArgs? got = null;
        vm.SelectionChanged += (_, e) => got = e;

        if (source == SelectionSource.User) vm.SelectedItem = vm.Items[0];
        else vm.SelectAuto(vm.Items[0]);

        Assert.NotNull(got);
        Assert.Equal(source, got!.Source);
        Assert.Equal(expected, got.ShouldMarkRead);
    }

    // ---- チェック（複数選択） ----

    [Fact]
    public void Check_IsIndependentOfSelection_AndDoesNotSelectOrMarkRead()
    {
        Add(1, "a", 10, read: false);
        Add(2, "b", 20);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.SelectedItem = vm.Items[1];
        var events = 0;
        vm.SelectionChanged += (_, _) => events++;

        vm.Items[0].IsChecked = true; // チェック欄のクリック相当
        vm.ToggleCheck(vm.Items[1]);

        Assert.Equal(2, vm.CheckedCount);
        Assert.True(vm.HasChecked);
        Assert.Equal("2通を選んでいます", vm.CheckedSummary);
        Assert.Equal(2, vm.SelectedItem!.GmMsgId);
        Assert.Equal(0, events);
        Assert.True(_mail.Get(Acc, 1) is { IsRead: false });
        Assert.Equal(new[] { "m:1", "m:2" }, vm.GetCheckedItems().Select(i => i.Key).OrderBy(k => k).ToArray());
    }

    [Fact]
    public void SelectAllVisible_ChecksUpToTwoHundred()
    {
        AddMany(250);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.LoadMore(); // 250 行が一覧にある

        vm.SelectAllVisible();

        Assert.Equal(200, vm.CheckedCount);
        Assert.True(vm.SelectAllChecked);
        Assert.True(vm.Items.Take(200).All(i => i.IsChecked));
        Assert.False(vm.Items[200].IsChecked);
    }

    [Fact]
    public void ToggleSelectAll_SecondCallClearsAllChecks()
    {
        AddMany(5);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.ToggleSelectAll();
        Assert.Equal(5, vm.CheckedCount);
        vm.ToggleSelectAll();
        Assert.Equal(0, vm.CheckedCount);
        Assert.False(vm.HasChecked);
    }

    [Fact]
    public void SelectAllChecked_IsFalseWhenOneIsUnchecked()
    {
        AddMany(3);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.SelectAllVisible();
        vm.Items[1].IsChecked = false;
        Assert.False(vm.SelectAllChecked);
        Assert.Equal(2, vm.CheckedCount);
    }

    [Fact]
    public void ClearChecks_UnchecksRows()
    {
        AddMany(3);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.SelectAllVisible();
        vm.ClearChecks();
        Assert.Equal(0, vm.CheckedCount);
        Assert.All(vm.Items, i => Assert.False(i.IsChecked));
    }

    [Fact]
    public void Checks_AreKeptOnRefresh_AndDroppedWhenMailDisappears()
    {
        AddMany(3);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        vm.SelectAllVisible();

        _mail.DeleteMessage(Acc, 2);
        Add(10, "新着", 0);
        vm.Refresh();

        Assert.Equal(2, vm.CheckedCount);
        Assert.DoesNotContain("m:2", vm.CheckedIds);
        Assert.False(vm.Items.First(i => i.GmMsgId == 10).IsChecked);
        Assert.True(vm.Items.First(i => i.GmMsgId == 1).IsChecked);
    }

    [Fact]
    public void ChecksChanged_IsRaised()
    {
        AddMany(2);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        var count = 0;
        vm.ChecksChanged += (_, _) => count++;
        vm.ToggleCheck(vm.Items[0]);
        vm.ClearChecks();
        Assert.Equal(2, count);
    }

    [Fact]
    public void TrashFolder_HasNoCheckboxes_AndSelectAllDoesNothing()
    {
        AddMany(3, FolderKind.Trash);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Trash);

        Assert.False(vm.ShowCheckboxColumn);
        Assert.All(vm.Items, i => Assert.False(i.ShowCheckbox));
        vm.SelectAllVisible();
        vm.ToggleCheck(vm.Items[0]);
        Assert.Equal(0, vm.CheckedCount);
    }

    [Theory]
    [InlineData(FolderKind.Inbox)]
    [InlineData(FolderKind.Sent)]
    [InlineData(FolderKind.Spam)]
    [InlineData(FolderKind.Drafts)]
    public void CheckboxColumn_IsShownInOtherFolders(FolderKind folder)
    {
        var vm = Vm();
        vm.LoadFolder(folder);
        Assert.True(vm.ShowCheckboxColumn);
    }

    // ---- 空の表示・読み込み失敗・未読数 ----

    [Fact]
    public void EmptyMessage_ShownWhenEmpty_ButNotDuringFirstSync()
    {
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        Assert.True(vm.ShowEmptyMessage);

        _syncStore.IsInitialSyncDone(Acc).Returns(false);
        _sync.RaiseStateChanged(Acc, SyncState.Syncing);
        Assert.False(vm.ShowEmptyMessage);

        _sync.RaiseStateChanged(Acc, SyncState.Idle);
        Assert.True(vm.ShowEmptyMessage);
    }

    [Fact]
    public void EmptyMessage_ShownWhileSyncingAfterFirstSyncIsDone()
    {
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        _syncStore.IsInitialSyncDone(Acc).Returns(true);
        _sync.RaiseStateChanged(Acc, SyncState.Syncing);
        Assert.True(vm.ShowEmptyMessage);
    }

    [Fact]
    public void EmptyMessage_NotShownWhenRowsExist()
    {
        AddMany(1);
        var vm = Vm();
        vm.LoadFolder(FolderKind.Inbox);
        Assert.False(vm.ShowEmptyMessage);
    }

    [Fact]
    public void LoadFailure_ShowsModalNoticeOnce_AndHidesEmptyMessage()
    {
        var broken = Substitute.For<IMailRepository>();
        broken.GetList(Arg.Any<string>(), Arg.Any<FolderKind>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(_ => throw new InvalidOperationException("db"));
        var vm = Vm(broken);

        vm.LoadFolder(FolderKind.Inbox);
        vm.Refresh();

        _dialogs.Received(1).ShowNotice(Arg.Any<string>(), Strings.BN_24);
        Assert.False(vm.ShowEmptyMessage);
    }

    [Fact]
    public void UnreadCounter_CountsInboxUnreadOfCurrentAccount_AndOutboxFailures()
    {
        Add(1, "a", 1, read: false);
        Add(2, "b", 2, read: false);
        Add(3, "c", 3, read: true);
        Add(4, "d", 4, read: false, folder: FolderKind.Sent, to: new[] { "x@example.jp" });
        var outbox = Substitute.For<IOutboxService>();
        outbox.FailedCount(Acc).Returns(2);
        var counter = new UnreadCounter(_mail, _state, outbox);

        Assert.Equal(2, counter.GetInboxUnread());
        Assert.Equal(2, counter.GetOutboxFailed());
        _state.CurrentAccountId = null;
        Assert.Equal(0, counter.GetInboxUnread());
        Assert.Equal(0, counter.GetOutboxFailed());
    }

    [Fact]
    public void Reloaded_IsRaisedOnLoadAndRefresh()
    {
        var vm = Vm();
        var n = 0;
        vm.Reloaded += (_, _) => n++;
        vm.LoadFolder(FolderKind.Inbox);
        vm.Refresh();
        Assert.Equal(2, n);
    }
}
