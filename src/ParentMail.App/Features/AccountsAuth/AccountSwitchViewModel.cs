using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>
/// 上部バーのアカウントボタン（アカウントごとに 1 つ、最大 5 つ、選択中 / 非選択）と、非選択側だけの受信トレイ未読バッジ。
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

    /// <summary>
    /// アカウントごとのボタン（登録順、1〜5 件）。再取得のたびに作り直さず、同じ項目を更新する
    /// （作り直すと押したボタンが消えてキーボードフォーカスを失うため）。増減したときだけ追加・削除する。
    /// </summary>
    public ObservableCollection<AccountButtonItem> Items { get; } = new();

    /// <summary>ボタン 1 つの最大幅（DIP）。1〜2 件は 300、3〜5 件は 220（A86）。</summary>
    [ObservableProperty] private double _buttonMaxWidthDip = Constants.AccountButtonMaxWidthDip;

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

        // 1. いなくなったアカウントのボタンだけ外す
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var id = Items[i].AccountId;
            if (!all.Any(a => a.Id == id)) Items.RemoveAt(i);
        }

        // 2. 登録順に並べながら、既存は更新、新規は挿入
        for (var idx = 0; idx < all.Count; idx++)
        {
            var a = all[idx];
            var selected = a.Id == current?.Id;
            // 選択中のボタンには未読バッジを出さない（自分の未読は受信トレイのフォルダ表示にある）
            var unread = selected ? 0 : _mail.UnreadCount(a.Id, FolderKind.Inbox);

            var found = -1;
            for (var j = idx; j < Items.Count; j++)
            {
                if (Items[j].AccountId == a.Id) { found = j; break; }
            }

            if (found < 0)
            {
                var id = a.Id;
                Items.Insert(idx, new AccountButtonItem(id, a.DisplayName, a.DisplayName, selected, unread, () => Select(id)));
                continue;
            }
            if (found != idx) Items.Move(found, idx);
            Items[idx].Update(a.DisplayName, a.DisplayName, selected, unread);
        }

        ButtonMaxWidthDip = Items.Count <= 2 ? Constants.AccountButtonMaxWidthDip : Constants.AccountButtonMaxWidthManyDip;
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

/// <summary>アカウントボタン 1 つ分。Unread は選択中のとき 0。値が変わると通知するので、画面の項目は作り直さずに済む。</summary>
public sealed class AccountButtonItem : ObservableObject
{
    private readonly Action _select;
    private string _name;
    private string _toolTip;
    private bool _isSelected;
    private int _unread;

    public AccountButtonItem(string accountId, string name, string toolTip, bool isSelected, int unread, Action select)
    {
        AccountId = accountId;
        _name = name;
        _toolTip = toolTip;
        _isSelected = isSelected;
        _unread = unread;
        _select = select;
        // 選択中でも常に実行可（無効にすると押せなくなりフォーカスも受けない）。押しても何もしない。
        SelectCommand = new RelayCommand(() => { if (!IsSelected) _select(); });
    }

    public string AccountId { get; }
    public string Name { get => _name; private set => SetProperty(ref _name, value); }
    /// <summary>省略表示されても全文が読めるよう、表示名そのもの。</summary>
    public string ToolTip { get => _toolTip; private set => SetProperty(ref _toolTip, value); }
    public bool IsSelected
    {
        get => _isSelected;
        private set { if (SetProperty(ref _isSelected, value)) OnPropertyChanged(nameof(ShowBadge)); }
    }
    public int Unread
    {
        get => _unread;
        private set
        {
            if (!SetProperty(ref _unread, value)) return;
            OnPropertyChanged(nameof(BadgeText));
            OnPropertyChanged(nameof(ShowBadge));
        }
    }
    public string BadgeText => Unread.ToString();
    public bool ShowBadge => !IsSelected && Unread > 0;
    public IRelayCommand SelectCommand { get; }

    /// <summary>表示内容を更新する（変わった項目だけ通知する）。</summary>
    internal void Update(string name, string toolTip, bool isSelected, int unread)
    {
        Name = name;
        ToolTip = toolTip;
        IsSelected = isSelected;
        Unread = unread;
    }
}
