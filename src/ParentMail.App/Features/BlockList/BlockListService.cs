using System.Windows;
using ParentMail.Core;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.BlockList;

// 受信拒否リストの窓を開く実装。ウィンドウの所有者は、いま操作中の窓（なければメイン窓）。
public sealed class BlockListService : IBlockListService
{
    private readonly IBlockRepository _repo;
    private readonly IAccountRepository _accounts;
    private readonly IDialogService _dialogs;
    private readonly ITextScale _scale;
    private readonly BlockListEditor _editor;

    public BlockListService(IBlockRepository repo, IAccountRepository accounts, IDialogService dialogs, ITextScale scale)
    {
        _repo = repo;
        _accounts = accounts;
        _dialogs = dialogs;
        _scale = scale;
        _editor = new BlockListEditor(repo);
    }

    public void OpenWindow(string accountId)
    {
        var name = _accounts.Get(accountId)?.DisplayName ?? "";
        var vm = new BlockListViewModel(accountId, name, _repo, _dialogs);
        new BlockListWindow(vm, _scale, FindOwner()).ShowDialog();
    }

    public BlockAddResult AddFromSender(string accountId, string address, BlockKind kind)
        => _editor.AddFromSender(accountId, address, kind);

    private static Window? FindOwner()
    {
        var windows = Application.Current.Windows.OfType<Window>().Where(w => w.IsVisible).ToList();
        return windows.FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow ?? windows.FirstOrDefault();
    }
}
