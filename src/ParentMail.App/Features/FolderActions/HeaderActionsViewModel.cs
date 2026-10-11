using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.App.Features.MailList;

namespace ParentMail.App.Features.FolderActions;

/// <summary>
/// プレビュー見出しのボタン（返信・全員に返信・転送／編集／削除）の状態とコマンド。
/// 見ているフォルダ・選択行・チェックの数から HeaderButtonState を作り直して表示に反映する。
/// ボタンの押下は FolderActionController へ渡すだけで、判断はそちらにある。
/// </summary>
public sealed partial class HeaderActionsViewModel : ObservableObject
{
    private readonly FolderActionController _controller;
    private readonly ListViewModel _list;

    public HeaderActionsViewModel(FolderActionController controller, ListViewModel list)
    {
        _controller = controller;
        _list = list;
        ReplyCommand = new AsyncRelayCommand(() => InvokeOnSelected(FolderActionKind.Reply), () => State.ReplyEnabled);
        ReplyAllCommand = new AsyncRelayCommand(() => InvokeOnSelected(FolderActionKind.ReplyAll), () => State.ReplyAllEnabled);
        ForwardCommand = new AsyncRelayCommand(() => InvokeOnSelected(FolderActionKind.Forward), () => State.ForwardEnabled);
        EditCommand = new AsyncRelayCommand(() => InvokeOnSelected(FolderActionKind.Edit), () => State.EditEnabled);
        DeleteCommand = new AsyncRelayCommand(() => _controller.DeleteAsync(null), () => State.DeleteEnabled);

        _list.SelectionChanged += (_, _) => Update();
        _list.ChecksChanged += (_, _) => Update();
        _list.Reloaded += (_, _) => Update();
        Update();
    }

    /// <summary>いまのボタンの状態。</summary>
    public HeaderButtonState State { get; private set; } = HeaderButtonState.For(ParentMail.Core.Models.FolderKind.Inbox, false, 0);

    public bool ShowReplyGroup => State.ShowReplyGroup;
    public bool ShowEdit => State.ShowEdit;
    public string DeleteLabel => State.DeleteLabel;

    public IAsyncRelayCommand ReplyCommand { get; }
    public IAsyncRelayCommand ReplyAllCommand { get; }
    public IAsyncRelayCommand ForwardCommand { get; }
    public IAsyncRelayCommand EditCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }

    /// <summary>表示とコマンドの有効・無効を作り直す。</summary>
    public void Update()
    {
        State = HeaderButtonState.For(_list.Folder, _list.SelectedItem is not null, _list.CheckedCount);
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(ShowReplyGroup));
        OnPropertyChanged(nameof(ShowEdit));
        OnPropertyChanged(nameof(DeleteLabel));
        ReplyCommand.NotifyCanExecuteChanged();
        ReplyAllCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private Task InvokeOnSelected(FolderActionKind kind)
        => _list.SelectedItem is { } row ? _controller.InvokeAsync(kind, row) : Task.CompletedTask;
}
