using System.Windows;
using ParentMail.Core;
using ParentMail.Core.Features.FilterRules;

namespace ParentMail.App.Features.FilterRules;

// 振り分けルールの窓を開く実装。ウィンドウの所有者は、いま操作中の窓（なければメイン窓）。
public sealed class FilterRulesService : IFilterRulesService, IFilterEditPresenter
{
    private readonly IFilterRepository _repo;
    private readonly IAccountRepository _accounts;
    private readonly IDialogService _dialogs;
    private readonly ITextScale _scale;

    public FilterRulesService(IFilterRepository repo, IAccountRepository accounts, IDialogService dialogs, ITextScale scale)
    {
        _repo = repo;
        _accounts = accounts;
        _dialogs = dialogs;
        _scale = scale;
    }

    public void OpenWindow(string accountId)
    {
        var name = _accounts.Get(accountId)?.DisplayName ?? "";
        var vm = new FilterRuleListViewModel(accountId, name, _repo, _dialogs, this);
        new FilterRulesWindow(vm, _scale, FindOwner()).ShowDialog();
    }

    public bool ShowEdit(FilterRuleEditViewModel vm)
        => new FilterRuleEditWindow(vm, _scale, FindOwner()).ShowDialog() == true;

    private static Window? FindOwner()
    {
        var windows = Application.Current.Windows.OfType<Window>().Where(w => w.IsVisible).ToList();
        return windows.FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow ?? windows.FirstOrDefault();
    }
}
