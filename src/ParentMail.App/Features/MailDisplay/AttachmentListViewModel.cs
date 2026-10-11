using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.Core;

namespace ParentMail.App.Features.MailDisplay;

/// <summary>
/// プレビュー本文の下の添付一覧（mail-display PLAN-3）。選ばれたメールの添付（埋め込み画像を除く）を並べ、
/// 各行の「開く」「保存」を AttachmentActions につなぐ。
/// </summary>
public sealed class AttachmentListViewModel : ObservableObject
{
    private readonly IMailRepository _mails;
    private readonly AttachmentActions _actions;
    private readonly ITextScale _scale;

    private string? _accountId;
    private long _gmailMsgId;

    public AttachmentListViewModel(IMailRepository mails, AttachmentActions actions, ITextScale scale)
    {
        _mails = mails;
        _actions = actions;
        _scale = scale;
        OpenCommand = new AsyncRelayCommand<AttachmentItem>(OpenAsync, item => item is { CanOpen: true });
        SaveCommand = new AsyncRelayCommand<AttachmentItem>(SaveAsync, item => item is { CanSave: true });
        _scale.Changed += (_, _) => OnPropertyChanged(nameof(FontSize));
    }

    public ObservableCollection<AttachmentItem> Items { get; } = new();

    public bool HasItems => Items.Count > 0;

    /// <summary>行の文字の大きさ（設定の文字サイズに合わせる）。</summary>
    public double FontSize => _scale.FontSizeDip;

    public IAsyncRelayCommand<AttachmentItem> OpenCommand { get; }
    public IAsyncRelayCommand<AttachmentItem> SaveCommand { get; }

    /// <summary>指定のメールの添付を読み込む。同じ一覧なら作り直さない（処理中の行を保つため）。</summary>
    public void Load(string accountId, long gmailMsgId)
    {
        var infos = _mails.GetAttachments(accountId, gmailMsgId).Where(a => !a.IsInline).ToList();
        if (accountId == _accountId && gmailMsgId == _gmailMsgId && infos.Select(i => i.Id).SequenceEqual(Items.Select(i => i.Id)))
            return;

        _accountId = accountId;
        _gmailMsgId = gmailMsgId;
        Items.Clear();
        foreach (var info in infos) Items.Add(new AttachmentItem(info));
        OnPropertyChanged(nameof(HasItems));
    }

    /// <summary>一覧を空にする（メールを選んでいないとき）。</summary>
    public void Clear()
    {
        if (_accountId is null && Items.Count == 0) return;
        _accountId = null;
        _gmailMsgId = 0;
        Items.Clear();
        OnPropertyChanged(nameof(HasItems));
    }

    private async Task OpenAsync(AttachmentItem? item)
    {
        var account = _accountId;
        if (item is null || account is null) return;
        await RunAsync(item, () => _actions.OpenAsync(account, item));
    }

    private async Task SaveAsync(AttachmentItem? item)
    {
        var account = _accountId;
        if (item is null || account is null) return;
        await RunAsync(item, () => _actions.SaveAsync(account, item));
    }

    // 行の状態（処理中・危険）が変わるたびに、ボタンの押せる状態を更新する
    private async Task RunAsync(AttachmentItem item, Func<Task> action)
    {
        void OnChanged(object? s, PropertyChangedEventArgs e)
        {
            OpenCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }
        item.PropertyChanged += OnChanged;
        try { await action(); }
        finally
        {
            item.PropertyChanged -= OnChanged;
            OpenCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
