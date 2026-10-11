using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ParentMail.App.Features.Compose;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.Compose;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.Compose;

// アドレス帳から選ぶ画面と、作成窓のボタンを実際に作って（表示は最小限）、配置・規則・作成窓にだけモーダルであることを確かめる。
[Collection("Wpf")]
public class AddressPickerWindowTests
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

    private static ComposeWindow MakeCompose()
    {
        var root = Path.Combine(Path.GetTempPath(), "ParentMail_選窓_" + Guid.NewGuid().ToString("N"));
        var vm = new ComposeViewModel(new ComposeInit { AccountId = "a" }, "母のメール", new FakeOutboxService(), new FakeDialogService(),
            new RecipientSuggester(new InMemoryContactRepository(), new InMemoryMailRepository()), new CachePaths(Path.Combine(root, "データ")),
            new FakeAppLog());
        return new ComposeWindow(vm, new FakeTextScale());
    }

    private static AddressPickerDialog MakeDialog(Window? owner)
    {
        var vm = new AddressPickerViewModel(new[]
        {
            new PickerRow("山田 太郎", "taro@example.com", false),
            new PickerRow("佐藤 花子", "hanako@example.jp", true),
        }, "母のメール");
        return new AddressPickerDialog(vm, new FakeTextScale(), owner);
    }

    [Fact]
    public void Compose_window_has_a_pick_button_beside_each_field_and_bcc_one_follows_the_bcc_row()
    {
        RunSta(() =>
        {
            EnsureApp();
            var w = MakeCompose();

            Assert.Equal(Strings.CP_18, w.ToPickField.Content);
            Assert.Equal(Strings.CP_18, w.CcPickField.Content);
            Assert.Equal(Strings.CP_18, w.BccPickField.Content);
            Assert.All(new[] { w.ToPickField, w.CcPickField, w.BccPickField }, b =>
            {
                Assert.True(b.MinHeight >= 56);
                Assert.NotNull(b.Command);
            });
            Assert.Equal(Visibility.Collapsed, w.BccPickField.Visibility);

            w.ViewModel.AddBccCommand.Execute(null);
            Assert.Equal(Visibility.Visible, w.BccPickField.Visibility);
        });
    }

    [Fact]
    public void Tab_order_puts_each_button_right_after_its_field()
    {
        RunSta(() =>
        {
            EnsureApp();
            var w = MakeCompose();
            var grid = (Grid)w.Content;
            var order = new UIElement[]
            {
                w.ToField, w.ToPickField, w.CcField, w.CcPickField, grid.Children.OfType<Button>().First(b => b.Name == "AddBccButton"),
                w.BccField, w.BccPickField, w.SubjectField, w.BodyField,
            };
            var index = order.Select(e => grid.Children.IndexOf(e)).ToList();

            Assert.All(index, i => Assert.True(i >= 0));
            Assert.Equal(index.OrderBy(i => i), index);
            // 添付の「削除」ボタンのあとに「添付」「送信」が来る
            Assert.True(grid.Children.IndexOf(w.AttachField.Parent as UIElement) > grid.Children.IndexOf(w.BodyField));
        });
    }

    [Fact]
    public void Picker_dialog_follows_the_window_rules()
    {
        RunSta(() =>
        {
            EnsureApp();
            var d = MakeDialog(null);

            Assert.Equal("アドレス帳から選ぶ - 母のメール", d.Title);
            Assert.Equal(720, d.Width);
            Assert.Equal(560, d.Height);
            Assert.True(d.ConfirmField.IsDefault);   // Enter = 入れる
            Assert.False(d.ConfirmField.IsCancel);
            Assert.True(d.CancelField.IsCancel);     // Esc = やめる
            Assert.Equal("入れる", d.ConfirmField.Content);
            Assert.Equal("やめる", d.CancelField.Content);
            Assert.Equal(System.Windows.Input.KeyboardNavigationMode.Once, System.Windows.Input.KeyboardNavigation.GetTabNavigation(d.ListField));
            Assert.Equal(SelectionMode.Single, d.ListField.SelectionMode);

            // 行の高さは 56 DIP 以上
            var style = d.ListField.ItemContainerStyle;
            var min = style.Setters.OfType<Setter>().Single(s => s.Property == FrameworkElement.MinHeightProperty);
            Assert.True((double)min.Value >= 56);
        });
    }

    [Fact]
    public void Picker_dialog_disables_only_its_compose_window_while_open()
    {
        RunSta(() =>
        {
            EnsureApp();
            var owner = MakeCompose();
            var other = MakeCompose();
            owner.Show();
            other.Show();
            var dialog = MakeDialog(owner);

            bool? ownerEnabledWhileOpen = null, otherEnabledWhileOpen = null;
            dialog.Dispatcher.BeginInvoke(new Action(() =>
            {
                ownerEnabledWhileOpen = owner.IsEnabled;
                otherEnabledWhileOpen = other.IsEnabled;
                dialog.Close();
            }), DispatcherPriority.ContextIdle);

            var confirmed = dialog.ShowForOwner();

            Assert.False(confirmed);
            Assert.False(ownerEnabledWhileOpen);
            Assert.True(otherEnabledWhileOpen);
            Assert.True(owner.IsEnabled);
            owner.Close();
            other.Close();
        });
    }

    [Fact]
    public void Picker_dialog_confirm_button_without_a_check_keeps_it_open_and_with_a_check_closes_it()
    {
        RunSta(() =>
        {
            EnsureApp();
            var owner = MakeCompose();
            owner.Show();
            var dialog = MakeDialog(owner);
            var vm = (AddressPickerViewModel)dialog.DataContext;
            var stillOpenAfterEmptyConfirm = false;

            dialog.Dispatcher.BeginInvoke(new Action(() =>
            {
                dialog.ConfirmField.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                stillOpenAfterEmptyConfirm = dialog.IsVisible;
                Assert.Equal(Strings.CP_20, vm.ErrorText);
                vm.Rows[0].Toggle();
                dialog.ConfirmField.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }), DispatcherPriority.ContextIdle);

            var confirmed = dialog.ShowForOwner();

            Assert.True(stillOpenAfterEmptyConfirm);
            Assert.True(confirmed);
            Assert.Equal("taro@example.com", vm.Result.Single().Address);
            owner.Close();
        });
    }
}
