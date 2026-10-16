using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.App.Resources;
using ParentMail.App.Services;
using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.Shell;

/// <summary>上部バーの 1 つのフォルダボタン。</summary>
public sealed partial class FolderItemViewModel : ObservableObject
{
    public FolderItemViewModel(FolderKind kind, string label, bool showsWhiteBadgeWhenActive)
    {
        Kind = kind;
        Label = label;
        ShowsWhiteBadgeWhenActive = showsWhiteBadgeWhenActive;
    }

    public FolderKind Kind { get; }
    public string Label { get; }
    /// <summary>受信トレイの件数は、選択中（青）のとき白地に赤文字で表示する。</summary>
    public bool ShowsWhiteBadgeWhenActive { get; }

    [ObservableProperty] private bool _isActive;
    [NotifyPropertyChangedFor(nameof(HasBadge))]
    [ObservableProperty] private string _badgeText = string.Empty;

    public bool HasBadge => BadgeText.Length > 0;
    public bool UseWhiteBadge => IsActive && ShowsWhiteBadgeWhenActive;

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(UseWhiteBadge));
}

/// <summary>メイン窓（帯・上部バー）の表示データ。一覧・プレビューは各機能のビューが担当する。</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ITextScale _scale;
    private readonly IAppState? _state;

    public MainViewModel(ITextScale scale, NotifierService notifier, IAppState? state = null)
    {
        _scale = scale;
        _state = state;
        Notifier = notifier;

        Folders = new ObservableCollection<FolderItemViewModel>
        {
            new(FolderKind.Inbox, Strings.TB_01, showsWhiteBadgeWhenActive: true),
            new(FolderKind.Outbox, Strings.TB_02, showsWhiteBadgeWhenActive: false),
            new(FolderKind.Sent, Strings.TB_03, false),
            new(FolderKind.Trash, Strings.TB_04, false),
            new(FolderKind.Drafts, Strings.TB_05, false),
            new(FolderKind.Spam, Strings.TB_06, false),
        };
        SelectFolderCommand = new RelayCommand<FolderItemViewModel>(SelectFolder);
        RefreshCommand = new RelayCommand(() => RefreshRequested?.Invoke(this, EventArgs.Empty), () => !IsRefreshing);

        _scale.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(AppFontSize));
            OnPropertyChanged(nameof(ChromeFontSize));
        };
        if (_state is not null)
        {
            _state.CurrentFolderChanged += (_, _) => UpdateActiveFolder(_state.CurrentFolder);
            UpdateActiveFolder(_state.CurrentFolder);
        }
        else
        {
            UpdateActiveFolder(FolderKind.Inbox);
        }
    }

    public NotifierService Notifier { get; }
    public ObservableCollection<FolderItemViewModel> Folders { get; }

    /// <summary>通常 UI の文字サイズ (DIP)。</summary>
    public double AppFontSize => _scale.FontSizeDip;
    /// <summary>上部バーの文字サイズ (DIP)。20 pt で頭打ち。</summary>
    public double ChromeFontSize => _scale.ChromeFontSizeDip;

    /// <summary>上段左のアカウントボタン群（accounts-auth が AccountButtonItem の一覧を設定する）。</summary>
    [ObservableProperty] private System.Collections.IEnumerable? _accountButtons;
    [ObservableProperty] private bool _htmlMode;
    [NotifyPropertyChangedFor(nameof(RefreshLabel))]
    [ObservableProperty] private bool _isRefreshing;

    public string RefreshLabel => IsRefreshing ? Strings.TB_13 : Strings.TB_12;

    public IRelayCommand<FolderItemViewModel> SelectFolderCommand { get; }
    public IRelayCommand RefreshCommand { get; }

    public event EventHandler? RefreshRequested;

    partial void OnIsRefreshingChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();

    /// <summary>受信トレイの未読数と送信トレイの失敗数を設定する。0 のときバッジは消える。</summary>
    public void SetCounts(int inboxUnread, int outboxFailed)
    {
        Folders[0].BadgeText = inboxUnread > 0 ? inboxUnread.ToString() : string.Empty;
        Folders[1].BadgeText = outboxFailed > 0 ? "!" + outboxFailed : string.Empty;
    }

    private void SelectFolder(FolderItemViewModel? item)
    {
        if (item is null) return;
        if (_state is not null) _state.CurrentFolder = item.Kind;
        else UpdateActiveFolder(item.Kind);
    }

    private void UpdateActiveFolder(FolderKind kind)
    {
        foreach (var f in Folders) f.IsActive = f.Kind == kind;
    }
}
