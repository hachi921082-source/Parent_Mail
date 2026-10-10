using System.Windows;
using System.Windows.Controls;
using ParentMail.App.Features.BlockList;
using ParentMail.App.Resources;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.BlockList;

// 受信拒否リストウィンドウの状態（追加・重複・エラー・削除確認）と、ウィンドウ生成の確認。
[Collection("Wpf")]
public class BlockListViewModelTests
{
    private const string Acc = "a1";

    private static (BlockListViewModel vm, InMemoryBlockRepository repo, FakeDialogService dlg) Make()
    {
        var repo = new InMemoryBlockRepository();
        var dlg = new FakeDialogService();
        return (new BlockListViewModel(Acc, "母のメール", repo, dlg), repo, dlg);
    }

    [Fact]
    public void Title_and_initial_state()
    {
        var (vm, _, _) = Make();
        Assert.Equal("受信拒否リスト - 母のメール", vm.Title);
        Assert.True(vm.IsEmpty);
        Assert.True(vm.IsAddress);
        Assert.False(vm.IsDomain);
        Assert.Equal("受信拒否はありません", BlockListText.EmptyList);
    }

    [Fact]
    public void Add_address_clears_input_and_lists_it()
    {
        var (vm, repo, _) = Make();
        vm.InputText = "Spam@Example.com";
        vm.AddCommand.Execute(null);

        Assert.Equal("", vm.InputText);
        Assert.Equal("", vm.ErrorText);
        Assert.Single(vm.Items);
        Assert.Equal("アドレス", vm.Items[0].KindText);
        Assert.Equal("spam@example.com", vm.Items[0].Value);
        Assert.Single(repo.GetAll(Acc));
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public void Add_domain_with_at_sign()
    {
        var (vm, _, _) = Make();
        vm.IsAddress = false;
        vm.InputText = "@Example.com";
        vm.AddCommand.Execute(null);

        Assert.Equal("ドメイン", vm.Items[0].KindText);
        Assert.Equal("example.com", vm.Items[0].Value);
    }

    [Fact]
    public void Invalid_input_shows_BL07_and_typing_clears_it()
    {
        var (vm, repo, _) = Make();
        vm.InputText = "bad";
        vm.AddCommand.Execute(null);
        Assert.Equal(Strings.BL_07, vm.ErrorText);
        Assert.Empty(repo.GetAll(Acc));

        vm.InputText = "bad2";
        Assert.Equal("", vm.ErrorText);
    }

    [Fact]
    public void Duplicate_shows_BL05_and_keeps_input()
    {
        var (vm, _, _) = Make();
        vm.InputText = "a@example.com";
        vm.AddCommand.Execute(null);
        vm.InputText = "A@EXAMPLE.COM";
        vm.AddCommand.Execute(null);

        Assert.Equal(Strings.BL_05, vm.ErrorText);
        Assert.Single(vm.Items);
        Assert.Equal("A@EXAMPLE.COM", vm.InputText);
    }

    [Fact]
    public void Delete_asks_confirmation_with_safe_default_and_only_removes_on_yes()
    {
        var (vm, repo, dlg) = Make();
        vm.InputText = "a@example.com";
        vm.AddCommand.Execute(null);

        dlg.ConfirmAnswer = false;
        vm.DeleteCommand.Execute(null);
        Assert.Single(repo.GetAll(Acc));

        dlg.ConfirmAnswer = true;
        vm.DeleteCommand.Execute(null);
        Assert.Empty(repo.GetAll(Acc));
        Assert.True(vm.IsEmpty);
        Assert.False(vm.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void Confirm_texts_follow_ui_strings()
    {
        Assert.Equal("この受信拒否を解除しますか?", BlockListText.ConfirmQuestion);
        Assert.Equal("解除する", BlockListText.ConfirmYes);
        Assert.Equal("やめる", BlockListText.ConfirmNo);
        Assert.Equal("受信拒否の解除", BlockListText.ConfirmTitle);
        Assert.Equal("このアドレスだけ", BlockListText.RadioAddress);
        Assert.Equal("このドメイン全体", BlockListText.RadioDomain);
        Assert.Equal("アドレスまたはドメイン", BlockListText.ValueLabel);
        Assert.Equal("閉じる", BlockListText.BtnClose);
    }

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

    [Fact]
    public void Window_constructs_with_expected_size_and_close_button()
    {
        RunSta(() =>
        {
            if (Application.Current is null)
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Parent_Mail;component/Resources/Palette.xaml"),
                });
            }
            var (vm, _, _) = Make();
            var w = new BlockListWindow(vm, new FakeTextScale(), null);
            Assert.Equal("受信拒否リスト - 母のメール", w.Title);
            Assert.Equal(720, w.Width);
            Assert.Equal(560, w.Height);
            var close = Find<Button>(w).Single(b => (string)b.Content == "閉じる");
            Assert.True(close.IsCancel);
        });
    }

    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T hit) yield return hit;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var x in Find<T>(child)) yield return x;
    }
}
