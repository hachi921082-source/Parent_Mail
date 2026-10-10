using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.Core;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.BlockList;

/// <summary>一覧の 1 行。</summary>
public sealed class BlockRowViewModel
{
    public BlockRowViewModel(BlockEntry entry) => Entry = entry;

    public BlockEntry Entry { get; }
    public long Id => Entry.Id;
    public string KindText => Entry.Kind == BlockKind.Domain ? BlockListText.KindDomain : BlockListText.KindAddress;
    public string Value => Entry.Value;
}

// 受信拒否リストウィンドウの状態。追加・削除はすぐ保存される。
public sealed partial class BlockListViewModel : ObservableObject
{
    private readonly string _accountId;
    private readonly IBlockRepository _repo;
    private readonly BlockListEditor _editor;
    private readonly IDialogService _dialogs;

    public BlockListViewModel(string accountId, string accountName, IBlockRepository repo, IDialogService dialogs)
    {
        _accountId = accountId;
        _repo = repo;
        _editor = new BlockListEditor(repo);
        _dialogs = dialogs;
        Title = BlockListText.Title(accountName);
        Reload();
    }

    public string Title { get; }

    public ObservableCollection<BlockRowViewModel> Items { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private BlockRowViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>種類の選択。false = このアドレスだけ（既定）、true = このドメイン全体。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAddress))]
    private bool _isDomain;

    /// <summary>ラジオボタン「このアドレスだけ」用（IsDomain の裏返し）。</summary>
    public bool IsAddress
    {
        get => !IsDomain;
        set => IsDomain = !value;
    }

    [ObservableProperty]
    private string _inputText = "";

    /// <summary>赤いエラー行。空ならエラーなし。</summary>
    [ObservableProperty]
    private string _errorText = "";

    partial void OnInputTextChanged(string value) => ErrorText = "";

    public void Reload(long? selectId = null)
    {
        var keep = selectId ?? SelectedItem?.Id;
        Items.Clear();
        foreach (var e in _repo.GetAll(_accountId)) Items.Add(new BlockRowViewModel(e));
        IsEmpty = Items.Count == 0;
        SelectedItem = Items.FirstOrDefault(i => i.Id == keep) ?? Items.FirstOrDefault();
    }

    [RelayCommand]
    private void Add()
    {
        var kind = IsDomain ? BlockKind.Domain : BlockKind.Address;
        var result = _editor.Add(_accountId, kind, InputText, out var entry);
        switch (result)
        {
            case BlockAddResult.Added:
                InputText = "";
                ErrorText = "";
                Reload(entry?.Id);
                break;
            case BlockAddResult.Duplicate:
                ErrorText = BlockListText.Duplicate;
                break;
            default:
                ErrorText = BlockListText.Invalid;
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (SelectedItem is null) return;
        // 既定ボタンは「やめる」（安全側）
        var ok = _dialogs.Confirm(BlockListText.ConfirmTitle, BlockListText.ConfirmQuestion,
            BlockListText.ConfirmYes, BlockListText.ConfirmNo, safeIsDefault: true);
        if (!ok) return;

        var index = Items.IndexOf(SelectedItem);
        _repo.Remove(SelectedItem.Id);
        Reload();
        // 消した位置の次の行を選んでおく（キーボードだけで続けて操作できる）
        if (Items.Count > 0) SelectedItem = Items[Math.Min(index, Items.Count - 1)];
    }

    private bool HasSelection() => SelectedItem is not null;
}
