using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.App;
using ParentMail.App.Features.Compose;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.Compose;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.Compose;

// 作成窓を実際に作り（表示はしない）、XAML の参照ミス・ドロップ・入力補完・窓の登録を確かめる。
[Collection("Wpf")]
public class ComposeWindowSmokeTests
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

    private static ComposeViewModel MakeVm(string root, ComposeInit init, FakeOutboxService outbox, FakeDialogService dialogs,
        InMemoryContactRepository contacts)
        => new(init, "母のメール", outbox, dialogs, new RecipientSuggester(contacts, new InMemoryMailRepository()),
            new CachePaths(Path.Combine(root, "データ")), new FakeAppLog());

    [Fact]
    public void Window_builds_with_expected_defaults()
    {
        RunSta(() =>
        {
            EnsureApp();
            var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
            var vm = MakeVm(root, new ComposeInit { AccountId = "a" }, new FakeOutboxService(), new FakeDialogService(), new InMemoryContactRepository());
            var w = new ComposeWindow(vm, new FakeTextScale());

            Assert.Equal("新しいメール - 母のメール", w.Title);
            Assert.Equal(960, w.Width);
            Assert.Equal(720, w.Height);
            Assert.False(w.BodyField.AcceptsTab);
            Assert.True(w.BodyField.AcceptsReturn);
            Assert.False(w.SubjectField.AcceptsReturn);
            Assert.False(w.SendField.IsDefault);
            Assert.False(w.AttachField.IsCancel);
            Assert.Equal(Visibility.Collapsed, w.BccField.Visibility);
            Assert.True(w.AllowDrop);
            Assert.Empty(w.InputBindings.OfType<KeyBinding>()); // 送信にも Esc にもキーを割り当てない
        });
    }

    [Fact]
    public void Bcc_row_appears_after_the_command()
    {
        RunSta(() =>
        {
            EnsureApp();
            var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
            var vm = MakeVm(root, new ComposeInit { AccountId = "a" }, new FakeOutboxService(), new FakeDialogService(), new InMemoryContactRepository());
            var w = new ComposeWindow(vm, new FakeTextScale());
            Assert.Equal(Visibility.Collapsed, w.BccField.Visibility);
            vm.AddBccCommand.Execute(null);
            Assert.Equal(Visibility.Visible, w.BccField.Visibility);
        });
    }

    [Fact]
    public void Dropped_japanese_named_files_are_attached()
    {
        RunSta(() =>
        {
            EnsureApp();
            var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "ダウンロード"));
            var file1 = Path.Combine(root, "ダウンロード", "家族旅行の写真　その１.jpg");
            var file2 = Path.Combine(root, "ダウンロード", "請求書(令和8年).pdf");
            File.WriteAllBytes(file1, new byte[2048]);
            File.WriteAllBytes(file2, new byte[4096]);

            var dialogs = new FakeDialogService();
            var vm = MakeVm(root, new ComposeInit { AccountId = "a" }, new FakeOutboxService(), dialogs, new InMemoryContactRepository());
            var w = new ComposeWindow(vm, new FakeTextScale());

            var data = new DataObject(DataFormats.FileDrop, new[] { file1, file2 });
            w.AddDroppedFiles(data);

            Assert.Equal(new[] { "家族旅行の写真　その１.jpg", "請求書(令和8年).pdf" }, vm.Attachments.Select(a => a.Name));
            Assert.True(vm.HasAttachments);
            Assert.Empty(dialogs.Notices);
            Assert.All(vm.Attachments, a => Assert.True(File.Exists(a.Info.LocalPath)));
        });
    }

    [Fact]
    public void Recipient_box_suggests_and_inserts_a_contact_with_japanese_name()
    {
        RunSta(() =>
        {
            EnsureApp();
            var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
            var contacts = new InMemoryContactRepository();
            contacts.Add(new Contact(0, "a", "山田 太郎", "やまだ たろう", new[] { "taro@example.com" }, ""));
            var vm = MakeVm(root, new ComposeInit { AccountId = "a" }, new FakeOutboxService(), new FakeDialogService(), contacts);
            var w = new ComposeWindow(vm, new FakeTextScale());

            w.Show(); // Popup は表示中の窓でないと開かない
            var box = w.ToField;
            var inner = (TextBox)box.FindName("Box")!;
            inner.Text = "やま";
            inner.CaretIndex = inner.Text.Length;
            box.RefreshSuggest();
            Assert.True(box.IsSuggestOpen);
            Assert.Equal(1, box.SuggestCount);

            box.InsertFirstSuggestion();
            Assert.Equal("山田 太郎 <taro@example.com>, ", vm.ToText);
            Assert.False(box.IsSuggestOpen);
            w.Close();
        });
    }

    [Fact]
    public void Registry_tracks_windows_until_they_close()
    {
        RunSta(() =>
        {
            EnsureApp();
            var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
            var registry = new ComposeWindowRegistry();
            var vm1 = MakeVm(root, new ComposeInit { AccountId = "a" }, new FakeOutboxService(), new FakeDialogService(), new InMemoryContactRepository());
            var vm2 = MakeVm(root, new ComposeInit { AccountId = "a" }, new FakeOutboxService(), new FakeDialogService(), new InMemoryContactRepository());
            var w1 = new ComposeWindow(vm1, new FakeTextScale());
            var w2 = new ComposeWindow(vm2, new FakeTextScale());
            registry.Register(w1);
            registry.Register(w2);
            w1.Show();
            w2.Show();
            Assert.Equal(2, registry.Count);
            w1.Close();
            Assert.Equal(1, registry.Count);
            w2.Close();
            Assert.Equal(0, registry.Count);
        });
    }

    private static ComposeWindow MakeWindow(string root)
        => new(MakeVm(root, new ComposeInit { AccountId = "a" }, new FakeOutboxService(), new FakeDialogService(), new InMemoryContactRepository()),
            new FakeTextScale());

    [Fact]
    public void OpenOnce_second_request_activates_existing_and_creates_nothing()
    {
        RunSta(() =>
        {
            EnsureApp();
            var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
            var registry = new ComposeWindowRegistry();
            var created = 0;
            Assert.True(registry.OpenOnce(() => { created++; return MakeWindow(root); }));
            Assert.False(registry.OpenOnce(() => { created++; return MakeWindow(root); }));
            Assert.Equal(1, created);
            Assert.Equal(1, registry.Count);
            Assert.True(registry.TryActivateExisting());
            registry.Windows[0].Close();
        });
    }

    [Fact]
    public void TryActivateExisting_restores_a_minimized_window_and_is_false_when_none_is_open()
    {
        RunSta(() =>
        {
            EnsureApp();
            var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
            var registry = new ComposeWindowRegistry();
            Assert.False(registry.TryActivateExisting());
            registry.OpenOnce(() => MakeWindow(root));
            var w = registry.Windows[0];
            w.WindowState = WindowState.Minimized;
            Assert.True(registry.TryActivateExisting());
            Assert.Equal(WindowState.Normal, w.WindowState);
            w.Close();
        });
    }

    [Fact]
    public void OpenOnce_after_close_creates_a_new_window()
    {
        RunSta(() =>
        {
            EnsureApp();
            var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
            var registry = new ComposeWindowRegistry();
            registry.OpenOnce(() => MakeWindow(root));
            registry.Windows[0].Close();
            Assert.False(registry.TryActivateExisting());
            Assert.True(registry.OpenOnce(() => MakeWindow(root)));
            Assert.Equal(1, registry.Count);
            registry.Windows[0].Close();
        });
    }

    [Fact]
    public void OpenOnce_creation_failure_leaves_no_guard_behind()
    {
        RunSta(() =>
        {
            EnsureApp();
            var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
            var registry = new ComposeWindowRegistry();
            Assert.Throws<InvalidOperationException>(() => registry.OpenOnce(() => throw new InvalidOperationException("作成失敗")));
            Assert.Equal(0, registry.Count);
            Assert.False(registry.TryActivateExisting());
            // 窓が作れたあとの Show 失敗でも登録を残さない
            Assert.Throws<InvalidOperationException>(() => registry.OpenOnce(() =>
            {
                var w = MakeWindow(root);
                w.Close();
                return w; // 閉じた窓は Show できず例外になる
            }));
            Assert.Equal(0, registry.Count);
            Assert.True(registry.OpenOnce(() => MakeWindow(root)));
            registry.Windows[0].Close();
        });
    }

    [Fact]
    public async Task Send_closes_the_window_right_away_and_hands_the_mail_to_the_outbox()
    {
        var outbox = new FakeOutboxService();
        var closed = false;
        var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
        var tcs = new TaskCompletionSource();
        var t = new Thread(() =>
        {
            try
            {
                EnsureApp();
                var vm = MakeVm(root, new ComposeInit { AccountId = "a", To = new[] { "母 <mother@example.com>" }, Subject = "ご挨拶", Body = "元気です。" },
                    outbox, new FakeDialogService(), new InMemoryContactRepository());
                var w = new ComposeWindow(vm, new FakeTextScale());
                w.Closed += (_, _) => closed = true;
                w.Show();
                w.SendField.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                // Click は async void。UI スレッドの処理を回して完了させる
                var frame = new System.Windows.Threading.DispatcherFrame();
                w.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                tcs.SetResult();
            }
            catch (Exception e) { tcs.SetException(e); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        await tcs.Task;
        t.Join();

        Assert.True(closed);
        var s = Assert.Single(outbox.Enqueued);
        Assert.Equal("ご挨拶", s.Subject);
        Assert.Equal(new[] { "母 <mother@example.com>" }, s.To);
    }

    [Fact]
    public void Closing_with_content_asks_first_and_back_keeps_the_window_then_discard_closes_it()
    {
        RunSta(() =>
        {
            EnsureApp();
            var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
            var dialogs = new FakeDialogService { ChooseAnswer = -1 };
            var vm = MakeVm(root, new ComposeInit { AccountId = "a", Subject = "書きかけ" }, new FakeOutboxService(), dialogs, new InMemoryContactRepository());
            var closed = false;
            var w = new ComposeWindow(vm, new FakeTextScale());
            w.Closed += (_, _) => closed = true;
            w.Show();
            vm.Subject = "書きかけの件名";

            void Pump()
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                w.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }

            w.Close(); // 「戻る」: 窓は残る
            Pump();
            Assert.False(closed);

            dialogs.ChooseAnswer = 1; // 「保存しないで閉じる」
            w.Close();
            Pump();
            Assert.True(closed);
        });
    }

    [Fact]
    public void Closing_an_empty_window_needs_no_question()
    {
        RunSta(() =>
        {
            EnsureApp();
            var root = Path.Combine(Path.GetTempPath(), "ParentMail_窓_" + Guid.NewGuid().ToString("N"));
            var dialogs = new FakeDialogService { ChooseAnswer = -1 };
            var vm = MakeVm(root, new ComposeInit { AccountId = "a" }, new FakeOutboxService(), dialogs, new InMemoryContactRepository());
            var closed = false;
            var w = new ComposeWindow(vm, new FakeTextScale());
            w.Closed += (_, _) => closed = true;
            w.Show();
            w.Close();
            Assert.True(closed);
        });
    }
}
