using System.Windows;
using System.Windows.Controls;
using ParentMail.App.Features.FilterRules;
using ParentMail.App.Resources;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.FilterRules;

// 編集ダイアログの検証、一覧の有効切り替え・並べ替え・削除確認、ウィンドウ生成の確認。
[Collection("Wpf")]
public class FilterViewModelTests
{
    private const string Acc = "a1";

    private sealed class ScriptedPresenter : IFilterEditPresenter
    {
        public Action<FilterRuleEditViewModel>? Fill { get; set; }
        public int Shown;
        public bool ShowEdit(FilterRuleEditViewModel vm)
        {
            Shown++;
            Fill?.Invoke(vm);
            return vm.TrySave();
        }
    }

    private static FilterRuleListViewModel NewList(InMemoryFilterRepository repo, FakeDialogService? dlg = null,
        ScriptedPresenter? p = null)
        => new(Acc, "母のメール", repo, dlg ?? new FakeDialogService(), p ?? new ScriptedPresenter());

    private static FilterRule Add(InMemoryFilterRepository repo, string subject, FilterAction a = FilterAction.MoveToTrash)
        => repo.Save(new FilterRule { AccountId = Acc, SubjectContains = subject, Action = a });

    // ---- 編集ダイアログ ----

    [Fact]
    public void Save_with_no_condition_shows_FR09_and_does_not_save()
    {
        var repo = new InMemoryFilterRepository();
        var vm = new FilterRuleEditViewModel(repo, Acc) { SenderContains = "  " };
        vm.SelectedChoice = vm.Choices[0];
        Assert.False(vm.TrySave());
        Assert.Equal(Strings.FR_09, vm.ErrorText);
        Assert.Empty(repo.GetAll(Acc));
    }

    [Fact]
    public void Save_with_no_action_shows_FR10()
    {
        var repo = new InMemoryFilterRepository();
        var vm = new FilterRuleEditViewModel(repo, Acc) { SubjectContains = "請求" };
        Assert.False(vm.TrySave());
        Assert.Equal(Strings.FR_10, vm.ErrorText);
        Assert.Empty(repo.GetAll(Acc));
    }

    [Fact]
    public void Save_new_rule_trims_and_stores_blank_as_null()
    {
        var repo = new InMemoryFilterRepository();
        var vm = new FilterRuleEditViewModel(repo, Acc) { SenderContains = " 山田 ", BodyContains = "" };
        vm.SelectedChoice = vm.Choices.Single(c => c.Action == FilterAction.MarkRead);
        Assert.True(vm.TrySave());
        Assert.Equal("", vm.ErrorText);

        var saved = Assert.Single(repo.GetAll(Acc));
        Assert.Equal("山田", saved.SenderContains);
        Assert.Null(saved.SubjectContains);
        Assert.Null(saved.BodyContains);
        Assert.True(saved.Enabled);
        Assert.Equal(FilterAction.MarkRead, saved.Action);
    }

    [Fact]
    public void Edit_prefills_and_keeps_enabled_flag_and_order()
    {
        var repo = new InMemoryFilterRepository();
        var r = repo.Save(new FilterRule { AccountId = Acc, SortOrder = 3, Enabled = false, SubjectContains = "元", Action = FilterAction.MoveToSpam });
        var vm = new FilterRuleEditViewModel(repo, Acc, r);
        Assert.True(vm.IsEdit);
        Assert.Equal("ルールを編集", vm.Title);
        Assert.Equal("元", vm.SubjectContains);
        Assert.Equal(FilterAction.MoveToSpam, vm.SelectedChoice!.Action);

        vm.SubjectContains = "新";
        Assert.True(vm.TrySave());
        var saved = Assert.Single(repo.GetAll(Acc));
        Assert.Equal("新", saved.SubjectContains);
        Assert.False(saved.Enabled);
        Assert.Equal(3, saved.SortOrder);
    }

    [Fact]
    public void Action_choices_follow_FR02_order_and_new_dialog_starts_unselected()
    {
        var vm = new FilterRuleEditViewModel(new InMemoryFilterRepository(), Acc);
        Assert.Equal(new[] { "迷惑メールへ移動", "ごみ箱へ移動", "既読にする" }, vm.Choices.Select(c => c.Text).ToArray());
        Assert.Null(vm.SelectedChoice);
        Assert.Equal("ルールを追加", vm.Title);
    }

    // ---- 一覧 ----

    [Fact]
    public void List_shows_description_and_action_in_sort_order()
    {
        var repo = new InMemoryFilterRepository();
        Add(repo, "請求書", FilterAction.MoveToSpam);
        Add(repo, "お知らせ", FilterAction.MarkRead);
        var vm = NewList(repo);

        Assert.Equal("振り分けルール - 母のメール", vm.Title);
        Assert.Equal("件名に『請求書』を含む → 迷惑メールへ", vm.Items[0].Description);
        Assert.Equal("既読にする", vm.Items[1].ActionText);
        Assert.False(vm.IsEmpty);
        Assert.True(NewList(new InMemoryFilterRepository()).IsEmpty);
    }

    [Fact]
    public void Toggling_enabled_is_saved_immediately_and_survives_edit()
    {
        var repo = new InMemoryFilterRepository();
        Add(repo, "請求書");
        var vm = NewList(repo);

        vm.Items[0].Enabled = false;
        Assert.False(repo.GetAll(Acc)[0].Enabled);

        vm.ToggleSelected();
        Assert.True(repo.GetAll(Acc)[0].Enabled);

        vm.ToggleSelected();            // 無効に戻してから編集しても、無効のまま
        var presenter = new ScriptedPresenter { Fill = e => e.SubjectContains = "請求書2" };
        var vm2 = NewList(repo, p: presenter);
        vm2.EditCommand.Execute(null);
        Assert.False(repo.GetAll(Acc)[0].Enabled);
        Assert.Equal("請求書2", repo.GetAll(Acc)[0].SubjectContains);
    }

    [Fact]
    public void Edit_after_toggle_on_same_screen_keeps_toggled_state()
    {
        var repo = new InMemoryFilterRepository();
        Add(repo, "請求書");
        var presenter = new ScriptedPresenter { Fill = e => e.SubjectContains = "変更後" };
        var vm = NewList(repo, p: presenter);

        vm.Items[0].Enabled = false;
        vm.EditCommand.Execute(null);

        var saved = repo.GetAll(Acc)[0];
        Assert.Equal("変更後", saved.SubjectContains);
        Assert.False(saved.Enabled);
    }

    [Fact]
    public void Add_via_presenter_appends_and_selects_new_rule()
    {
        var repo = new InMemoryFilterRepository();
        Add(repo, "既存");
        var presenter = new ScriptedPresenter
        {
            Fill = e => { e.SubjectContains = "新規"; e.SelectedChoice = e.Choices[1]; },
        };
        var vm = NewList(repo, p: presenter);

        vm.AddCommand.Execute(null);

        Assert.Equal(1, presenter.Shown);
        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("新規", vm.SelectedItem!.Rule.SubjectContains);
    }

    [Fact]
    public void Move_up_and_down_reorders_and_persists_and_disables_at_edges()
    {
        var repo = new InMemoryFilterRepository();
        var a = Add(repo, "A");
        var b = Add(repo, "B");
        var c = Add(repo, "C");
        var vm = NewList(repo);

        Assert.False(vm.MoveUpCommand.CanExecute(null));        // 先頭
        vm.SelectedItem = vm.Items[2];
        Assert.False(vm.MoveDownCommand.CanExecute(null));      // 末尾
        vm.MoveUpCommand.Execute(null);
        Assert.Equal(new[] { a.Id, c.Id, b.Id }, repo.GetAll(Acc).Select(r => r.Id).ToArray());
        Assert.Equal(c.Id, vm.SelectedItem!.Id);                // 動かした行を選んだまま

        vm.SelectedItem = vm.Items[0];
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(new[] { c.Id, a.Id, b.Id }, repo.GetAll(Acc).Select(r => r.Id).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, repo.GetAll(Acc).Select(r => r.SortOrder).ToArray());
    }

    [Fact]
    public void Delete_asks_confirmation_and_only_removes_on_yes()
    {
        var repo = new InMemoryFilterRepository();
        Add(repo, "A");
        var dlg = new FakeDialogService();
        var vm = NewList(repo, dlg);

        dlg.ConfirmAnswer = false;
        vm.DeleteCommand.Execute(null);
        Assert.Single(repo.GetAll(Acc));

        dlg.ConfirmAnswer = true;
        vm.DeleteCommand.Execute(null);
        Assert.Empty(repo.GetAll(Acc));
        Assert.True(vm.IsEmpty);
        Assert.False(vm.EditCommand.CanExecute(null));
    }

    [Fact]
    public void Confirm_and_button_texts_follow_ui_strings()
    {
        Assert.Equal("このルールを削除しますか?", FilterText.ConfirmQuestion);
        Assert.Equal("削除", FilterText.ConfirmYes);
        Assert.Equal("やめる", FilterText.ConfirmNo);
        Assert.Equal("ルールの削除", FilterText.ConfirmTitle);
        Assert.Equal(new[] { "追加", "編集", "削除", "上へ", "下へ", "閉じる" },
            new[] { FilterText.BtnAdd, FilterText.BtnEdit, FilterText.BtnDelete, FilterText.BtnUp, FilterText.BtnDown, FilterText.BtnClose });
        Assert.Equal(new[] { "有効", "条件の説明", "動作" }, new[] { FilterText.ColEnabled, FilterText.ColDescription, FilterText.ColAction });
        Assert.Equal(new[] { "保存", "やめる" }, new[] { FilterText.BtnSave, FilterText.BtnCancel });
    }

    // ---- ウィンドウ ----

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
    public void Windows_construct_with_expected_defaults()
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
            var repo = new InMemoryFilterRepository();
            Add(repo, "請求書");
            var scale = new FakeTextScale();

            var list = new FilterRulesWindow(NewList(repo), scale, null);
            Assert.Equal("振り分けルール - 母のメール", list.Title);
            Assert.Equal(900, list.Width);
            Assert.Equal(600, list.Height);
            Assert.Contains(Find<Button>(list), b => b.IsCancel && (string)b.Content == "閉じる");

            var edit = new FilterRuleEditWindow(new FilterRuleEditViewModel(repo, Acc), scale, null);
            Assert.Equal("ルールを追加", edit.Title);
            var buttons = Find<Button>(edit).ToList();
            Assert.Contains(buttons, b => b.IsDefault && (string)b.Content == "保存");
            Assert.Contains(buttons, b => b.IsCancel && (string)b.Content == "やめる");
            Assert.All(buttons, b => Assert.True(b.IsEnabled));   // 保存は常に押せる
        });
    }

    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T hit) yield return hit;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var x in Find<T>(child)) yield return x;
    }
}
