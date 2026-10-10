using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.Core;
using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Features.FilterRules;

namespace ParentMail.App.Features.MailList;

/// <summary>
/// 一覧ペイン下部の 2 列 x 3 行のボタン帯。順番は 新しいメール / 検索 / 設定 / アドレス帳 / 受信拒否リスト / 振り分けルール。
/// アドレス帳・受信拒否リスト・振り分けルールは各機能の窓を開く入口（サービス）へ渡す。
/// 新しいメールは IComposeWindowFactory の契約まで。検索と設定は、受け取る機能がまだ無いので、
/// 押されたことを知らせるイベント（差し込み口）だけを用意する。
/// </summary>
public sealed partial class ListButtonStripViewModel : ObservableObject
{
    private readonly IAppState _state;
    private readonly ITextScale _scale;
    private readonly IComposeWindowFactory? _compose;
    private readonly IAddressBookService? _addressBook;
    private readonly IBlockListService? _blockList;
    private readonly IFilterRulesService? _filterRules;

    public ListButtonStripViewModel(IAppState state, ITextScale scale, IComposeWindowFactory? compose = null,
        IAddressBookService? addressBook = null, IBlockListService? blockList = null, IFilterRulesService? filterRules = null)
    {
        _state = state;
        _scale = scale;
        _compose = compose;
        _addressBook = addressBook;
        _blockList = blockList;
        _filterRules = filterRules;

        ComposeCommand = new RelayCommand(Compose, HasAccount);
        SearchCommand = new RelayCommand(() => SearchRequested?.Invoke(this, EventArgs.Empty));
        SettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        AddressBookCommand = new RelayCommand(() => WithAccount(id => _addressBook?.OpenBook(id)), HasAccount);
        BlockListCommand = new RelayCommand(() => WithAccount(id => _blockList?.OpenWindow(id)), HasAccount);
        FilterRulesCommand = new RelayCommand(() => WithAccount(id => _filterRules?.OpenWindow(id)), HasAccount);

        _state.CurrentAccountChanged += (_, _) => NotifyCanExecute();
        _scale.Changed += (_, _) => OnPropertyChanged(nameof(ChromeFontSize));
    }

    /// <summary>帯の文字サイズ (DIP)。上部バーと同じく 20 pt で頭打ち。</summary>
    public double ChromeFontSize => _scale.ChromeFontSizeDip;

    public IRelayCommand ComposeCommand { get; }
    public IRelayCommand SearchCommand { get; }
    public IRelayCommand SettingsCommand { get; }
    public IRelayCommand AddressBookCommand { get; }
    public IRelayCommand BlockListCommand { get; }
    public IRelayCommand FilterRulesCommand { get; }

    /// <summary>検索ボタンが押された。search 機能が検索ボックスへ移す処理をつなぐ（A47）。</summary>
    public event EventHandler? SearchRequested;

    /// <summary>設定ボタンが押された。設定の窓が実装されたら、その入口をつなぐ。</summary>
    public event EventHandler? SettingsRequested;

    private bool HasAccount() => _state.CurrentAccountId is not null;

    private void WithAccount(Action<string> open)
    {
        if (_state.CurrentAccountId is { } id) open(id);
    }

    // 新しいメール: 表示中のアカウントを差出人にした空の作成窓を開く契約まで。
    private void Compose()
    {
        if (_state.CurrentAccountId is not { } id) return;
        _compose?.Open(new ComposeInit { AccountId = id });
    }

    private void NotifyCanExecute()
    {
        ComposeCommand.NotifyCanExecuteChanged();
        AddressBookCommand.NotifyCanExecuteChanged();
        BlockListCommand.NotifyCanExecuteChanged();
        FilterRulesCommand.NotifyCanExecuteChanged();
    }
}
