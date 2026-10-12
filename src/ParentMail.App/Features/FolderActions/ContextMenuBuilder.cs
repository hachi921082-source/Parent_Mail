using ParentMail.Core.Models;

namespace ParentMail.App.Features.FolderActions;

/// <summary>右クリックメニューの 1 項目。</summary>
public sealed record MenuEntry(FolderActionKind Kind, string Label, bool Enabled = true);

/// <summary>
/// フォルダごとの右クリックメニューの中身を決める（folder-actions PLAN 2 章）。画面には触れない。
/// 迷惑メールではない・元のフォルダに戻す・今すぐ送る・下書きに戻すは右クリックだけにある（Q63）。
/// </summary>
public static class ContextMenuBuilder
{
    /// <param name="folder">一覧で見ているフォルダ。</param>
    /// <param name="mail">右クリックした行のメール。下書きや送信箱の行のようにメールが無い行は null。</param>
    /// <param name="isChecked">右クリックした行にチェックが入っているか。</param>
    /// <param name="checkedCount">チェックが入っている行の数。</param>
    public static IReadOnlyList<MenuEntry> Build(FolderKind folder, MailMessage? mail, bool isChecked, int checkedCount)
    {
        var deleteLabel = isChecked && checkedCount > 0 ? FolderActionText.DeleteChecked(checkedCount) : FolderActionText.Delete;
        var delete = new MenuEntry(FolderActionKind.Delete, deleteLabel);

        switch (folder)
        {
            case FolderKind.Inbox:
            {
                var hasMail = mail is not null;
                var hasSender = mail is not null && !string.IsNullOrWhiteSpace(mail.FromAddress);
                return new[]
                {
                    new MenuEntry(FolderActionKind.Reply, FolderActionText.Reply, hasMail),
                    new MenuEntry(FolderActionKind.ReplyAll, FolderActionText.ReplyAll, hasMail),
                    new MenuEntry(FolderActionKind.Forward, FolderActionText.Forward, hasMail),
                    new MenuEntry(FolderActionKind.ToggleRead, mail is { IsRead: true } ? FolderActionText.MarkUnread : FolderActionText.MarkRead, hasMail),
                    new MenuEntry(FolderActionKind.MoveToSpam, FolderActionText.MoveToSpam, hasMail),
                    delete,
                    new MenuEntry(FolderActionKind.AddToAddressBook, FolderActionText.AddToAddressBook, hasSender),
                    new MenuEntry(FolderActionKind.AddToBlockList, FolderActionText.AddToBlockList, hasSender),
                };
            }
            case FolderKind.Sent:
                return new[] { new MenuEntry(FolderActionKind.Forward, FolderActionText.Forward), delete };
            case FolderKind.Drafts:
                return new[] { new MenuEntry(FolderActionKind.Edit, FolderActionText.Edit), delete };
            case FolderKind.Spam:
                return new[] { new MenuEntry(FolderActionKind.NotSpam, FolderActionText.NotSpam), delete };
            case FolderKind.Trash:
                return new[] { new MenuEntry(FolderActionKind.Restore, FolderActionText.Restore) };
            case FolderKind.Outbox:
                return new[]
                {
                    new MenuEntry(FolderActionKind.SendNow, FolderActionText.SendNow),
                    new MenuEntry(FolderActionKind.ReturnToDrafts, FolderActionText.ReturnToDrafts),
                };
            default:
                return Array.Empty<MenuEntry>();
        }
    }
}
