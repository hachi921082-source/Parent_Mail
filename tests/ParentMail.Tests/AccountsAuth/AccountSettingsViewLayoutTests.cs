using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ParentMail.App.Features.AccountsAuth;
using ParentMail.Core;
using ParentMail.Core.Features.AccountsAuth;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AccountsAuth;

// 設定窓のアカウント欄: 行の一覧は縦スクロールで 2〜3 行ぶんの高さ、追加ボタンは一覧の外（Issue #8、A87）。
[Collection("Wpf")]
public sealed class AccountSettingsViewLayoutTests
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

    private static void Pump()
    {
        for (var i = 0; i < 3; i++) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private sealed class NoDialogs : IAccountDialogs
    {
        public bool ShowRename(RenameViewModel vm) => false;
        public bool ShowReenter(ReenterPasswordViewModel vm) => false;
        public bool ShowAdd(bool firstLaunch) => false;
    }

    private sealed class NoDialogService : IDialogService
    {
        public void ShowNotice(string title, string message) { }
        public bool Confirm(string title, string message, string yesText, string noText, bool safeIsDefault = true) => false;
        public int Choose(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int safeIndex) => -1;
    }

    private static (Window Win, AccountSettingsView View) Show(int count)
    {
        if (Application.Current is null)
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Parent_Mail;component/Resources/Palette.xaml"),
            });
        }
        var repo = new InMemoryAccountRepository();
        for (var i = 1; i <= count; i++) repo.Add(new Account($"a{i}", $"u{i}@gmail.com", $"アカウント{i}", i - 1));
        var svc = new AccountService(repo, new FakeCredentialStore(), new FakeImapGatewayFactory(), new FakeSyncService(), new FakeOutboxService());
        var dialog = new NoDialogService();
        var vm = new AccountSettingsViewModel(svc, repo, new WarningGate(dialog), dialog, new NoDialogs(), dispatch: a => a());
        var view = new AccountSettingsView { DataContext = vm };
        var win = new Window
        {
            Width = 720, SizeToContent = SizeToContent.Height, Left = -20000, Top = 0, ShowInTaskbar = false, ShowActivated = false,
            WindowStyle = WindowStyle.None, Content = view, FontSize = 24,
        };
        win.Show();
        Pump();
        return (win, view);
    }

    [Fact]
    public void FiveRows_ListScrollsVertically_ShowsThreeRows_AddButtonStaysOutside()
    {
        RunSta(() =>
        {
            var (win, view) = Show(5);
            try
            {
                var scroller = (ScrollViewer)view.FindName("RowsScroller");
                var host = (ItemsControl)view.FindName("RowsHost");
                var row = host.ActualHeight / 5;
                Assert.True(scroller.ScrollableHeight > 0);
                Assert.Equal(row * 3, scroller.ActualHeight, 1.0);
                // 追加ボタンは一覧の枠の外（一覧の下）にある
                var add = FindButtons(view).Single(b => b.Command == ((AccountSettingsViewModel)view.DataContext).AddCommand);
                Assert.False(add.IsDescendantOf(scroller));
                Assert.False(add.IsEnabled);
            }
            finally { win.Close(); }
        });
    }

    [Fact]
    public void TwoRows_NoScrollNeeded_AddEnabled()
    {
        RunSta(() =>
        {
            var (win, view) = Show(2);
            try
            {
                var scroller = (ScrollViewer)view.FindName("RowsScroller");
                Assert.Equal(0, scroller.ScrollableHeight, 0.5);
                var add = FindButtons(view).Single(b => b.Command == ((AccountSettingsViewModel)view.DataContext).AddCommand);
                Assert.True(add.IsEnabled);
            }
            finally { win.Close(); }
        });
    }

    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is Button b) yield return b;
            foreach (var d in FindButtons(child)) yield return d;
        }
    }
}
