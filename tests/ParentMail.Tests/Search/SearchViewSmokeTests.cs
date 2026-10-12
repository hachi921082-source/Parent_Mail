using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.App.Features.MailList;
using ParentMail.App.Features.Search;
using ParentMail.App.Features.Shell;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.Search;

// 検索ボックスのビューを実際に作り、XAML の参照ミスと、一覧ペインの検索枠・下部ボタンとのつながりを確かめる（表示はしない）。
[Collection("Wpf")]
public class SearchViewSmokeTests
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

    [Fact]
    public void AddSearch_PlugsBoxIntoListPane_AndBottomSearchButtonRequestsFocus()
    {
        RunSta(() =>
        {
            EnsureApp();
            var services = new ServiceCollection();
            services.AddAppShell();
            services.AddSingleton<IAppLog>(new FakeAppLog());
            services.AddSingleton<IAppState>(new FakeAppState { CurrentAccountId = "a" });
            services.AddSingleton<IClock>(new FakeClock());
            services.AddSingleton<ITextScale>(new FakeTextScale());
            services.AddSingleton<IMailRepository>(new InMemoryMailRepository());
            services.AddSingleton<IOutboxRepository>(new InMemoryOutboxRepository());
            services.AddSingleton<IDraftRepository>(new InMemoryDraftRepository());
            services.AddMailList();
            SearchRegistration.AddSearch(services);
            using var sp = services.BuildServiceProvider();

            var window = sp.GetRequiredService<MainWindow>();
            var list = Assert.IsType<MailListView>(((ContentControl)window.FindName("ListPaneSlot")).Content);
            Assert.IsType<SearchView>(list.SearchHost.Content);
        });
    }

    [Fact]
    public void SearchView_ShowsPlaceholderButtonsAndNotes_WithJapaneseText()
    {
        RunSta(() =>
        {
            EnsureApp();
            var mail = new InMemoryMailRepository();
            mail.Upsert("a", new MailMessage { GmailMsgId = 1, Subject = "運動会のお知らせ", FromName = "田中 花子", DateUtc = DateTime.UtcNow },
                new[] { new MessageFolder(1, FolderKind.Inbox, 1, 1) });
            var state = new FakeAppState { CurrentAccountId = "a" };
            var clock = new FakeClock { UtcNow = DateTime.UtcNow };
            var list = new ListViewModel(mail, new InMemoryOutboxRepository(), new InMemoryDraftRepository(), state, clock, dispatch: a => a());
            var vm = new SearchViewModel(new SearchService(mail, clock), list, state);
            var view = new SearchView(vm);
            var host = new Window { Content = view, Width = 400, Height = 400, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Show();
            try
            {
                view.UpdateLayout();
                var texts = Descendants<TextBlock>(view).Where(t => t.Visibility == Visibility.Visible).Select(t => t.Text).ToList();
                Assert.Contains(Strings.SR_03, texts);
                Assert.DoesNotContain(Strings.SR_05, texts);
                var buttons = Descendants<Button>(view).Select(b => b.Content as string).ToList();
                Assert.Equal(new[] { Strings.SR_01, Strings.SR_02 }, buttons);

                vm.Query = "運動";
                // 実機と同じく、続きが画面のスレッドで走るようにする
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(host.Dispatcher));
                var task = vm.SearchCommand.ExecuteAsync(null);
                // 画面のスレッドで続きが走るよう、メッセージを回しながら待つ
                var until = DateTime.UtcNow.AddSeconds(5);
                while (!task.IsCompleted && DateTime.UtcNow < until)
                {
                    host.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                    Thread.Sleep(10);
                }
                Assert.True(task.IsCompleted);
                view.UpdateLayout();
                texts = Descendants<TextBlock>(view).Where(t => t.Visibility == Visibility.Visible).Select(t => t.Text).ToList();
                Assert.Contains(Strings.SR_05, texts);
                Assert.DoesNotContain(Strings.SR_03, texts);
            }
            finally { host.Close(); }
        });
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }
}
