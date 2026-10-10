using ParentMail.Core.Models;

namespace ParentMail.Core.Features.AccountsAuth;

/// <summary>
/// 現在のアカウント・フォルダ・選択メールの実装（IAppState）。
/// アカウントを切り替えると、フォルダは受信トレイに戻り、選択は解除される。
/// 先に状態をそろえてから CurrentAccountChanged を出し、続けてフォルダ・選択の変更を通知する。
/// </summary>
public sealed class AppState : IAppState
{
    private string? _account;
    private FolderKind _folder = FolderKind.Inbox;
    private long? _selected;

    public string? CurrentAccountId
    {
        get => _account;
        set
        {
            if (_account == value) return;
            var folderChanged = _folder != FolderKind.Inbox;
            var selectionChanged = _selected is not null;
            _account = value;
            _folder = FolderKind.Inbox;
            _selected = null;
            CurrentAccountChanged?.Invoke(this, EventArgs.Empty);
            if (folderChanged) CurrentFolderChanged?.Invoke(this, EventArgs.Empty);
            if (selectionChanged) SelectedMessageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public FolderKind CurrentFolder
    {
        get => _folder;
        set
        {
            if (_folder == value) return;
            _folder = value;
            CurrentFolderChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public long? SelectedMessageId
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            SelectedMessageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CurrentAccountChanged;
    public event EventHandler? CurrentFolderChanged;
    public event EventHandler? SelectedMessageChanged;
}
