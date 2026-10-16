using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NSubstitute;
using ParentMail.App.Features.AccountsAuth;
using ParentMail.Core;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AccountsAuth;

// アカウントボタン帯の実際の画面部品のテスト（画面外に窓を出して配置を確かめる）。
// 矢印の出る・出ない、選択中ボタンの表示位置、隠れた未読のバッジ、タブ停止でないことを見る。
[Collection("Wpf")]
public sealed class AccountStripViewTests
{
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

    private static void Pump()
    {
        for (var i = 0; i < 3; i++) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private sealed class Fixture
    {
        public readonly InMemoryAccountRepository Repo = new();
        public readonly FakeAppState State = new();
        public readonly Dictionary<string, int> Unread = new();
        public AccountSwitchViewModel Vm = null!;
        public AccountStrip Strip = null!;
        public Window Win = null!;

        public static Fixture Create(int count, double width)
        {
            EnsureApp();
            var f = new Fixture();
            for (var i = 1; i <= count; i++) f.Repo.Add(new Account($"a{i}", $"u{i}@gmail.com", $"アカウント{i}", i - 1));
            var mail = Substitute.For<IMailRepository>();
            mail.UnreadCount(Arg.Any<string>(), FolderKind.Inbox).Returns(c => f.Unread.GetValueOrDefault(c.Arg<string>()));
            f.Vm = new AccountSwitchViewModel(f.Repo, f.State, mail, new FakeSettingsStore(), new FakeSyncService());
            f.Vm.Initialize();
            f.Strip = new AccountStrip { Items = f.Vm.Items, ButtonMaxWidth = f.Vm.ButtonMaxWidthDip, FontSize = 24 };
            f.Win = new Window
            {
                Width = width, Height = 200, Left = -20000, Top = 0, ShowInTaskbar = false, ShowActivated = false,
                WindowStyle = WindowStyle.None, Content = f.Strip,
            };
            f.Win.Show();
            Pump();
            return f;
        }

        public void Close() => Win.Close();
        public Button LeftArrow => (Button)Strip.FindName("LeftArrow");
        public Button RightArrow => (Button)Strip.FindName("RightArrow");
        public ScrollViewer Scroller => (ScrollViewer)Strip.FindName("Scroller");

        public Button ButtonOf(string id)
        {
            var host = (ItemsControl)Strip.FindName("Host");
            for (var i = 0; i < host.Items.Count; i++)
            {
                if (((AccountButtonItem)host.Items[i]).AccountId != id) continue;
                var presenter = host.ItemContainerGenerator.ContainerFromIndex(i);
                return (Button)System.Windows.Media.VisualTreeHelper.GetChild(presenter, 0);
            }
            throw new InvalidOperationException(id);
        }

        public bool FullyVisible(string id)
        {
            var b = ButtonOf(id);
            var host = (ItemsControl)Strip.FindName("Host");
            var left = b.TransformToAncestor(host).Transform(new Point(0, 0)).X;
            return left >= Scroller.HorizontalOffset - 0.6 && left + b.ActualWidth <= Scroller.HorizontalOffset + Scroller.ViewportWidth + 0.6;
        }
    }

    [Fact]
    public void FitsInWideWindow_NoArrows()
    {
        RunSta(() =>
        {
            var f = Fixture.Create(2, 1200);
            try
            {
                Assert.Equal(Visibility.Collapsed, f.LeftArrow.Parent is FrameworkElement l ? l.Visibility : Visibility.Visible);
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)f.RightArrow.Parent).Visibility);
            }
            finally { f.Close(); }
        });
    }

    [Fact]
    public void FiveAccountsInNarrowWindow_ShowArrows_ArrowsNotTabStops_SelectedIsVisible()
    {
        RunSta(() =>
        {
            var f = Fixture.Create(5, 700);
            try
            {
                Assert.Equal(Visibility.Visible, ((FrameworkElement)f.LeftArrow.Parent).Visibility);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)f.RightArrow.Parent).Visibility);
                Assert.False(f.LeftArrow.IsTabStop);
                Assert.False(f.RightArrow.IsTabStop);
                Assert.Equal("左のアカウントを見る", System.Windows.Automation.AutomationProperties.GetName(f.LeftArrow));
                Assert.Equal("右のアカウントを見る", System.Windows.Automation.AutomationProperties.GetName(f.RightArrow));
                // 先頭が選択中: 左は端なので灰色、右は押せる
                Assert.True(f.FullyVisible("a1"));
                Assert.False(f.LeftArrow.IsEnabled);
                Assert.True(f.RightArrow.IsEnabled);

                // 最後のアカウントへ切り替えると、それが全部見える位置まで動く
                f.Vm.Items.Single(i => i.AccountId == "a5").SelectCommand.Execute(null);
                Pump();
                Assert.True(f.FullyVisible("a5"));
                Assert.True(f.LeftArrow.IsEnabled);
                Assert.False(f.RightArrow.IsEnabled);
            }
            finally { f.Close(); }
        });
    }

    [Fact]
    public void ArrowPress_RevealsNextHiddenButtonCompletely()
    {
        RunSta(() =>
        {
            var f = Fixture.Create(5, 700);
            try
            {
                var before = f.Scroller.HorizontalOffset;
                f.RightArrow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                Assert.True(f.Scroller.HorizontalOffset > before);
                // 見えている最後のボタンは全部見える（欠けたまま止まらない）
                var visibleCount = new[] { "a1", "a2", "a3", "a4", "a5" }.Count(f.FullyVisible);
                Assert.True(visibleCount >= 2);
                f.LeftArrow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                Assert.Equal(0, f.Scroller.HorizontalOffset, 0.6);
            }
            finally { f.Close(); }
        });
    }

    [Fact]
    public void HiddenUnread_ShownOnRightArrow_AsSum_WithTooltipLine()
    {
        RunSta(() =>
        {
            var f = Fixture.Create(5, 700);
            try
            {
                f.Unread["a4"] = 3;
                f.Unread["a5"] = 4;
                f.Vm.Refresh();
                Pump();
                var badge = (Border)f.Strip.FindName("RightBadge");
                var text = (TextBlock)f.Strip.FindName("RightBadgeText");
                Assert.Equal(Visibility.Visible, badge.Visibility);
                Assert.Equal("7", text.Text);
                Assert.Contains("見えていないアカウントに未読メールがあります", (string)f.RightArrow.ToolTip);
                Assert.Equal(Visibility.Collapsed, ((Border)f.Strip.FindName("LeftBadge")).Visibility);

                // 未読が無くなればバッジも案内文も消える
                f.Unread.Clear();
                f.Vm.Refresh();
                Pump();
                Assert.Equal(Visibility.Collapsed, badge.Visibility);
                Assert.Equal("右のアカウントを見る", (string)f.RightArrow.ToolTip);
            }
            finally { f.Close(); }
        });
    }

    [Fact]
    public void FocusOnHiddenButton_ScrollsItIntoView()
    {
        RunSta(() =>
        {
            var f = Fixture.Create(5, 700);
            try
            {
                var last = f.ButtonOf("a5");
                Assert.False(f.FullyVisible("a5"));
                last.Focus();
                Pump();
                Assert.True(f.FullyVisible("a5"));
            }
            finally { f.Close(); }
        });
    }

    [Fact]
    public void ResizeWindow_KeepsSelectedVisible()
    {
        RunSta(() =>
        {
            var f = Fixture.Create(5, 1100);
            try
            {
                f.Vm.Items.Single(i => i.AccountId == "a5").SelectCommand.Execute(null);
                Pump();
                f.Win.Width = 600;
                Pump();
                Assert.True(f.FullyVisible("a5"));
            }
            finally { f.Close(); }
        });
    }
}
