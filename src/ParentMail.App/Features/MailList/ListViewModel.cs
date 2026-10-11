using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.MailList;

/// <summary>
/// 一覧ペインの表示データ。フォルダの読み込み（200 通ずつ）、行、選択、チェック（複数選択）、空の表示を受け持つ。
/// DB の読み込みは UI スレッドで行う（ヘッダ列だけの 200 行なので軽い）。同期の完了などバックグラウンドからの通知は dispatch で UI に戻す。
/// 削除そのものは folder-actions、検索の UI は search が担当する。
/// </summary>
public sealed partial class ListViewModel : ObservableObject
{
    public const int PageSize = 200;

    private readonly IMailRepository _mail;
    private readonly IOutboxRepository _outbox;
    private readonly IDraftRepository _drafts;
    private readonly IAppState _state;
    private readonly IClock _clock;
    private readonly ISyncService? _sync;
    private readonly ISyncCacheStore? _syncStore;
    private readonly IAppLog? _log;
    private readonly IDialogService? _dialogs;
    private readonly Action<Action> _dispatch;

    private readonly HashSet<string> _checked = new();
    private FolderKind _folder = FolderKind.Inbox;
    private string? _accountId;
    private int _mailLoaded;
    private bool _hasMore;
    private int _autoDepth;
    private bool _loadFailed;
    private bool _failureShown;
    private MailListItem? _selected;
    private bool _searchActive;

    public ListViewModel(IMailRepository mail, IOutboxRepository outbox, IDraftRepository drafts, IAppState state,
        IClock clock, ISyncService? sync = null, ISyncCacheStore? syncStore = null, IAppLog? log = null,
        IDialogService? dialogs = null, Action<Action>? dispatch = null)
    {
        _mail = mail;
        _outbox = outbox;
        _drafts = drafts;
        _state = state;
        _clock = clock;
        _sync = sync;
        _syncStore = syncStore;
        _log = log;
        _dialogs = dialogs;
        _dispatch = dispatch ?? DispatchToUi;

        Items.CollectionChanged += OnItemsChanged;
        _state.CurrentAccountChanged += (_, _) => _dispatch(Reload);
        _state.CurrentFolderChanged += (_, _) => _dispatch(Reload);
        if (_sync is not null)
        {
            _sync.SyncCompleted += (_, e) => _dispatch(() => { if (e.AccountId == _state.CurrentAccountId) Refresh(); });
            _sync.StateChanged += (_, e) => _dispatch(() => { if (e.AccountId == _state.CurrentAccountId) RaiseEmptyChanged(); });
        }
    }

    public ObservableCollection<MailListItem> Items { get; } = new();

    public FolderKind Folder => _folder;

    /// <summary>選択された行。ビューの操作（クリック・上下キー）で変わったときは User、アプリの自動選択は SelectAuto 経由で Auto。</summary>
    public MailListItem? SelectedItem
    {
        get => _selected;
        set => SetSelected(value, _autoDepth > 0 ? SelectionSource.Auto : SelectionSource.User);
    }

    /// <summary>選択の変更（起点つき）。mail-display が購読し、User かつ受信トレイ・迷惑メールなら既読にする。</summary>
    public event EventHandler<SelectionChangedArgs>? SelectionChanged;

    /// <summary>一覧を読み込み直した（上部バーのバッジ更新に使う）。</summary>
    public event EventHandler? Reloaded;

    /// <summary>検索結果の表示中に同期の完了などで再読み込みが要るとき（search が結果を取り直して UpdateSearchResults を呼ぶ）。</summary>
    public event EventHandler? SearchRefreshRequested;

    /// <summary>チェックの増減（mail-display のプレビュー切替に使う）。</summary>
    public event EventHandler? ChecksChanged;

    // ---- 表示状態 ----

    /// <summary>チェック欄・「すべて選ぶ」を出すフォルダか（ゴミ箱と送信箱は出さない、A62）。</summary>
    public bool ShowCheckboxColumn => _searchActive || ShowCheckboxFor(_folder);

    /// <summary>「メールはありません」を出すか。初回同期の最中と読み込み失敗のときは出さない（Q62）。</summary>
    public bool ShowEmptyMessage => !_searchActive && Items.Count == 0 && !_loadFailed && !IsFirstSyncRunning();

    /// <summary>検索結果を表示中か（フォルダの切替・アカウントの切替・クリアで false に戻る）。</summary>
    public bool IsSearchActive => _searchActive;

    public int CheckedCount => _checked.Count;
    public bool HasChecked => _checked.Count > 0;
    public string CheckedSummary => Strings.ML_10.Replace("N", _checked.Count.ToString());

    /// <summary>「すべて選ぶ」の状態。先頭から最大 200 行がすべてチェック済みのとき true。</summary>
    public bool SelectAllChecked
    {
        get
        {
            var rows = Items.Where(i => i.ShowCheckbox).Take(Constants.MaxDeleteBatch).ToList();
            return rows.Count > 0 && rows.All(i => i.IsChecked);
        }
    }

    /// <summary>チェック済みのキー（メール ID は "m:ID"）。選択行とは独立（A59）。</summary>
    public IReadOnlyCollection<string> CheckedIds => _checked;

    public IReadOnlyList<MailListItem> GetCheckedItems() => Items.Where(i => i.IsChecked).ToList();

    // ---- 読み込み ----

    /// <summary>いまのアカウントとフォルダ（IAppState）で読み直す。チェックと選択は解除する。</summary>
    public void Reload() => LoadFolder(_state.CurrentFolder);

    /// <summary>フォルダを切り替えて先頭 200 通を読む。チェックは解除し、選択も外す（Auto）。</summary>
    public void LoadFolder(FolderKind kind)
    {
        var wasSearch = _searchActive;
        _searchActive = false;
        if (wasSearch) OnPropertyChanged(nameof(IsSearchActive));
        _folder = kind;
        _accountId = _state.CurrentAccountId;
        ClearChecks();
        SelectAuto(null);
        Items.Clear();
        _mailLoaded = 0;
        _hasMore = false;
        OnPropertyChanged(nameof(Folder));
        OnPropertyChanged(nameof(ShowCheckboxColumn));

        if (_accountId is null)
        {
            RaiseReloaded();
            return;
        }
        try
        {
            var rows = QueryRows(0, PageSize, includeLocal: true, out var mailCount);
            foreach (var r in rows) AddRow(r);
            _mailLoaded = mailCount;
            _hasMore = HasPaging(_folder) && mailCount >= PageSize;
            _loadFailed = false;
            _failureShown = false;
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        RaiseEmptyChanged();
        RaiseReloaded();
    }

    /// <summary>同期の完了後・操作後の読み直し。選択・チェック・スクロールを保つ（A52, A65）。</summary>
    public void Refresh()
    {
        if (_accountId != _state.CurrentAccountId || _accountId is null)
        {
            LoadFolder(_state.CurrentFolder);
            return;
        }
        if (_searchActive)
        {
            SearchRefreshRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        try
        {
            var count = Math.Max(PageSize, _mailLoaded);
            var rows = QueryRows(0, count, includeLocal: true, out var mailCount);
            MergeRows(rows);
            _mailLoaded = mailCount;
            _hasMore = HasPaging(_folder) && mailCount >= count;
            // 消えたメールのチェックは捨てる
            var keys = Items.Select(i => i.Key).ToHashSet();
            if (_checked.RemoveWhere(k => !keys.Contains(k)) > 0) RaiseChecksChanged();
            if (_selected is not null && !Items.Contains(_selected)) SelectAuto(null);
            _loadFailed = false;
            _failureShown = false;
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        RaiseEmptyChanged();
        RaiseReloaded();
    }

    /// <summary>
    /// 検索結果で一覧を置き換える（search が呼ぶ）。チェックと選択は解除する（Q71）。
    /// 結果の行は検索側が作る（フォルダ名つき、チェック欄あり）。ページ送りはしない。
    /// </summary>
    public void ShowSearchResults(IReadOnlyList<MailListItem> rows)
    {
        _accountId = _state.CurrentAccountId;
        ClearChecks();
        SelectAuto(null);
        Items.Clear();
        _searchActive = true;
        _hasMore = false;
        _mailLoaded = 0;
        _loadFailed = false;
        foreach (var r in rows) AddRow(r);
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(ShowCheckboxColumn));
        RaiseEmptyChanged();
    }

    /// <summary>検索結果の取り直し。既存の行を使い回して、選択・チェック・スクロール位置を保つ。</summary>
    public void UpdateSearchResults(IReadOnlyList<MailListItem> rows)
    {
        if (!_searchActive) return;
        MergeRows(rows);
        var keys = Items.Select(i => i.Key).ToHashSet();
        if (_checked.RemoveWhere(k => !keys.Contains(k)) > 0) RaiseChecksChanged();
        if (_selected is not null && !Items.Contains(_selected)) SelectAuto(null);
        RaiseEmptyChanged();
    }

    /// <summary>下へスクロールして終わりに近づいたとき、次の 200 通を足す。</summary>
    public void LoadMore()
    {
        if (!_hasMore || _accountId is null) return;
        try
        {
            var rows = QueryRows(_mailLoaded, PageSize, includeLocal: false, out var mailCount);
            var existing = Items.Select(i => i.Key).ToHashSet();
            foreach (var r in rows.Where(r => !existing.Contains(r.Key))) AddRow(r);
            _mailLoaded += mailCount;
            _hasMore = mailCount >= PageSize;
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    private static bool HasPaging(FolderKind kind) => kind != FolderKind.Outbox;

    private static bool ShowCheckboxFor(FolderKind kind) => kind is not (FolderKind.Trash or FolderKind.Outbox);

    private List<MailListItem> QueryRows(int mailOffset, int mailLimit, bool includeLocal, out int mailCount)
    {
        var acc = _accountId!;
        var now = _clock.LocalNow;
        var rows = new List<MailListItem>();
        mailCount = 0;

        if (_folder == FolderKind.Outbox)
        {
            foreach (var o in _outbox.GetAll(acc).OrderByDescending(o => o.CreatedUtc))
            {
                rows.Add(new MailListItem("o:" + o.Id)
                {
                    OutboxId = o.Id,
                    SortUtc = o.CreatedUtc,
                    ShowCheckbox = false,
                    Sender = RecipientSummary.Format(o.To),
                    Subject = SubjectText(o.Subject),
                    DateText = DateFormatter.Format(o.CreatedUtc, now),
                    HasAttachment = o.Attachments.Count > 0,
                    IsFailed = o.FailureNotified,
                });
            }
            return rows;
        }

        if (includeLocal && _folder == FolderKind.Drafts)
        {
            // 同期でまだ Gmail に上がっていないローカル下書きを先頭に並べる
            foreach (var d in _drafts.GetAll(acc).Where(d => d.GmailUid is null).Reverse())
            {
                rows.Add(new MailListItem("d:" + d.LocalId)
                {
                    DraftId = d.LocalId,
                    ShowCheckbox = true,
                    Sender = RecipientSummary.Format(d.To),
                    Subject = SubjectText(d.Subject),
                    HasAttachment = d.Attachments.Count > 0,
                });
            }
        }

        var mails = _mail.GetList(acc, _folder, mailLimit, mailOffset);
        mailCount = mails.Count;
        foreach (var m in mails)
        {
            var sentLike = _folder is FolderKind.Sent or FolderKind.Drafts;
            rows.Add(new MailListItem("m:" + m.GmailMsgId)
            {
                GmMsgId = m.GmailMsgId,
                SortUtc = m.DateUtc,
                ShowCheckbox = ShowCheckboxFor(_folder),
                Sender = sentLike ? RecipientSummary.Format(m.To) : SenderText(m),
                Subject = SubjectText(m.Subject),
                DateText = DateFormatter.Format(m.DateUtc, now),
                IsUnread = !m.IsRead,
                HasAttachment = m.HasAttachments,
            });
        }
        return rows;
    }

    private static string SenderText(MailMessage m)
    {
        if (!string.IsNullOrWhiteSpace(m.FromName)) return m.FromName.Trim();
        if (!string.IsNullOrWhiteSpace(m.FromAddress)) return m.FromAddress.Trim();
        return Strings.ML_12;
    }

    private static string SubjectText(string? subject)
        => string.IsNullOrWhiteSpace(subject) ? Strings.ML_13 : subject.Trim();

    private void AddRow(MailListItem row)
    {
        row.IsChecked = _checked.Contains(row.Key);
        row.CheckChanged = OnItemChecked;
        Items.Add(row);
    }

    // 既存の行のインスタンスを使い回して差分だけ反映する（選択とスクロール位置を保つため）。
    private void MergeRows(IReadOnlyList<MailListItem> rows)
    {
        _autoDepth++;
        try
        {
            var newKeys = rows.Select(r => r.Key).ToHashSet();
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                if (!newKeys.Contains(Items[i].Key)) Items.RemoveAt(i);
            }
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (i < Items.Count && Items[i].Key == row.Key)
                {
                    Items[i].UpdateFrom(row);
                    continue;
                }
                var at = -1;
                for (var j = i + 1; j < Items.Count; j++)
                {
                    if (Items[j].Key == row.Key) { at = j; break; }
                }
                if (at >= 0)
                {
                    Items.Move(at, i);
                    Items[i].UpdateFrom(row);
                }
                else
                {
                    row.IsChecked = _checked.Contains(row.Key);
                    row.CheckChanged = OnItemChecked;
                    Items.Insert(i, row);
                }
            }
        }
        finally
        {
            _autoDepth--;
        }
    }

    private void Fail(Exception ex)
    {
        _loadFailed = true;
        _log?.Error("MailList", "list load failed", ex);
        if (_failureShown) return;
        _failureShown = true;
        _dialogs?.ShowNotice(Strings.WD_01, Strings.BN_24);
    }

    // ---- 選択 ----

    private void SetSelected(MailListItem? item, SelectionSource source)
    {
        if (ReferenceEquals(_selected, item)) return;
        _selected = item;
        OnPropertyChanged(nameof(SelectedItem));
        _state.SelectedMessageId = item?.GmMsgId;
        SelectionChanged?.Invoke(this, new SelectionChangedArgs(item, source, item?.Folder ?? _folder));
    }

    /// <summary>アプリが自動で選ぶ（既読にはしない、A58）。</summary>
    public void SelectAuto(MailListItem? item)
    {
        _autoDepth++;
        try { SelectedItem = item; }
        finally { _autoDepth--; }
    }

    /// <summary>
    /// 削除・迷惑メールへ移す・迷惑メールでない・元に戻す のあとに、次の選択を決める（Q62）。
    /// 選択中の行が消えるときは、1 つ古いメール、無ければ 1 つ新しいメール、どちらも無ければ選択なし（Auto）。
    /// 消えるのは選択中以外だけなら選択は変えない。行は一覧から外し、チェックも外す。
    /// </summary>
    public void SelectNextAfterRemoval(IEnumerable<long> removedIds)
    {
        var removed = removedIds.ToHashSet();
        var gone = Items.Where(i => i.GmMsgId is { } id && removed.Contains(id)).ToList();
        if (gone.Count == 0) return;

        MailListItem? next = _selected;
        if (_selected is not null && gone.Contains(_selected))
        {
            var idx = Items.IndexOf(_selected);
            next = Items.Skip(idx + 1).FirstOrDefault(i => !gone.Contains(i))
                   ?? Items.Take(idx).LastOrDefault(i => !gone.Contains(i));
        }

        // 先に次の行を選んでから消す（選択中の行を消すと、ListView が一瞬「選択なし」を通知してしまうため）
        SelectAuto(next);
        var checksChanged = false;
        _autoDepth++;
        try
        {
            foreach (var g in gone)
            {
                g.CheckChanged = null;
                checksChanged |= _checked.Remove(g.Key);
                Items.Remove(g);
            }
            _mailLoaded = Math.Max(0, _mailLoaded - gone.Count);
        }
        finally
        {
            _autoDepth--;
        }
        if (checksChanged) RaiseChecksChanged();
        RaiseEmptyChanged();
    }

    // ---- チェック（複数選択、A59-A65） ----

    private void OnItemChecked(MailListItem item)
    {
        if (item.IsChecked) _checked.Add(item.Key);
        else _checked.Remove(item.Key);
        RaiseChecksChanged();
    }

    /// <summary>1 行のチェックを反転する（スペースキー用）。チェック欄の無い行では何もしない。</summary>
    public void ToggleCheck(MailListItem? item)
    {
        if (item is null || !item.ShowCheckbox) return;
        item.IsChecked = !item.IsChecked;
    }

    /// <summary>いま一覧にある行を先頭から最大 200 通までチェックする。</summary>
    public void SelectAllVisible()
    {
        foreach (var item in Items.Where(i => i.ShowCheckbox).Take(Constants.MaxDeleteBatch))
        {
            item.IsChecked = true;
        }
    }

    /// <summary>「すべて選ぶ」の操作: 全部チェック済みなら外す（全チェックを解除）、そうでなければ選ぶ。</summary>
    public void ToggleSelectAll()
    {
        if (SelectAllChecked) ClearChecks();
        else SelectAllVisible();
    }

    /// <summary>すべてのチェックを外す（「選択をやめる」、フォルダ・アカウント・検索の変更、削除のあと）。</summary>
    public void ClearChecks()
    {
        if (_checked.Count == 0) return;
        // 1 行ずつの通知は止め、最後に 1 回だけ知らせる
        foreach (var item in Items.Where(i => i.IsChecked).ToList())
        {
            var callback = item.CheckChanged;
            item.CheckChanged = null;
            item.IsChecked = false;
            item.CheckChanged = callback;
        }
        _checked.Clear();
        RaiseChecksChanged();
    }

    private void RaiseChecksChanged()
    {
        OnPropertyChanged(nameof(CheckedCount));
        OnPropertyChanged(nameof(HasChecked));
        OnPropertyChanged(nameof(CheckedSummary));
        OnPropertyChanged(nameof(SelectAllChecked));
        ChecksChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- 補助 ----

    private bool IsFirstSyncRunning()
        => _accountId is { } id && _sync is not null && _syncStore is not null
           && _sync.GetState(id) == SyncState.Syncing && !_syncStore.IsInitialSyncDone(id);

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ShowEmptyMessage));
        OnPropertyChanged(nameof(SelectAllChecked));
    }

    private void RaiseEmptyChanged() => OnPropertyChanged(nameof(ShowEmptyMessage));

    private void RaiseReloaded() => Reloaded?.Invoke(this, EventArgs.Empty);

    private static void DispatchToUi(Action action)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) action();
        else d.BeginInvoke(action);
    }
}
