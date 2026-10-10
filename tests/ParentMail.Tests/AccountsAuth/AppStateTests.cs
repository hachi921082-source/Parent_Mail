using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;

namespace ParentMail.Tests.AccountsAuth;

// 実 IAppState（現在のアカウント・フォルダ・選択メール）のテスト。
public sealed class AppStateTests
{
    [Fact]
    public void Defaults_NoAccount_Inbox_NoSelection()
    {
        var s = new AppState();
        Assert.Null(s.CurrentAccountId);
        Assert.Equal(FolderKind.Inbox, s.CurrentFolder);
        Assert.Null(s.SelectedMessageId);
    }

    [Fact]
    public void Setters_RaiseEvent_OnlyWhenValueChanges()
    {
        var s = new AppState();
        var acc = 0; var folder = 0; var sel = 0;
        s.CurrentAccountChanged += (_, _) => acc++;
        s.CurrentFolderChanged += (_, _) => folder++;
        s.SelectedMessageChanged += (_, _) => sel++;

        s.CurrentAccountId = "a1";
        s.CurrentAccountId = "a1";
        s.CurrentFolder = FolderKind.Trash;
        s.CurrentFolder = FolderKind.Trash;
        s.SelectedMessageId = 5;
        s.SelectedMessageId = 5;

        Assert.Equal((1, 1, 1), (acc, folder, sel));
    }

    [Fact]
    public void AccountChange_ResetsFolderToInbox_ClearsSelection_AndRaisesAfterAccount()
    {
        var s = new AppState { CurrentAccountId = "a1" };
        s.CurrentFolder = FolderKind.Sent;
        s.SelectedMessageId = 9;
        var order = new List<string>();
        s.CurrentAccountChanged += (_, _) => order.Add("account:" + s.CurrentFolder + ":" + s.SelectedMessageId);
        s.CurrentFolderChanged += (_, _) => order.Add("folder");
        s.SelectedMessageChanged += (_, _) => order.Add("selected");

        s.CurrentAccountId = "a2";

        Assert.Equal(FolderKind.Inbox, s.CurrentFolder);
        Assert.Null(s.SelectedMessageId);
        // 先に状態をそろえてからアカウント変更を通知し、続けてフォルダ・選択の変更を通知する
        Assert.Equal(new[] { "account:Inbox:", "folder", "selected" }, order);
    }

    [Fact]
    public void AccountChange_WhenAlreadyInboxAndNoSelection_RaisesOnlyAccountEvent()
    {
        var s = new AppState();
        var other = 0;
        s.CurrentFolderChanged += (_, _) => other++;
        s.SelectedMessageChanged += (_, _) => other++;
        s.CurrentAccountId = "a1";
        Assert.Equal(0, other);
    }
}
