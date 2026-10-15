using ParentMail.App.Features.MailList;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Features.FolderActions;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.FolderActions;

/// <summary>
/// 一覧の行に対する操作（削除・元に戻す・迷惑メール・復元・既読切り替え・アドレス帳・受信拒否・編集・送信箱）を受け持つ。
/// 判断（誰に対して、何通まで、オフラインか）と画面への反映（行を消す、次のメールを選ぶ、通知・お知らせを出す）をここで行い、
/// 通信と cache.db の更新は IFolderActionService に任せる。
/// 行はまず画面から消し（楽観更新）、失敗したぶんは一覧を読み直して元に戻す。
/// 削除のあとに自動で選ばれたメールは既読にしない（A58）。選び直しは ListViewModel.SelectNextAfterRemoval が Auto 起点で行う。
/// </summary>
public sealed class FolderActionController
{
    private readonly IFolderActionService _service;
    private readonly ListViewModel _list;
    private readonly IAppState _state;
    private readonly INotifier _notifier;
    private readonly IDialogService _dialogs;
    private readonly IClock _clock;
    private readonly IMailRepository _mails;
    private readonly IAppLog _log;
    private readonly IAddressBookService? _addressBook;
    private readonly IBlockListService? _blockList;
    private readonly IDraftService? _drafts;
    private readonly IComposeWindowFactory? _compose;
    private readonly IOutboxService? _outbox;
    private readonly IReplyForwardHandler? _replyForward;

    private UndoToken? _latest;

    public FolderActionController(IFolderActionService service, ListViewModel list, IAppState state, INotifier notifier,
        IDialogService dialogs, IClock clock, IMailRepository mails, IAppLog log,
        IAddressBookService? addressBook = null, IBlockListService? blockList = null, IDraftService? drafts = null,
        IComposeWindowFactory? compose = null, IOutboxService? outbox = null, IReplyForwardHandler? replyForward = null)
    {
        _service = service;
        _list = list;
        _state = state;
        _notifier = notifier;
        _dialogs = dialogs;
        _clock = clock;
        _mails = mails;
        _log = log;
        _addressBook = addressBook;
        _blockList = blockList;
        _drafts = drafts;
        _compose = compose;
        _outbox = outbox;
        _replyForward = replyForward;
    }

    /// <summary>直近の削除の元に戻す記録。通知が出ているあいだだけ有効（新しい削除で置き換わる）。</summary>
    public UndoToken? LatestUndo => _latest;

    /// <summary>削除できるフォルダか（受信トレイ・送信済み・下書き・迷惑メール）。ごみ箱と送信箱は削除ボタンも出ない。</summary>
    public static bool CanDeleteIn(FolderKind folder) => folder is FolderKind.Inbox or FolderKind.Sent or FolderKind.Drafts or FolderKind.Spam;

    // ---- 削除 ----

    /// <summary>
    /// 削除の対象行を決める。contextRow が右クリックした行（ボタン・Delete キーなら null）。
    /// 右クリック: チェック済みの行ならチェック済みのすべて、そうでなければその行だけ（A76）。
    /// ボタン・キー: チェックがあればチェック済みのすべて（選択行は無視、A59）、無ければ選択行。
    /// </summary>
    public IReadOnlyList<MailListItem> ResolveDeleteRows(MailListItem? contextRow)
    {
        var checkedRows = _list.GetCheckedItems();
        if (contextRow is not null)
            return contextRow.IsChecked && checkedRows.Count > 0 ? checkedRows : new[] { contextRow };
        if (checkedRows.Count > 0) return checkedRows;
        return _list.SelectedItem is { } sel ? new[] { sel } : Array.Empty<MailListItem>();
    }

    public async Task DeleteAsync(MailListItem? contextRow = null)
    {
        if (_state.CurrentAccountId is not { } acc || !CanDeleteIn(_list.Folder)) return;
        var rows = ResolveDeleteRows(contextRow);
        if (rows.Count == 0) return;
        if (rows.Count > Constants.MaxDeleteBatch)
        {
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_22);
            return;
        }

        var folder = _list.Folder;
        var targets = new List<DeleteTarget>();
        var unresolved = 0;
        foreach (var r in rows.Where(r => r.GmMsgId.HasValue))
        {
            // 一覧に出た後で同期により所属が消えた行は、移せないものとして数える
            if (_service.FindUid(acc, r.GmMsgId!.Value, folder) is { } uid) targets.Add(new DeleteTarget(r.GmMsgId.Value, folder, uid));
            else unresolved++;
        }
        var draftRows = rows.Where(r => r.DraftId.HasValue && !r.GmMsgId.HasValue).ToList();
        await RunDeleteAsync(acc, targets, draftRows, unresolved).ConfigureAwait(true);
    }

    /// <summary>
    /// 呼び出し側が移動元フォルダつきの対象を渡す削除（検索結果など、行ごとにフォルダが違う場合）。
    /// ごみ箱にあるものは対象外になり、一覧からも消さない。
    /// </summary>
    public Task DeleteTargetsAsync(IReadOnlyList<DeleteTarget> targets)
    {
        if (_state.CurrentAccountId is not { } acc || targets.Count == 0) return Task.CompletedTask;
        if (targets.Count > Constants.MaxDeleteBatch)
        {
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_22);
            return Task.CompletedTask;
        }
        return RunDeleteAsync(acc, targets, Array.Empty<MailListItem>(), 0);
    }

    private async Task RunDeleteAsync(string acc, IReadOnlyList<DeleteTarget> targets, IReadOnlyList<MailListItem> draftRows, int unresolved)
    {
        // オフラインでは何も変えない（ローカル下書きだけの削除は通信しないので続けられる）
        if (targets.Count > 0 && _service.IsOffline(acc))
        {
            ShowOffline();
            return;
        }

        foreach (var d in draftRows)
        {
            try { await _service.DeleteLocalDraftAsync(d.DraftId!.Value).ConfigureAwait(true); }
            catch (Exception ex) { _log.Error("FolderActions", "local draft delete failed", ex); }
        }

        // 楽観更新: 先に行を消して、1 つ古いメールを自動選択する
        var removable = targets.Where(t => t.FromFolder != FolderKind.Trash).Select(t => t.GmMsgId).ToList();
        _list.SelectNextAfterRemoval(removable);
        if (draftRows.Count > 0) _list.Refresh();
        if (targets.Count == 0 && unresolved == 0) return;

        DeleteResult result;
        try
        {
            result = await _service.DeleteAsync(acc, targets).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error("FolderActions", "delete failed", ex);
            _list.Refresh();
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_06);
            return;
        }

        var moved = result.Move.Moved.Count;
        var failed = result.Move.Failed + unresolved;
        if (moved == 0 && failed == 0) return; // ごみ箱にあるものだけだった

        if (moved == 0 && result.Move.Status == MoveStatus.Offline)
        {
            _list.Refresh();
            ShowOffline();
            return;
        }

        if (failed > 0)
        {
            // 移せたものは移したまま。移せなかったものだけ一覧に戻す
            _list.Refresh();
            var total = moved + failed;
            _dialogs.ShowNotice(Strings.WD_10, total == 1 ? Strings.BN_06 : FolderActionText.PartialFailure(total, failed));
        }
        if (moved > 0 && result.Token is not null) ShowUndoNotice(result.Token, moved);
    }

    private void ShowUndoNotice(UndoToken token, int count)
    {
        _latest = token;
        _notifier.ShowNotice(FolderActionText.MovedToTrash(count), FolderActionText.UndoLabel, () => _ = UndoAsync(token),
            Constants.UndoNoticeSeconds);
    }

    // ---- 元に戻す ----

    /// <summary>最新の削除を元に戻す。通知の「元に戻す」から呼ぶ。期限が切れた記録や、古い記録は何もしない。</summary>
    public async Task UndoAsync(UndoToken token)
    {
        if (!ReferenceEquals(token, _latest)) return;
        if (new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)) > token.ExpiresUtc) return;
        if (_service.IsOffline(token.AccountId))
        {
            ShowOffline();
            return;
        }

        MoveResult result;
        try
        {
            result = await _service.UndoDeleteAsync(token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error("FolderActions", "undo failed", ex);
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_06);
            return;
        }

        if (result.Status == MoveStatus.Expired) return;
        _latest = null;
        _list.Refresh();
        if (result.Status == MoveStatus.Offline) { ShowOffline(); return; }
        if (result.Failed > 0)
        {
            var total = result.Moved.Count + result.Failed;
            _dialogs.ShowNotice(Strings.WD_10, total == 1 ? Strings.BN_06 : FolderActionText.PartialFailure(total, result.Failed));
        }
    }

    // ---- 行ごとの操作 ----

    /// <summary>メニューや見出しボタンから 1 つの操作を実行する。row は操作の対象の行。</summary>
    public async Task InvokeAsync(FolderActionKind kind, MailListItem row)
    {
        if (_state.CurrentAccountId is not { } acc) return;
        try
        {
            switch (kind)
            {
                case FolderActionKind.Delete: await DeleteAsync(row).ConfigureAwait(true); break;
                case FolderActionKind.MoveToSpam: await MoveOneAsync(acc, row, FolderKind.Spam).ConfigureAwait(true); break;
                case FolderActionKind.NotSpam: await MoveOneAsync(acc, row, FolderKind.Inbox).ConfigureAwait(true); break;
                case FolderActionKind.Restore: await MoveOneAsync(acc, row, FolderKind.Inbox).ConfigureAwait(true); break;
                case FolderActionKind.ToggleRead: await ToggleReadAsync(acc, row).ConfigureAwait(true); break;
                case FolderActionKind.Reply when row.GmMsgId is { } id && _replyForward is not null:
                    await _replyForward.ReplyAsync(acc, id).ConfigureAwait(true); break;
                case FolderActionKind.ReplyAll when row.GmMsgId is { } id && _replyForward is not null:
                    await _replyForward.ReplyAllAsync(acc, id).ConfigureAwait(true); break;
                case FolderActionKind.Forward when row.GmMsgId is { } id && _replyForward is not null:
                    await _replyForward.ForwardAsync(acc, id).ConfigureAwait(true); break;
                case FolderActionKind.AddToAddressBook: AddSender(acc, row); break;
                case FolderActionKind.AddToBlockList: BlockSender(acc, row); break;
                case FolderActionKind.Edit: await EditDraftAsync(acc, row).ConfigureAwait(true); break;
                case FolderActionKind.SendNow when row.OutboxId is { } oid && _outbox is not null:
                    await _outbox.SendNowAsync(oid).ConfigureAwait(true);
                    _list.Refresh();
                    break;
                case FolderActionKind.ReturnToDrafts when row.OutboxId is { } oid && _outbox is not null:
                    await _outbox.ReturnToDraftsAsync(oid).ConfigureAwait(true);
                    _list.Refresh();
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Error("FolderActions", "action failed: " + kind, ex);
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_06);
        }
    }

    /// <summary>
    /// 一覧の行のダブルクリック。下書きフォルダのときだけ、その行を「編集」ボタンと同じ経路で開く（チェック状態は見ない）。
    /// 他のフォルダでは何もしない。作成窓が開いている場合の扱いは EditDraftAsync の既存の判定に任せる。
    /// </summary>
    public Task DoubleClickAsync(MailListItem row)
        => _list.Folder == FolderKind.Drafts ? InvokeAsync(FolderActionKind.Edit, row) : Task.CompletedTask;

    // 迷惑メールにする・迷惑メールではない・ごみ箱から戻す（1 通、通知なし）
    private async Task MoveOneAsync(string acc, MailListItem row, FolderKind to)
    {
        if (row.GmMsgId is not { } id) return;
        var from = _list.Folder;
        if (_service.FindUid(acc, id, from) is not { } uid)
        {
            _list.Refresh();
            return;
        }
        if (_service.IsOffline(acc))
        {
            ShowOffline();
            return;
        }

        var target = new DeleteTarget(id, from, uid);
        _list.SelectNextAfterRemoval(new[] { id });
        MoveResult result;
        try
        {
            result = to == FolderKind.Spam ? await _service.MoveToSpamAsync(acc, target).ConfigureAwait(true)
                : from == FolderKind.Trash ? await _service.RestoreAsync(acc, target).ConfigureAwait(true)
                : await _service.NotSpamAsync(acc, target).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Error("FolderActions", "move failed", ex);
            _list.Refresh();
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_06);
            return;
        }

        if (result.Status == MoveStatus.Ok) return;
        _list.Refresh();
        if (result.Status == MoveStatus.Offline) ShowOffline();
        else _dialogs.ShowNotice(Strings.WD_10, Strings.BN_06);
    }

    // 既読・未読の切り替え（オフラインでも可。同期へ積まれる）
    private async Task ToggleReadAsync(string acc, MailListItem row)
    {
        if (row.GmMsgId is not { } id) return;
        await _service.ToggleReadAsync(acc, id).ConfigureAwait(true);
        _list.Refresh();
    }

    private void AddSender(string acc, MailListItem row)
    {
        if (row.GmMsgId is not { } id || _addressBook is null) return;
        var mail = _mails.Get(acc, id);
        if (mail is null || string.IsNullOrWhiteSpace(mail.FromAddress)) return;
        _addressBook.AddFromSender(acc, mail.FromName ?? "", mail.FromAddress);
    }

    // 受信拒否に追加: 「このアドレスだけ」「このドメイン全体」「やめる」から選ぶ。既存のメールは動かさない（A25）。
    private void BlockSender(string acc, MailListItem row)
    {
        if (row.GmMsgId is not { } id || _blockList is null) return;
        var mail = _mails.Get(acc, id);
        if (mail is null || string.IsNullOrWhiteSpace(mail.FromAddress)) return;

        var buttons = new[] { FolderActionText.BlockAddressOnly, FolderActionText.BlockWholeDomain, FolderActionText.Cancel };
        var choice = _dialogs.Choose(Strings.WD_16, mail.FromAddress, buttons, defaultIndex: 0, safeIndex: 2);
        if (choice is not (0 or 1)) return;

        var result = _blockList.AddFromSender(acc, mail.FromAddress, choice == 0 ? BlockKind.Address : BlockKind.Domain);
        switch (result)
        {
            case BlockAddResult.Added:
                _notifier.ShowNotice(Strings.FA_11);
                break;
            case BlockAddResult.Duplicate:
                _dialogs.ShowNotice(Strings.WD_16, Strings.BL_05);
                break;
            default:
                _dialogs.ShowNotice(Strings.WD_16, Strings.BL_07);
                break;
        }
    }

    // 下書きを作成窓で開く（ローカル下書き行はローカルの行から、Gmail の下書きは所属から）
    // 作成窓が既に開いていれば前面に出すだけ（下書きの取得もしない）。取得中の続けての要求も受け付けない。
    private bool _editingDraft;

    private async Task EditDraftAsync(string acc, MailListItem row)
    {
        if (_drafts is null || _compose is null) return;
        if (_editingDraft || _compose.TryActivateExisting()) return;
        _editingDraft = true;
        try
        {
            ComposeInit? init = null;
            if (row.DraftId is { } did && !row.GmMsgId.HasValue) init = await _drafts.OpenForEditAsync(did).ConfigureAwait(true);
            else if (row.GmMsgId is { } id) init = await _drafts.OpenForEditByMessageAsync(acc, id).ConfigureAwait(true);
            if (init is not null) _compose.Open(init);
        }
        finally { _editingDraft = false; }
    }

    private void ShowOffline() => _dialogs.ShowNotice(Strings.WD_10, Strings.BN_05);
}
