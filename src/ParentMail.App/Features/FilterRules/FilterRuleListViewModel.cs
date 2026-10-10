using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.FilterRules;

/// <summary>一覧の 1 行。「有効」を切り替えるとすぐ保存される。</summary>
public sealed class FilterRuleRowViewModel : ObservableObject
{
    private readonly Action<FilterRuleRowViewModel, bool> _onToggle;
    private bool _enabled;

    public FilterRuleRowViewModel(FilterRule rule, Action<FilterRuleRowViewModel, bool> onToggle)
    {
        Rule = rule;
        _enabled = rule.Enabled;
        _onToggle = onToggle;
    }

    public FilterRule Rule { get; private set; }
    public long Id => Rule.Id;
    public string Description => RuleDescriber.Describe(Rule);
    public string ActionText => FilterText.ActionText(Rule.Action);

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            Rule = Rule with { Enabled = value };
            OnPropertyChanged();
            _onToggle(this, value);
        }
    }
}

// 振り分けルール一覧ウィンドウの状態。有効・無効の切り替え、並べ替え、削除はすぐ保存される。
public sealed partial class FilterRuleListViewModel : ObservableObject
{
    private readonly string _accountId;
    private readonly IFilterRepository _repo;
    private readonly IDialogService _dialogs;
    private readonly IFilterEditPresenter _presenter;

    public FilterRuleListViewModel(string accountId, string accountName, IFilterRepository repo,
        IDialogService dialogs, IFilterEditPresenter presenter)
    {
        _accountId = accountId;
        _repo = repo;
        _dialogs = dialogs;
        _presenter = presenter;
        Title = FilterText.Title(accountName);
        Reload();
    }

    public string Title { get; }

    public ObservableCollection<FilterRuleRowViewModel> Items { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private FilterRuleRowViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isEmpty;

    public void Reload(long? selectId = null)
    {
        var keep = selectId ?? SelectedItem?.Id;
        Items.Clear();
        foreach (var r in _repo.GetAll(_accountId)) Items.Add(new FilterRuleRowViewModel(r, OnToggle));
        IsEmpty = Items.Count == 0;
        SelectedItem = Items.FirstOrDefault(i => i.Id == keep) ?? Items.FirstOrDefault();
        NotifyMoveState();
    }

    private void OnToggle(FilterRuleRowViewModel row, bool enabled)
        => _repo.Save(row.Rule);

    /// <summary>選んでいる行の有効・無効を反転する（Space キー用）。</summary>
    public void ToggleSelected()
    {
        if (SelectedItem is { } row) row.Enabled = !row.Enabled;
    }

    [RelayCommand]
    private void Add()
    {
        var vm = new FilterRuleEditViewModel(_repo, _accountId);
        if (_presenter.ShowEdit(vm)) Reload(vm.Result?.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Edit()
    {
        if (SelectedItem is null) return;
        var vm = new FilterRuleEditViewModel(_repo, _accountId, SelectedItem.Rule);
        if (_presenter.ShowEdit(vm)) Reload(vm.Result?.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (SelectedItem is null) return;
        // 既定ボタンは「やめる」（安全側）
        var ok = _dialogs.Confirm(FilterText.ConfirmTitle, FilterText.ConfirmQuestion,
            FilterText.ConfirmYes, FilterText.ConfirmNo, safeIsDefault: true);
        if (!ok) return;

        var index = Items.IndexOf(SelectedItem);
        _repo.Remove(SelectedItem.Id);
        Reload();
        if (Items.Count > 0) SelectedItem = Items[Math.Min(index, Items.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(1);

    private void Move(int delta)
    {
        if (SelectedItem is null) return;
        var from = Items.IndexOf(SelectedItem);
        var to = from + delta;
        if (to < 0 || to >= Items.Count) return;

        var ids = Items.Select(i => i.Id).ToList();
        (ids[from], ids[to]) = (ids[to], ids[from]);
        _repo.Reorder(_accountId, ids);
        Reload(SelectedItem.Id);
    }

    private bool HasSelection() => SelectedItem is not null;
    private bool CanMoveUp() => SelectedItem is not null && Items.IndexOf(SelectedItem) > 0;
    private bool CanMoveDown() => SelectedItem is not null && Items.IndexOf(SelectedItem) < Items.Count - 1;

    private void NotifyMoveState()
    {
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }
}
