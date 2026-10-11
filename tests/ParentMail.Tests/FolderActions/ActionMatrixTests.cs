using ParentMail.App.Features.FolderActions;
using ParentMail.App.Resources;
using ParentMail.Core.Models;

namespace ParentMail.Tests.FolderActions;

// フォルダごとの右クリックメニューと見出しボタンの表（folder-actions PLAN 2 章）のテスト。
public sealed class ActionMatrixTests
{
    private static MailMessage Mail(bool read = false, string from = "taro@example.jp") => new()
    {
        GmailMsgId = 1, Subject = "ご案内", FromName = "山田 太郎", FromAddress = from, IsRead = read,
    };

    private static FolderActionKind[] Kinds(IReadOnlyList<MenuEntry> items) => items.Select(i => i.Kind).ToArray();

    // ---- 右クリックメニュー ----

    [Fact]
    public void Menu_Inbox_HasAllEightItems_InOrder()
    {
        var items = ContextMenuBuilder.Build(FolderKind.Inbox, Mail(), false, 0);
        Assert.Equal(new[]
        {
            FolderActionKind.Reply, FolderActionKind.ReplyAll, FolderActionKind.Forward, FolderActionKind.ToggleRead,
            FolderActionKind.MoveToSpam, FolderActionKind.Delete, FolderActionKind.AddToAddressBook, FolderActionKind.AddToBlockList,
        }, Kinds(items));
        Assert.All(items, i => Assert.True(i.Enabled));
    }

    [Fact]
    public void Menu_Inbox_ReadToggleLabelFollowsCurrentState()
    {
        Assert.Equal(Strings.FA_02.Split(" / ")[0],
            ContextMenuBuilder.Build(FolderKind.Inbox, Mail(read: false), false, 0).First(i => i.Kind == FolderActionKind.ToggleRead).Label);
        Assert.Equal(Strings.FA_02.Split(" / ")[1],
            ContextMenuBuilder.Build(FolderKind.Inbox, Mail(read: true), false, 0).First(i => i.Kind == FolderActionKind.ToggleRead).Label);
    }

    [Fact]
    public void Menu_Inbox_SenderItemsNeedAnAddress()
    {
        var items = ContextMenuBuilder.Build(FolderKind.Inbox, Mail(from: ""), false, 0);
        Assert.False(items.First(i => i.Kind == FolderActionKind.AddToAddressBook).Enabled);
        Assert.False(items.First(i => i.Kind == FolderActionKind.AddToBlockList).Enabled);
        Assert.True(items.First(i => i.Kind == FolderActionKind.Delete).Enabled);
    }

    [Theory]
    [InlineData(FolderKind.Sent, new[] { FolderActionKind.Forward, FolderActionKind.Delete })]
    [InlineData(FolderKind.Drafts, new[] { FolderActionKind.Edit, FolderActionKind.Delete })]
    [InlineData(FolderKind.Spam, new[] { FolderActionKind.NotSpam, FolderActionKind.Delete })]
    [InlineData(FolderKind.Trash, new[] { FolderActionKind.Restore })]
    [InlineData(FolderKind.Outbox, new[] { FolderActionKind.SendNow, FolderActionKind.ReturnToDrafts })]
    public void Menu_OtherFolders_MatchTheMatrix(FolderKind folder, FolderActionKind[] expected)
    {
        Assert.Equal(expected, Kinds(ContextMenuBuilder.Build(folder, null, false, 0)));
    }

    [Fact]
    public void Menu_LabelsComeFromTheApprovedStrings()
    {
        Assert.Equal(Strings.FA_08, ContextMenuBuilder.Build(FolderKind.Spam, null, false, 0)[0].Label);
        Assert.Equal(Strings.FA_09, ContextMenuBuilder.Build(FolderKind.Trash, null, false, 0)[0].Label);
        Assert.Equal(Strings.OB_03, ContextMenuBuilder.Build(FolderKind.Outbox, null, false, 0)[0].Label);
        Assert.Equal(Strings.OB_04, ContextMenuBuilder.Build(FolderKind.Outbox, null, false, 0)[1].Label);
        Assert.Equal(Strings.FA_07, ContextMenuBuilder.Build(FolderKind.Drafts, null, false, 0)[0].Label);
    }

    [Fact]
    public void Menu_DeleteOnCheckedRow_ShowsCheckedCount_OnUncheckedRowPlain()
    {
        var onChecked = ContextMenuBuilder.Build(FolderKind.Inbox, Mail(), true, 3).First(i => i.Kind == FolderActionKind.Delete);
        Assert.Equal("選んだ3通を削除", onChecked.Label);
        var onUnchecked = ContextMenuBuilder.Build(FolderKind.Inbox, Mail(), false, 3).First(i => i.Kind == FolderActionKind.Delete);
        Assert.Equal(Strings.FA_04, onUnchecked.Label);
    }

    // ---- 見出しボタン ----

    [Theory]
    [InlineData(FolderKind.Inbox, true, true, true, true)]
    [InlineData(FolderKind.Sent, false, false, true, true)]
    [InlineData(FolderKind.Spam, false, false, false, true)]
    [InlineData(FolderKind.Trash, false, false, false, false)]
    [InlineData(FolderKind.Outbox, false, false, false, false)]
    public void Header_WithSelection_MatchesTheMatrix(FolderKind folder, bool reply, bool replyAll, bool forward, bool delete)
    {
        var s = HeaderButtonState.For(folder, hasSelection: true, checkedCount: 0);
        Assert.Equal((reply, replyAll, forward, delete), (s.ReplyEnabled, s.ReplyAllEnabled, s.ForwardEnabled, s.DeleteEnabled));
        Assert.True(s.ShowReplyGroup);
        Assert.False(s.ShowEdit);
        Assert.Equal(Strings.FA_04, s.DeleteLabel);
    }

    [Fact]
    public void Header_Drafts_ShowsEditAndDeleteOnly()
    {
        var s = HeaderButtonState.For(FolderKind.Drafts, true, 0);
        Assert.False(s.ShowReplyGroup);
        Assert.True(s.ShowEdit);
        Assert.True(s.EditEnabled);
        Assert.True(s.DeleteEnabled);
    }

    [Theory]
    [InlineData(FolderKind.Inbox)]
    [InlineData(FolderKind.Sent)]
    [InlineData(FolderKind.Drafts)]
    [InlineData(FolderKind.Spam)]
    [InlineData(FolderKind.Trash)]
    [InlineData(FolderKind.Outbox)]
    public void Header_NoSelection_DisablesEverything(FolderKind folder)
    {
        var s = HeaderButtonState.For(folder, hasSelection: false, checkedCount: 0);
        Assert.False(s.ReplyEnabled || s.ReplyAllEnabled || s.ForwardEnabled || s.EditEnabled || s.DeleteEnabled);
    }

    [Theory]
    [InlineData(FolderKind.Inbox)]
    [InlineData(FolderKind.Sent)]
    [InlineData(FolderKind.Drafts)]
    [InlineData(FolderKind.Spam)]
    public void Header_WhileRowsAreChecked_OnlyDeleteIsEnabled_WithCountLabel(FolderKind folder)
    {
        var s = HeaderButtonState.For(folder, hasSelection: true, checkedCount: 5);
        Assert.False(s.ReplyEnabled || s.ReplyAllEnabled || s.ForwardEnabled || s.EditEnabled);
        Assert.True(s.DeleteEnabled);
        Assert.Equal("選んだ5通を削除", s.DeleteLabel);
    }

    [Fact]
    public void Header_ChecksEnableDeleteEvenWithoutSelection()
    {
        var s = HeaderButtonState.For(FolderKind.Inbox, hasSelection: false, checkedCount: 2);
        Assert.True(s.DeleteEnabled);
    }

    // ---- 文言の切り分け ----

    [Fact]
    public void Text_UndoNotice_SplitsTheBracketedButton()
    {
        Assert.Equal("ごみ箱に移動しました", FolderActionText.MovedToTrash(1));
        Assert.Equal("4通をごみ箱に移動しました", FolderActionText.MovedToTrash(4));
        Assert.Equal("元に戻す", FolderActionText.UndoLabel);
    }

    [Fact]
    public void Text_PartialFailure_FillsBothCounts()
    {
        Assert.Equal("5通のうち2通を移せませんでした。もう一度お試しください。", FolderActionText.PartialFailure(5, 2));
    }
}
