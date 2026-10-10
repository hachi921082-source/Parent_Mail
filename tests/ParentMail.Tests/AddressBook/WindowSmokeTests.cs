using System.Windows;
using System.Windows.Controls;
using ParentMail.App.Features.AddressBook;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.AddressBook;

// 2 つのウィンドウを実際に作り、XAML の参照ミス・初期フォーカス・既定ボタンを確かめる（表示はしない）。
[Collection("Wpf")]
public class WindowSmokeTests
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

    private sealed class NoDialogs : ParentMail.Core.IDialogService
    {
        public void ShowNotice(string title, string message) { }
        public bool Confirm(string title, string message, string yesText, string noText, bool safeIsDefault = true) => false;
        public int Choose(string title, string message, IReadOnlyList<string> buttons, int defaultIndex, int safeIndex) => -1;
    }

    private sealed class NoPresenter : IContactEditPresenter
    {
        public bool ShowEdit(ContactEditViewModel vm) => false;
    }

    [Fact]
    public void Windows_construct_with_expected_defaults()
    {
        RunSta(() =>
        {
            EnsureApp();
            var repo = new InMemoryContactRepository();
            repo.Add(new Contact(0, "a", "山田 太郎", "やまだ", new[] { "t@example.com" }, "弟"));
            var scale = new FakeTextScale();

            var bookVm = new AddressBookViewModel("a", "母のメール", repo, new NoDialogs(), new NoPresenter());
            var book = new AddressBookWindow(bookVm, scale, null);
            Assert.Equal("アドレス帳 - 母のメール", book.Title);
            Assert.Equal(900, book.Width);
            Assert.Equal(600, book.Height);

            var editVm = new ContactEditViewModel(repo, "a");
            var edit = new ContactEditWindow(editVm, scale, null);
            Assert.Equal("人を追加", edit.Title);
            var buttons = FindAll<Button>(edit).ToList();
            Assert.Contains(buttons, b => b.IsDefault && (string)b.Content == "保存");
            Assert.Contains(buttons, b => b.IsCancel && (string)b.Content == "やめる");
            Assert.True(edit.FontSize > 0);
        });
    }

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T hit) yield return hit;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var x in FindAll<T>(child)) yield return x;
        }
    }
}
