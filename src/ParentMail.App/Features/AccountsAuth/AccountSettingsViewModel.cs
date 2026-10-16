using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>設定窓のアカウント一覧の 1 行（表示名・アドレスと、3 つのボタン）。</summary>
public sealed class AccountRowViewModel
{
    public AccountRowViewModel(Account account, IAsyncRelayCommand rename, IAsyncRelayCommand reenter, IAsyncRelayCommand delete)
    {
        Id = account.Id;
        DisplayName = account.DisplayName;
        Email = account.Email;
        RenameCommand = rename;
        ReenterPasswordCommand = reenter;
        DeleteCommand = delete;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Email { get; }
    public IAsyncRelayCommand RenameCommand { get; }
    public IAsyncRelayCommand ReenterPasswordCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
}

/// <summary>
/// 設定窓のアカウント欄（accounts-auth PLAN-2）。名前変更は確認なしで直接、追加・削除・入れ直しは注意ダイアログを通す。
/// 削除は送信待ちが残っていれば止め、そうでなければ注意 → 最終確認 → 完全削除の順に進む。
/// </summary>
public sealed partial class AccountSettingsViewModel : ObservableObject
{
    private readonly AccountService _service;
    private readonly IAccountRepository _accounts;
    private readonly WarningGate _gate;
    private readonly IDialogService _dialog;
    private readonly IAccountDialogs _dialogs;
    private readonly Action<Action> _dispatch;

    public AccountSettingsViewModel(AccountService service, IAccountRepository accounts, WarningGate gate,
        IDialogService dialog, IAccountDialogs dialogs, Action<Action>? dispatch = null)
    {
        _dispatch = dispatch ?? PostToUi;
        _service = service;
        _accounts = accounts;
        _gate = gate;
        _dialog = dialog;
        _dialogs = dialogs;
        AddCommand = new AsyncRelayCommand(AddAsync, () => _service.CanAddMore);

        // 追加の通知は ConfigureAwait(false) 後の別スレッドから来るので、一覧の更新は UI スレッドに寄せる
        _service.AccountAdded += (_, _) => _dispatch(Reload);
        _service.AccountRenamed += (_, _) => _dispatch(Reload);
        _service.AccountRemoved += (_, _) => _dispatch(Reload);
        Reload();
    }

    // UI スレッドなら直接、別スレッドなら Dispatcher に載せる。Application が無い（単体テスト）ときは同期実行
    private static void PostToUi(Action action)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) action();
        else d.BeginInvoke(action);
    }

    public string SectionTitle => Strings.ST_05;
    public string RenameLabel => Strings.AC_09;
    public string ReenterLabel => Strings.AC_10;
    public string DeleteLabel => Strings.AC_11;
    public string AddLabel => Strings.AC_12;

    public ObservableCollection<AccountRowViewModel> Accounts { get; } = new();

    public IAsyncRelayCommand AddCommand { get; }

    /// <summary>追加ボタンが灰色のとき、その横に出す「アカウントは5つまでです」。</summary>
    [ObservableProperty] private string _addNote = string.Empty;

    /// <summary>一覧を取り直す（追加・名前変更・削除のあと）。</summary>
    public void Reload()
    {
        Accounts.Clear();
        foreach (var a in _accounts.GetAll())
        {
            var id = a.Id;
            Accounts.Add(new AccountRowViewModel(a,
                new AsyncRelayCommand(() => RenameAsync(id)),
                new AsyncRelayCommand(() => ReenterAsync(id)),
                new AsyncRelayCommand(() => DeleteAsync(id))));
        }
        AddNote = _service.CanAddMore ? string.Empty : Strings.AC_04;
        AddCommand.NotifyCanExecuteChanged();
    }

    /// <summary>名前を変える: 注意ダイアログは出さず、窓を直接開く。</summary>
    public Task RenameAsync(string accountId)
    {
        if (_accounts.Get(accountId) is not { } account) return Task.CompletedTask;
        _dialogs.ShowRename(new RenameViewModel(_service, account.Id, account.DisplayName));
        return Task.CompletedTask;
    }

    /// <summary>パスワードを入れ直す: 注意ダイアログ → 入れ直し窓。</summary>
    public async Task ReenterAsync(string accountId)
    {
        if (_accounts.Get(accountId) is not { } account) return;
        if (!await _gate.ConfirmAsync()) return;
        _dialogs.ShowReenter(new ReenterPasswordViewModel(_service, account));
    }

    /// <summary>削除: 送信待ちがあれば止める → 注意ダイアログ → 最終確認 → 完全削除。</summary>
    public async Task DeleteAsync(string accountId)
    {
        if (_accounts.Get(accountId) is null) return;

        // 送信待ちが残っているときは、注意ダイアログも出さずにここで止める（Q68）
        if (_service.HasPendingOutbox(accountId))
        {
            _dialog.ShowNotice(Strings.WD_08, Strings.AC_20);
            return;
        }

        if (!await _gate.ConfirmAsync()) return;
        if (!_dialog.Confirm(Strings.WD_07, AccountStrings.DeleteConfirmText,
                AccountStrings.DeleteYes, AccountStrings.DeleteNo, safeIsDefault: true)) return;

        var result = await _service.DeleteAsync(accountId);
        if (result == DeleteResult.OutboxPending)
        {
            // 確認の間に送信待ちが増えた場合の念のための再確認
            _dialog.ShowNotice(Strings.WD_08, Strings.AC_20);
            return;
        }

        Reload();
        // 1 つも残らなければ、追加の窓へ（初回起動と同じ。注意ダイアログなし）
        if (_accounts.GetAll().Count == 0) _dialogs.ShowAdd(firstLaunch: true);
    }

    /// <summary>追加: すでにアカウントがあるときだけ注意ダイアログを通す。</summary>
    public async Task AddAsync()
    {
        if (!_service.CanAddMore) return;
        if (_accounts.GetAll().Count > 0 && !await _gate.ConfirmAsync()) return;
        _dialogs.ShowAdd(firstLaunch: false);
        Reload();
    }
}
