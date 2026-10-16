using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>
/// 上部バーのアカウントボタン（アカウントごとに 1 つ、選択中 / 非選択）と、非選択側だけの受信トレイ未読バッジ。
/// 現在のアカウントは IAppState.CurrentAccountId に持ち、最後に使った ID を設定に保存する。
/// </summary>
public sealed partial class AccountSwitchViewModel : ObservableObject
{
    private readonly IAccountRepository _accounts;
    private readonly IAppState _state;
    private readonly IMailRepository _mail;
    private readonly ISettingsStore _settings;
    private readonly SynchronizationContext? _ui;

    public AccountSwitchViewModel(IAccountRepository accounts, IAppState state, IMailRepository mail,
        ISettingsStore settings, ISyncService? sync = null)
    {
        _accounts = accounts;
        _state = state;
        _mail = mail;
        _settings = settings;
        _ui = SynchronizationContext.Current;

        _state.CurrentAccountChanged += (_, _) => Post(OnCurrentChanged);
        if (sync is not null)
        {
            // 同期はバックグラウンドで終わるので、画面の更新は UI スレッドへ回す
            sync.SyncCompleted += (_, e) => Post(() =>
            {
                if (e.AccountId != _state.CurrentAccountId) Refresh();
            });
        }
    }

    /// <summary>アカウントごとのボタン（登録順、1〜2 件）。変わるたびに作り直す。</summary>
    [ObservableProperty] private IReadOnlyList<AccountButtonItem> _items = Array.Empty<AccountButtonItem>();

    /// <summary>起動時: 前回のアカウント（無ければ先頭）を現在にする。</summary>
    public void Initialize() => Refresh();

    /// <summary>アカウントの追加・名前変更・削除や同期完了のあとに、表示を取り直す。</summary>
    public void Refresh()
    {
        var all = _accounts.GetAll();
        var current = all.FirstOrDefault(a => a.Id == _state.CurrentAccountId);
        if (current is null && all.Count > 0)
        {
            var last = _settings.Get().LastAccountId;
            current = all.FirstOrDefault(a => a.Id == last) ?? all[0];
            _state.CurrentAccountId = current.Id; // OnCurrentChanged が再入しても結果は同じ
        }
        else if (current is null && _state.CurrentAccountId is not null)
        {
            _state.CurrentAccountId = null;
        }

        Items = all.Select(a =>
        {
            var selected = a.Id == current?.Id;
            // 選択中のボタンには未読バッジを出さない（自分の未読は受信トレイのフォルダ表示にある）
            var unread = selected ? 0 : _mail.UnreadCount(a.Id, FolderKind.Inbox);
            return new AccountButtonItem(a.Id, a.DisplayName, a.DisplayName, selected, unread, () => Select(a.Id));
        }).ToList();
    }

    // 切替: IAppState が受信トレイへ戻し・選択解除を行う。ズームと検索のリセットは各機能が CurrentAccountChanged で行う。
    // 選択中のボタンを押しても何もしない。
    private void Select(string accountId)
    {
        if (accountId != _state.CurrentAccountId) _state.CurrentAccountId = accountId;
    }

    private void OnCurrentChanged()
    {
        if (_state.CurrentAccountId is { } id && _settings.Get().LastAccountId != id)
        {
            _settings.Save(_settings.Get() with { LastAccountId = id });
        }
        Refresh();
    }

    private void Post(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui) action();
        else _ui.Post(_ => action(), null);
    }
}

/// <summary>アカウントボタン 1 つ分。Unread は選択中のとき 0。</summary>
public sealed class AccountButtonItem
{
    private readonly Action _select;

    public AccountButtonItem(string accountId, string name, string toolTip, bool isSelected, int unread, Action select)
    {
        AccountId = accountId;
        Name = name;
        ToolTip = toolTip;
        IsSelected = isSelected;
        Unread = unread;
        _select = select;
        // 選択中でも常に実行可（無効にすると押せなくなりフォーカスも受けない）。押しても何もしない。
        SelectCommand = new RelayCommand(() => { if (!IsSelected) _select(); });
    }

    public string AccountId { get; }
    public string Name { get; }
    /// <summary>省略表示されても全文が読めるよう、表示名そのもの。</summary>
    public string ToolTip { get; }
    public bool IsSelected { get; }
    public int Unread { get; }
    public string BadgeText => Unread.ToString();
    public bool ShowBadge => !IsSelected && Unread > 0;
    public IRelayCommand SelectCommand { get; }
}
