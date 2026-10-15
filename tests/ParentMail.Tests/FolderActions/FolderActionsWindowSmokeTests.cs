using System.Windows;
using System.Windows.Controls;
using NSubstitute;
using ParentMail.App.Features.FolderActions;
using ParentMail.App.Features.MailList;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.FolderActions;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.FolderActions;

// 見出しボタンと右クリックメニューを実際に作り、XAML の参照ミス・ボタンの並び・メニューの大きさを確かめる（画面には出すが一瞬で閉じる）。
[Collection("Wpf")]
public class FolderActionsWindowSmokeTests
{
    private const string Acc = "acc1";

    private static void RunSta(Action body)
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try { body(); } catch (Exception e) { error = e; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error is not null) throw new Exception("STA スレッドで失敗: " + error, error);
    }

    private static void EnsureApp()
    {
        if (Application.Current is null)
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Parent_Mail;component/Resources/Palette.xaml"),
            });
        }
    }

    private sealed class Setup
    {
        public readonly InMemoryMailRepository Mail = new();
        public readonly FakeAppState State = new() { CurrentAccountId = Acc };
        public readonly FakeClock Clock = new();
        public readonly ListViewModel List;
        public readonly FolderActionController Controller;

        public Setup(FolderKind folder, int count)
        {
            List = new ListViewModel(Mail, new InMemoryOutboxRepository(), new InMemoryDraftRepository(), State, Clock, null, null, null, null, a => a());
            for (var i = 1; i <= count; i++)
            {
                Mail.Upsert(Acc, new MailMessage
                {
                    GmailMsgId = i, Subject = "お知らせ" + i, FromName = "鈴木 一郎", FromAddress = "ichiro@example.jp",
                    DateUtc = Clock.UtcNow.AddMinutes(-i),
                }, new[] { new MessageFolder(i, folder, (uint)i, 1) });
            }
            State.CurrentFolder = folder;
            List.LoadFolder(folder);
            Controller = new FolderActionController(Substitute.For<IFolderActionService>(), List, State, Substitute.For<INotifier>(),
                new FakeDialogService(), Clock, Mail, new FakeAppLog());
        }
    }

    [Fact]
    public void HeaderButtons_InboxWithSelection_ReplyGroupAndDeleteAreEnabled_DeleteIsRedOutline()
    {
        RunSta(() =>
        {
            EnsureApp();
            var s = new Setup(FolderKind.Inbox, 2);
            var vm = new HeaderActionsViewModel(s.Controller, s.List);
            var left = new HeaderActionsView(vm);
            var right = new DeleteActionView(vm);
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(left);
            panel.Children.Add(right);
            var host = new Window { Content = panel, Width = 900, Height = 200, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Show();
            try
            {
                host.UpdateLayout();
                Assert.False(((Button)left.FindName("ReplyButton")!).IsEnabled); // 未選択
                s.List.SelectedItem = s.List.Items[0];
                Assert.True(((Button)left.FindName("ReplyButton")!).IsEnabled);
                Assert.True(((Button)left.FindName("ReplyAllButton")!).IsEnabled);
                Assert.True(((Button)left.FindName("ForwardButton")!).IsEnabled);
                Assert.Equal(Visibility.Collapsed, ((Button)left.FindName("EditButton")!).Visibility);

                var del = (Button)right.FindName("DeleteButton")!;
                Assert.True(del.IsEnabled);
                Assert.Equal("削除", del.Content);
                Assert.Same(Application.Current.Resources["DeleteOutlineButton"], del.Style);
                Assert.True(del.ActualHeight >= 56);
            }
            finally { host.Close(); }
        });
    }

    [Fact]
    public void HeaderButtons_WhileRowsChecked_DeleteSaysCount_AndOthersAreDisabled()
    {
        RunSta(() =>
        {
            EnsureApp();
            var s = new Setup(FolderKind.Inbox, 4);
            var vm = new HeaderActionsViewModel(s.Controller, s.List);
            var left = new HeaderActionsView(vm);
            var right = new DeleteActionView(vm);
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(left);
            panel.Children.Add(right);
            var host = new Window { Content = panel, Width = 900, Height = 200, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Show();
            try
            {
                s.List.SelectedItem = s.List.Items[0];
                s.List.Items[1].IsChecked = true;
                s.List.Items[2].IsChecked = true;
                host.UpdateLayout();
                Assert.False(((Button)left.FindName("ReplyButton")!).IsEnabled);
                Assert.False(((Button)left.FindName("ForwardButton")!).IsEnabled);
                var del = (Button)right.FindName("DeleteButton")!;
                Assert.True(del.IsEnabled);
                Assert.Equal("選んだ2通を削除", del.Content);
            }
            finally { host.Close(); }
        });
    }

    [Fact]
    public void HeaderButtons_DraftsFolder_ShowsEditInsteadOfReplyGroup()
    {
        RunSta(() =>
        {
            EnsureApp();
            var s = new Setup(FolderKind.Drafts, 1);
            var vm = new HeaderActionsViewModel(s.Controller, s.List);
            var left = new HeaderActionsView(vm);
            var host = new Window { Content = left, Width = 900, Height = 200, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Show();
            try
            {
                s.List.SelectedItem = s.List.Items[0];
                host.UpdateLayout();
                var edit = (Button)left.FindName("EditButton")!;
                Assert.Equal(Visibility.Visible, edit.Visibility);
                Assert.True(edit.IsEnabled);
                Assert.Equal(Strings.TB_24, edit.Content);
                Assert.False(((Button)left.FindName("ReplyButton")!).IsVisible);
            }
            finally { host.Close(); }
        });
    }

    [Fact]
    public void ContextMenu_Inbox_HasEightLargeItems_WithRedDelete_AndJapaneseLabels()
    {
        RunSta(() =>
        {
            EnsureApp();
            var s = new Setup(FolderKind.Inbox, 2);
            var host = new ListContextMenuHost(s.List, s.Controller, s.State, s.Mail, new FakeTextScale());
            var menu = host.BuildMenu(s.List.Items[0])!;

            var items = menu.Items.OfType<MenuItem>().ToList();
            Assert.Equal(8, items.Count);
            Assert.All(items, i => Assert.True(i.MinHeight >= 48));
            Assert.Equal(new object?[] { "返信", "全員に返信", "転送", "既読にする", "迷惑メールにする", "削除", "アドレス帳に追加", "受信拒否に追加" },
                items.Select(i => i.Header).ToArray());
            Assert.Equal(Application.Current.Resources["DeleteOutlineText"], items[5].Foreground);
        });
    }

    [Fact]
    public void ContextMenu_Trash_HasOnlyRestore()
    {
        RunSta(() =>
        {
            EnsureApp();
            var s = new Setup(FolderKind.Trash, 1);
            var host = new ListContextMenuHost(s.List, s.Controller, s.State, s.Mail, new FakeTextScale());
            var menu = host.BuildMenu(s.List.Items[0])!;
            Assert.Equal(new object?[] { Strings.FA_09 }, menu.Items.OfType<MenuItem>().Select(i => i.Header).ToArray());
        });
    }

    // 回帰: チェック欄の上のダブルクリックは行の本文として扱わない（本文の上は行を返す）
    [Fact]
    public void RowBodyAt_IgnoresCheckBoxAndFindsTextBody()
    {
        RunSta(() =>
        {
            EnsureApp();
            var s = new Setup(FolderKind.Drafts, 1);
            var template = new DataTemplate();
            var panel = new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            var box = new FrameworkElementFactory(typeof(CheckBox));
            box.SetValue(FrameworkElement.WidthProperty, 40.0);
            box.SetValue(FrameworkElement.HeightProperty, 40.0);
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Subject"));
            panel.AppendChild(box);
            panel.AppendChild(text);
            template.VisualTree = panel;

            var list = new ListView { ItemsSource = s.List.Items, ItemTemplate = template, Width = 300, Height = 100 };
            var window = new Window { Content = list, Width = 400, Height = 200, ShowInTaskbar = false };
            window.Show();
            try
            {
                list.UpdateLayout();
                var item = (ListViewItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                var checkBox = FindChild<CheckBox>(item)!;
                var textBlock = FindChild<TextBlock>(item)!;
                Assert.Null(ListContextMenuHost.RowBodyAt(checkBox));
                Assert.Same(s.List.Items[0], ListContextMenuHost.RowBodyAt(textBlock));
                Assert.Same(s.List.Items[0], ListContextMenuHost.RowBodyAt(item));
                Assert.Null(ListContextMenuHost.RowBodyAt(null));
                Assert.Null(ListContextMenuHost.RowBodyAt(list));
            }
            finally { window.Close(); }
        });
    }

    private static T? FindChild<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (c is T t) return t;
            if (FindChild<T>(c) is { } f) return f;
        }
        return null;
    }
}
