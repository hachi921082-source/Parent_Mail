namespace ParentMail.App.Features.FolderActions;

/// <summary>
/// 返信・全員に返信・転送を始める入口。実装は reply-forward 機能が登録する（それまでは未登録で、押しても何も起きない）。
/// folder-actions は見出しボタンと右クリックメニューから呼ぶだけで、作成窓の初期値づくりには関わらない。
/// </summary>
public interface IReplyForwardHandler
{
    Task ReplyAsync(string accountId, long gmailMsgId);
    Task ReplyAllAsync(string accountId, long gmailMsgId);
    Task ForwardAsync(string accountId, long gmailMsgId);
}
