using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.App.Features.MailDisplay;
using ParentMail.App.Features.Shell;
using ParentMail.App.Resources;
using ParentMail.App.SafetyWarnings;
using ParentMail.Core;
using ParentMail.Core.Features.MailDisplay;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Core.SafetyWarnings;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.MailDisplay;

// プレビューの画面部品を実際に作り、XAML の参照ミスと本文の組み立て（リンク化・日本語）を確かめる。
// WebView2 は作らない（HTML 表示の実機確認は W3 の手動確認で行う）。
[Collection("Wpf")]
public class PreviewViewSmokeTests
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

    private sealed class NoRead : IReadStateSync
    {
        public Task SetSeenAsync(string accountId, long gmailMsgId, bool seen, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoLauncher : IExternalLauncher
    {
        public void OpenUrl(string url) { }
    }

    private sealed class SimpleSwitch : IHtmlModeSwitch
    {
        public bool IsOn { get; set; }
        public event EventHandler? Changed { add { } remove { } }
    }

    private static PreviewViewModel CreateVm(FakeAppState state, InMemoryMailRepository mails)
    {
        var dialogs = new FakeDialogService();
        return new PreviewViewModel(state, mails, new FakeBodyFetcher(), new FakeAttachmentService(), new NoRead(),
            new FakeContactLookup(), new FakeTextScale(), new SimpleSwitch(), new PreviewSelectionHints(),
            new InMemoryOutboxRepository(), new InMemoryDraftRepository(), new InMemoryAccountRepository(), dialogs, new FakeAppLog(),
            new LinkClickHandler(dialogs, new NoLauncher(), new FakeComposeWindowFactory(), state));
    }

    [Fact]
    public void 本文のURLはリンクになり日本語がそのまま入る()
    {
        RunSta(() =>
        {
            EnsureApp();
            var state = new FakeAppState { CurrentAccountId = Acc };
            var mails = new InMemoryMailRepository();
            mails.Upsert(Acc, new MailMessage
            {
                GmailMsgId = 1, Subject = "ご案内", FromAddress = "taro@example.com", FromName = "山田 太郎",
                BodyState = BodyState.Fetched, BodyText = "詳細はhttps://example.com/日程をご覧ください。\n二行目",
            }, new[] { new MessageFolder(1, FolderKind.Inbox, 1, 1) });

            var vm = CreateVm(state, mails);
            var view = new PreviewView(vm);
            state.SelectedMessageId = 1;
            vm.LastLoad.GetAwaiter().GetResult();

            var rb = (RichTextBox)view.FindName("PlainBox");
            var text = new TextRange(rb.Document.ContentStart, rb.Document.ContentEnd).Text;
            Assert.Contains("詳細は", text);
            Assert.Contains("二行目", text);
            var links = rb.Document.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<Hyperlink>()).ToList();
            var link = Assert.Single(links);
            Assert.Equal("https://example.com/", link.Tag);
            Assert.NotNull(view.HeaderButtons);
            Assert.NotNull(view.DeleteButton);
        });
    }

    [Fact]
    public void 主窓のスロットにプレビューが入り_HTMLのチェックが本文の有無に連動する()
    {
        RunSta(() =>
        {
            EnsureApp();
            var state = new FakeAppState { CurrentAccountId = Acc };
            var mails = new InMemoryMailRepository();
            mails.Upsert(Acc, new MailMessage
            {
                GmailMsgId = 1, Subject = "HTMLなし", BodyState = BodyState.Fetched, BodyText = "本文だけ",
            }, new[] { new MessageFolder(1, FolderKind.Inbox, 1, 1) });
            mails.Upsert(Acc, new MailMessage
            {
                GmailMsgId = 2, Subject = "HTMLあり", BodyState = BodyState.Fetched, BodyText = "本文", BodyHtml = "<p>本文</p>",
            }, new[] { new MessageFolder(2, FolderKind.Inbox, 2, 1) });

            var services = new ServiceCollection();
            var scale = new FakeTextScale();
            var main = new MainViewModel(scale, new ParentMail.App.Services.NotifierService(), state);
            services.AddSingleton(scale);
            services.AddSingleton<ITextScale>(scale);
            services.AddSingleton<IAppState>(state);
            services.AddSingleton<IMailRepository>(mails);
            services.AddSingleton<IBodyFetcher, FakeBodyFetcher>();
            services.AddSingleton<IAttachmentService, FakeAttachmentService>();
            services.AddSingleton<IReadStateSync, NoRead>();
            services.AddSingleton<IContactLookup, FakeContactLookup>();
            services.AddSingleton<IOutboxRepository, InMemoryOutboxRepository>();
            services.AddSingleton<IDraftRepository, InMemoryDraftRepository>();
            services.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
            services.AddSingleton<IDialogService, FakeDialogService>();
            services.AddSingleton<IAppLog, FakeAppLog>();
            services.AddSingleton<IExternalLauncher, NoLauncher>();
            services.AddSingleton<IComposeWindowFactory, FakeComposeWindowFactory>();
            services.AddSingleton<LinkClickHandler>();
            services.AddSingleton(main);
            services.AddMailDisplay();
            using var sp = services.BuildServiceProvider();

            var window = new MainWindow(main, new FakeSettingsStore());
            // 起動時の Loaded と同じ経路で差し込む
            PreviewSlotAttacher.Install(() => sp);
            window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            var slot = (ContentControl)window.FindName("PreviewSlot");
            Assert.IsType<PreviewView>(slot.Content);

            var vm = sp.GetRequiredService<PreviewViewModel>();
            var check = FindCheckBox(window);
            state.SelectedMessageId = 1;
            vm.LastLoad.GetAwaiter().GetResult();
            Assert.False(check.IsEnabled);

            state.SelectedMessageId = 2;
            vm.LastLoad.GetAwaiter().GetResult();
            Assert.True(check.IsEnabled);
        });
    }

    private static CheckBox FindCheckBox(Window window)
    {
        var top = (ContentControl)window.FindName("TopBarSlot");
        CheckBox? Find(DependencyObject p)
        {
            if (p is CheckBox c && (string?)c.Content == Strings.TB_11) return c;
            foreach (var child in LogicalTreeHelper.GetChildren(p).OfType<DependencyObject>())
                if (Find(child) is { } f) return f;
            return null;
        }
        return Find((DependencyObject)top.Content)!;
    }
}
