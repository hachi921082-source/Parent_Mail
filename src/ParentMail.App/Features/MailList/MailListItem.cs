using CommunityToolkit.Mvvm.ComponentModel;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.MailList;

/// <summary>
/// 一覧の 1 行。キャッシュのメール、送信箱の 1 通、ローカル下書きのいずれか。
/// 同期の再読み込みでは同じインスタンスを更新して使うので、選択とスクロール位置が保たれる。
/// </summary>
public sealed partial class MailListItem : ObservableObject
{
    public MailListItem(string key)
    {
        Key = key;
    }

    /// <summary>行を一意に表すキー（m:メールID / o:送信箱ID / d:下書きID）。チェック状態の管理に使う。</summary>
    public string Key { get; }

    public long? GmMsgId { get; init; }
    public long? OutboxId { get; init; }
    /// <summary>まだキャッシュの一覧に無いローカル下書き（下書きフォルダのみ）。</summary>
    public long? DraftId { get; init; }
    /// <summary>並べ替えと日付表示の元（INTERNALDATE、A68）。</summary>
    public DateTime SortUtc { get; init; }
    /// <summary>横断表示（検索結果）用のフォルダ名。通常の一覧では null。</summary>
    public string? FolderLabel { get; init; }
    /// <summary>横断表示（検索結果）で、この行を代表するフォルダ。通常の一覧では null（一覧のフォルダに従う）。</summary>
    public FolderKind? Folder { get; init; }
    /// <summary>チェック欄を出すか（ゴミ箱と送信箱は出さない、A62）。</summary>
    public bool ShowCheckbox { get; init; }

    [ObservableProperty] private string _sender = string.Empty;
    [ObservableProperty] private string _subject = string.Empty;
    [ObservableProperty] private string _dateText = string.Empty;
    [ObservableProperty] private bool _isUnread;
    [ObservableProperty] private bool _hasAttachment;
    /// <summary>送信箱で、送信に失敗した行（赤い印）。</summary>
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private bool _isChecked;

    /// <summary>チェックが変わったときに ListViewModel へ知らせる。</summary>
    internal Action<MailListItem>? CheckChanged { get; set; }

    partial void OnIsCheckedChanged(bool value) => CheckChanged?.Invoke(this);

    /// <summary>再読み込みで、表示内容だけを新しい行の値に合わせる（チェックは触らない）。</summary>
    internal void UpdateFrom(MailListItem other)
    {
        Sender = other.Sender;
        Subject = other.Subject;
        DateText = other.DateText;
        IsUnread = other.IsUnread;
        HasAttachment = other.HasAttachment;
        IsFailed = other.IsFailed;
    }
}
