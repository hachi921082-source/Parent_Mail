using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.Core;

namespace ParentMail.App.Services;

/// <summary>画面に出す帯 1 件の表示用データ。</summary>
public sealed class BannerViewModel
{
    public BannerViewModel(BannerEntry entry, Action<BannerKind> close)
    {
        Kind = entry.Kind;
        Text = entry.Text;
        CanClose = entry.CanClose;
        HasAction = entry.Action is not null;
        ActionLabel = entry.Action?.Label ?? string.Empty;
        ActionCommand = new RelayCommand(() => entry.Action?.Action());
        CloseCommand = new RelayCommand(() => close(entry.Kind));
    }

    public BannerKind Kind { get; }
    public string Text { get; }
    public bool CanClose { get; }
    public bool HasAction { get; }
    public string ActionLabel { get; }
    public IRelayCommand ActionCommand { get; }
    public IRelayCommand CloseCommand { get; }
}

/// <summary>帯と一時通知のホスト（INotifier）。どのスレッドから呼んでも UI スレッドで更新する。</summary>
public sealed partial class NotifierService : ObservableObject, INotifier
{
    private readonly BannerHost _host = new();
    private readonly DispatcherTimer _noticeTimer = new();
    private Action? _noticeAction;

    [ObservableProperty] private bool _isNoticeVisible;
    [ObservableProperty] private string _noticeText = string.Empty;
    [ObservableProperty] private string? _noticeActionLabel;

    public NotifierService()
    {
        _host.Changed += (_, _) => RunOnUi(RefreshBanners);
        _noticeTimer.Tick += (_, _) => HideNotice();
    }

    public ObservableCollection<BannerViewModel> Banners { get; } = new();

    public bool HasNoticeAction => NoticeActionLabel is not null;

    public IRelayCommand NoticeActionCommand => _noticeActionCommand ??= new RelayCommand(() =>
    {
        var action = _noticeAction;
        HideNotice();
        action?.Invoke();
    });
    private IRelayCommand? _noticeActionCommand;

    public void ShowBanner(BannerKind kind, string text, (string Label, Action Action)? action = null)
        => _host.Show(kind, text, action);

    public void HideBanner(BannerKind kind) => _host.Hide(kind);

    public void ShowNotice(string text, string? actionLabel = null, Action? action = null,
        int seconds = Constants.TransientNoticeSeconds)
        => RunOnUi(() =>
        {
            _noticeAction = action;
            NoticeText = text;
            NoticeActionLabel = actionLabel;
            OnPropertyChanged(nameof(HasNoticeAction));
            IsNoticeVisible = true;
            _noticeTimer.Stop();
            _noticeTimer.Interval = TimeSpan.FromSeconds(seconds);
            _noticeTimer.Start();
        });

    private void HideNotice()
    {
        _noticeTimer.Stop();
        _noticeAction = null;
        IsNoticeVisible = false;
    }

    private void RefreshBanners()
    {
        Banners.Clear();
        foreach (var entry in _host.Visible)
        {
            Banners.Add(new BannerViewModel(entry, kind => _host.Dismiss(kind)));
        }
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
