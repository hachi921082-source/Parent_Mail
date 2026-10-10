using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>
/// 上部バーのアカウント切替ボタン（現在の表示名）と、もう一方のアカウントの受信トレイ未読バッジ。
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
        SwitchCommand = new RelayCommand(Switch, () => FindOther() is not null);

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

    /// <summary>切替ボタンの文字 = 現在のアカウントの表示名だけ。</summary>
    [ObservableProperty] private string _label = string.Empty;

    [NotifyPropertyChangedFor(nameof(HasBadge), nameof(BadgeText))]
    [ObservableProperty] private int _otherUnread;

    public bool HasBadge => OtherUnread > 0;
    public string BadgeText => OtherUnread > 0 ? OtherUnread.ToString() : string.Empty;

    /// <summary>もう一方のアカウントへ切り替える（1 つしか無いときは何もしない）。</summary>
    public IRelayCommand SwitchCommand { get; }

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

        Label = current?.DisplayName ?? string.Empty;
        var other = all.FirstOrDefault(a => a.Id != current?.Id);
        OtherUnread = other is null ? 0 : _mail.UnreadCount(other.Id, FolderKind.Inbox);
        SwitchCommand.NotifyCanExecuteChanged();
    }

    private Account? FindOther()
    {
        var all = _accounts.GetAll();
        return all.Count < 2 ? null : all.FirstOrDefault(a => a.Id != _state.CurrentAccountId);
    }

    // 切替: IAppState が受信トレイへ戻し・選択解除を行う。ズームと検索のリセットは各機能が CurrentAccountChanged で行う。
    private void Switch()
    {
        var other = FindOther();
        if (other is not null) _state.CurrentAccountId = other.Id;
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
