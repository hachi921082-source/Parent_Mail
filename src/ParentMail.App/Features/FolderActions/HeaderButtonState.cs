using ParentMail.Core.Models;

namespace ParentMail.App.Features.FolderActions;

/// <summary>
/// プレビュー見出しのボタン（返信・全員に返信・転送／編集・削除）の有効・無効と削除ボタンの文言。
/// 何も選んでいなければすべて無効（A3）。チェックが入っているあいだは削除だけ有効で、文言は「選んだN通を削除」。
/// </summary>
public sealed record HeaderButtonState(
    bool ShowReplyGroup,
    bool ReplyEnabled,
    bool ReplyAllEnabled,
    bool ForwardEnabled,
    bool ShowEdit,
    bool EditEnabled,
    bool DeleteEnabled,
    string DeleteLabel)
{
    /// <param name="folder">一覧で見ているフォルダ。</param>
    /// <param name="hasSelection">選ばれた行があるか（メール・ローカル下書き・送信箱の行のどれでも）。</param>
    /// <param name="checkedCount">チェックが入っている行の数。</param>
    public static HeaderButtonState For(FolderKind folder, bool hasSelection, int checkedCount)
    {
        var showReply = folder != FolderKind.Drafts;
        var showEdit = folder == FolderKind.Drafts;

        if (checkedCount > 0)
        {
            // 複数選択中は削除だけ（チェック欄の無いごみ箱・送信箱では起こらない）
            return new HeaderButtonState(showReply, false, false, false, showEdit, false, true, FolderActionText.DeleteChecked(checkedCount));
        }

        var on = hasSelection;
        return folder switch
        {
            FolderKind.Inbox => new HeaderButtonState(true, on, on, on, false, false, on, FolderActionText.Delete),
            FolderKind.Sent => new HeaderButtonState(true, false, false, on, false, false, on, FolderActionText.Delete),
            FolderKind.Drafts => new HeaderButtonState(false, false, false, false, true, on, on, FolderActionText.Delete),
            FolderKind.Spam => new HeaderButtonState(true, false, false, false, false, false, on, FolderActionText.Delete),
            _ => new HeaderButtonState(true, false, false, false, false, false, false, FolderActionText.Delete),
        };
    }
}
