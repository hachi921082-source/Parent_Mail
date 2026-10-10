using ParentMail.Core.Models;

namespace ParentMail.App.Features.MailList;

/// <summary>選択の起点。User = 利用者の操作（クリック・上下キー・Enter）、Auto = アプリが自動で選んだ（削除後・再読み込み後）。</summary>
public enum SelectionSource { User, Auto }

/// <summary>
/// 一覧の選択が変わったときの通知。IAppState の SelectedMessageChanged には起点を載せられないので、
/// 起点つきのこの通知を ListViewModel.SelectionChanged で出す（mail-display が購読する）。
/// </summary>
public sealed class SelectionChangedArgs : EventArgs
{
    public SelectionChangedArgs(MailListItem? item, SelectionSource source, FolderKind folder)
    {
        Item = item;
        Source = source;
        Folder = folder;
    }

    public MailListItem? Item { get; }
    public SelectionSource Source { get; }
    public FolderKind Folder { get; }

    /// <summary>既読にしてよい選択か（利用者が選んだ、かつ受信トレイか迷惑メール、Q69/Q74/A58）。</summary>
    public bool ShouldMarkRead => Item is { GmMsgId: not null } && Source == SelectionSource.User
                                  && Folder is FolderKind.Inbox or FolderKind.Spam;
}
