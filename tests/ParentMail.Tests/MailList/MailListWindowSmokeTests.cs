using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.App.Features.MailList;
using ParentMail.App.Features.Shell;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.MailList;

// ビューを実際に作り、XAML の参照ミス・ボタンの並び・主窓のスロットへの差し込みを確かめる（表示はしない）。
[Collection("Wpf")]
public class MailListWindowSmokeTests
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

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in FindAll<T>(child)) yield return d;
        }
    }

    [Fact]
    public void ButtonStrip_HasFiveButtons_KeepGridCells_AndBlankCellHasNoControl()
    {
        RunSta(() =>
        {
            EnsureApp();
            var vm = new ListButtonStripViewModel(new FakeAppState { CurrentAccountId = "a" }, new FakeTextScale());
            var view = new ListButtonStripView(vm);
            var host = new Window { Content = view, Width = 400, Height = 300, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Show();
            try
            {
                view.UpdateLayout();
                var buttons = FindAll<Button>(view).ToList();
                Assert.Equal(new[] { Strings.TB_14, Strings.TB_16, Strings.TB_17, Strings.TB_18, Strings.TB_19 },
                    buttons.Select(b => ((TextBlock)b.Content).Text).ToArray());
                Assert.Same(Application.Current.Resources["PrimaryButton"], buttons[0].Style);
                Assert.All(buttons, b => Assert.True(b.ActualHeight >= 56));
                // 2 列 x 3 行。各ボタンのマスは変わらず、行 0・列 1 は空きマス（Q88）
                var cells = buttons.Select(b => (Grid.GetRow(b), Grid.GetColumn(b))).ToArray();
                Assert.Equal(new[] { (0, 0), (1, 0), (1, 1), (2, 0), (2, 1) }, cells);
                Assert.DoesNotContain(cells, c => c == (0, 1));
                Assert.Equal(5, buttons.Count);
                Assert.Equal(5, FindAll<UIElement>(view).Count(e => e is Button));
                var rows = buttons.Select(b => (int)Math.Round(b.TranslatePoint(new Point(0, 0), view).Y)).Distinct().Count();
                var cols = buttons.Select(b => (int)Math.Round(b.TranslatePoint(new Point(0, 0), view).X)).Distinct().Count();
                Assert.Equal(3, rows);
                Assert.Equal(2, cols);
            }
            finally { host.Close(); }
        });
    }

    [Fact]
    public void MailListView_ShowsRowsWithCheckboxes_AndJapaneseTexts()
    {
        RunSta(() =>
        {
            EnsureApp();
            var mail = new InMemoryMailRepository();
            mail.Upsert("a", new MailMessage { GmailMsgId = 1, Subject = "ご案内の件", FromName = "山田 太郎", DateUtc = DateTime.UtcNow.AddHours(-1) },
                new[] { new MessageFolder(1, FolderKind.Inbox, 1, 1) });
            mail.Upsert("a", new MailMessage { GmailMsgId = 2, Subject = "お知らせ", FromName = "田中 花子", DateUtc = DateTime.UtcNow.AddHours(-2), HasAttachments = true },
                new[] { new MessageFolder(2, FolderKind.Inbox, 2, 1) });
            var state = new FakeAppState { CurrentAccountId = "a" };
            var list = new ListViewModel(mail, new InMemoryOutboxRepository(), new InMemoryDraftRepository(), state,
                new FakeClock { UtcNow = DateTime.UtcNow }, dispatch: a => a());
            list.LoadFolder(FolderKind.Inbox);

            var view = new MailListView(list);
            var host = new Window { Content = view, Width = 500, Height = 600, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            host.Show();
            try
            {
                view.UpdateLayout();
                var texts = FindAll<TextBlock>(view).Select(t => t.Text).ToList();
                Assert.Contains("ご案内の件", texts);
                Assert.Contains("田中 花子", texts);
                Assert.Contains(Strings.ML_09, FindAll<ContentPresenter>(view).Select(c => c.Content as string));
                var boxes = FindAll<CheckBox>(view).Where(c => Equals(c.GetValue(System.Windows.Automation.AutomationProperties.NameProperty), Strings.ML_08)).ToList();
                Assert.Equal(2, boxes.Count);

                // チェック欄を押しても行は選ばれず、チェックだけが付く
                boxes[0].IsChecked = true;
                Assert.Equal(1, list.CheckedCount);
                Assert.Null(list.SelectedItem);
                Assert.False(list.ShowEmptyMessage);
            }
            finally { host.Close(); }
        });
    }

    [Fact]
    public void AddMailList_PlugsViewsIntoMainWindowSlots()
    {
        RunSta(() =>
        {
            EnsureApp();
            var services = new ServiceCollection();
            services.AddAppShell();
            services.AddSingleton<IAppLog>(new FakeAppLog());
            services.AddSingleton<IAppState>(new FakeAppState());
            services.AddSingleton<IMailRepository>(new InMemoryMailRepository());
            services.AddSingleton<IOutboxRepository>(new InMemoryOutboxRepository());
            services.AddSingleton<IDraftRepository>(new InMemoryDraftRepository());
            services.AddMailList();
            using var sp = services.BuildServiceProvider();

            var window = sp.GetRequiredService<MainWindow>();
            Assert.IsType<MailListView>(((ContentControl)window.FindName("ListPaneSlot")).Content);
            Assert.IsType<ListButtonStripView>(((ContentControl)window.FindName("BottomStripSlot")).Content);
            Assert.Same(window, sp.GetRequiredService<MainWindow>());
        });
    }
}
