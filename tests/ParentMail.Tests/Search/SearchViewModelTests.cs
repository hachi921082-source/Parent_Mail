using ParentMail.App.Features.MailList;
using ParentMail.App.Features.Search;
using ParentMail.Core.Models;
using ParentMail.Tests.Data;
using ParentMail.Tests.Fakes;
using static ParentMail.Tests.Data.CacheFixture;

namespace ParentMail.Tests.Search;

// 検索ボックスの状態と、一覧（ListViewModel）の検索結果表示のテスト。
public sealed class SearchViewModelTests : IDisposable
{
    private readonly CacheFixture _f = new();
    private readonly FakeAppState _state = new() { CurrentAccountId = AccountId };
    private readonly ListViewModel _list;
    private readonly SearchViewModel _vm;

    public SearchViewModelTests()
    {
        _list = new ListViewModel(_f.Mail, _f.Outbox, _f.Drafts, _state, _f.Clock, dispatch: a => a());
        _vm = new SearchViewModel(new SearchService(_f.Mail, _f.Clock), _list, _state);
    }

    public void Dispose() => _f.Dispose();

    private void Put(long id, string subject, params FolderKind[] folders)
        => _f.Mail.Upsert(AccountId, Msg(id, subject: subject), folders.Select(k => In(id, k, (uint)(100 + id))).ToList());

    private async Task Search(string q)
    {
        _vm.Query = q;
        await _vm.SearchCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task Search_ReplacesList_WithFolderLabels_AndChecksAreAvailable()
    {
        Put(1, "運動会のお知らせ", FolderKind.Inbox);
        Put(2, "旅行の件", FolderKind.Inbox);
        Put(3, "運動靴の領収書", FolderKind.AllMail);
        _list.LoadFolder(FolderKind.Inbox);
        Assert.Equal(2, _list.Items.Count);

        await Search("運動");

        Assert.True(_vm.IsActive);
        Assert.True(_list.IsSearchActive);
        Assert.Equal(new long?[] { 3, 1 }, _list.Items.Select(i => i.GmMsgId).ToArray());
        Assert.All(_list.Items, i => Assert.NotNull(i.FolderLabel));
        Assert.True(_list.ShowCheckboxColumn);
        Assert.False(_list.ShowEmptyMessage);
        Assert.False(_vm.ShowNoResults);
    }

    [Fact]
    public async Task NoResults_ShowsNoResultsFlag_AndHidesListEmptyMessage()
    {
        Put(1, "件名", FolderKind.Inbox);
        await Search("存在しない語");
        Assert.True(_vm.ShowNoResults);
        Assert.True(_vm.IsActive);
        Assert.Empty(_list.Items);
        Assert.False(_list.ShowEmptyMessage);
    }

    [Fact]
    public async Task Search_InTrashFolder_StillShowsCheckboxes()
    {
        Put(1, "ごみ箱の中", FolderKind.Trash);
        _state.CurrentFolder = FolderKind.Trash;
        _list.LoadFolder(FolderKind.Trash);
        Assert.False(_list.ShowCheckboxColumn);
        await Search("ごみ箱");
        Assert.True(_list.ShowCheckboxColumn);
    }

    [Fact]
    public async Task Clear_RestoresFolderView_ResetsQuery_AndChecks()
    {
        Put(1, "運動会", FolderKind.Inbox);
        Put(2, "旅行", FolderKind.Inbox);
        _list.LoadFolder(FolderKind.Inbox);
        await Search("運動");
        _list.ToggleCheck(_list.Items[0]);
        Assert.Equal(1, _list.CheckedCount);

        _vm.ClearCommand.Execute(null);

        Assert.False(_vm.IsActive);
        Assert.False(_list.IsSearchActive);
        Assert.Equal("", _vm.Query);
        Assert.Equal(0, _list.CheckedCount);
        Assert.Equal(2, _list.Items.Count);
        Assert.Null(_list.Items[0].FolderLabel);
    }

    [Fact]
    public async Task FolderOrAccountChange_ClearsSearch()
    {
        Put(1, "運動会", FolderKind.Inbox);
        await Search("運動");
        _state.CurrentFolder = FolderKind.Sent;
        Assert.False(_vm.IsActive);
        Assert.False(_list.IsSearchActive);
        Assert.Equal("", _vm.Query);

        await Search("運動"); // フォルダが違っても全体を探す
        Assert.True(_vm.IsActive);
        _state.CurrentAccountId = OtherAccountId;
        Assert.False(_vm.IsActive);
        Assert.False(_list.IsSearchActive);
    }

    [Fact]
    public async Task ChecksAreClearedWhenANewSearchRuns()
    {
        Put(1, "運動会", FolderKind.Inbox);
        Put(2, "運動靴", FolderKind.Inbox);
        await Search("運動");
        _list.SelectAllVisible();
        Assert.Equal(2, _list.CheckedCount);
        await Search("運動会");
        Assert.Equal(0, _list.CheckedCount);
        Assert.Single(_list.Items);
    }

    [Fact]
    public async Task SelectingAResult_ReportsTheRowsOwnFolder()
    {
        Put(1, "運動会", FolderKind.Trash, FolderKind.AllMail);
        Put(2, "運動靴", FolderKind.Inbox);
        _list.LoadFolder(FolderKind.Sent);
        await Search("運動");
        var seen = new List<(long?, FolderKind)>();
        _list.SelectionChanged += (_, e) => seen.Add((e.Item?.GmMsgId, e.Folder));
        _list.SelectedItem = _list.Items.Single(i => i.GmMsgId == 1);
        _list.SelectedItem = _list.Items.Single(i => i.GmMsgId == 2);
        Assert.Equal(new (long?, FolderKind)[] { (1, FolderKind.Trash), (2, FolderKind.Inbox) }, seen);
    }

    [Fact]
    public async Task Refresh_WhileSearching_KeepsResultsAndSelection_AndPicksUpNewMail()
    {
        Put(1, "運動会", FolderKind.Inbox);
        await Search("運動");
        _list.SelectedItem = _list.Items[0];
        var kept = _list.Items[0];

        Put(2, "運動靴", FolderKind.Inbox);
        _list.Refresh(); // 同期の完了など
        await Task.Delay(500);

        Assert.True(_list.IsSearchActive);
        Assert.Equal(2, _list.Items.Count);
        Assert.Same(kept, _list.SelectedItem);
    }

    [Fact]
    public async Task BlankQuery_DoesNothing()
    {
        Put(1, "件名", FolderKind.Inbox);
        _list.LoadFolder(FolderKind.Inbox);
        await Search("   ");
        Assert.False(_vm.IsActive);
        Assert.False(_list.IsSearchActive);
        Assert.Single(_list.Items);
    }

    [Fact]
    public void RequestFocus_RaisesFocusRequested()
    {
        var n = 0;
        _vm.FocusRequested += (_, _) => n++;
        _vm.RequestFocus();
        Assert.Equal(1, n);
    }
}
