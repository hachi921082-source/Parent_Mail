namespace ParentMail.App.Features.FolderActions;

/// <summary>右クリックメニューと見出しボタンから呼ぶ操作の種類。</summary>
public enum FolderActionKind
{
    Reply,
    ReplyAll,
    Forward,
    ToggleRead,
    MoveToSpam,
    Delete,
    AddToAddressBook,
    AddToBlockList,
    /// <summary>下書きを作成窓で開く。</summary>
    Edit,
    NotSpam,
    /// <summary>ごみ箱から受信トレイへ戻す。</summary>
    Restore,
    /// <summary>送信箱のメールをすぐ送る。</summary>
    SendNow,
    /// <summary>送信箱のメールを下書きに戻す。</summary>
    ReturnToDrafts,
}
