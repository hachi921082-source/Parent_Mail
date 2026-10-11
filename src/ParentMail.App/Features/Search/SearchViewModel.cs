using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentMail.App.Features.MailList;
using ParentMail.Core;

namespace ParentMail.App.Features.Search;

/// <summary>
/// 一覧ペイン上部の検索ボックスの状態。検索すると一覧が結果に置き換わり、クリア・フォルダの切替・アカウントの切替で元に戻る。
/// 結果の行は ListViewModel が表示する（チェック・複数選択・プレビューは通常の一覧と同じ）。
/// </summary>
public sealed partial class SearchViewModel : ObservableObject
{
    private readonly SearchService _service;
    private readonly ListViewModel _list;
    private readonly IAppState _state;
    private readonly IAppLog? _log;
    private int _generation;

    public SearchViewModel(SearchService service, ListViewModel list, IAppState state, IAppLog? log = null)
    {
        _service = service;
        _list = list;
        _state = state;
        _log = log;
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        ClearCommand = new RelayCommand(Clear);
        // フォルダ・アカウントの切替などで一覧が読み直されたら、検索の状態も捨てる
        _list.Reloaded += (_, _) => OnListReloaded();
        // 検索の途中でフォルダやアカウントを切り替えたら、その検索の結果は捨てる
        _state.CurrentFolderChanged += (_, _) => _generation++;
        _state.CurrentAccountChanged += (_, _) => _generation++;
        _list.SearchRefreshRequested += (_, _) => _ = RefreshAsync();
    }

    /// <summary>入力欄の文字。</summary>
    [ObservableProperty] private string _query = string.Empty;

    /// <summary>結果を表示中か（固定の注意書きを出す）。</summary>
    [ObservableProperty] private bool _isActive;

    /// <summary>結果が 0 件（「見つかりませんでした」を出す）。</summary>
    [ObservableProperty] private bool _showNoResults;

    /// <summary>上限であふれた（「結果が多いため一部のみ表示しています」を出す）。</summary>
    [ObservableProperty] private bool _showTruncated;

    public IAsyncRelayCommand SearchCommand { get; }
    public IRelayCommand ClearCommand { get; }

    /// <summary>入力欄へフォーカスを移してほしい（下部の「検索」ボタン、A47）。</summary>
    public event EventHandler? FocusRequested;

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    private async Task SearchAsync()
    {
        if (_state.CurrentAccountId is not { } account) return;
        if (string.IsNullOrWhiteSpace(Query)) return;
        var gen = ++_generation;
        SearchResult result;
        try
        {
            result = await _service.SearchAsync(account, Query).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log?.Error("Search", "search failed", ex);
            return;
        }
        // 待っている間にクリアや別の検索があったら捨てる
        if (gen != _generation) return;
        _list.ShowSearchResults(result.Items);
        ApplyResult(result);
    }

    // 同期の完了や削除のあとの取り直し。選択とチェックを保つ。
    private async Task RefreshAsync()
    {
        if (!IsActive || _state.CurrentAccountId is not { } account) return;
        var gen = _generation;
        try
        {
            var result = await _service.SearchAsync(account, _shownQuery).ConfigureAwait(true);
            if (gen != _generation || !_list.IsSearchActive) return;
            _list.UpdateSearchResults(result.Items);
            ApplyResult(result);
        }
        catch (Exception ex)
        {
            _log?.Error("Search", "search refresh failed", ex);
        }
    }

    // 結果を出した時点の検索語（入力欄を書き換えても取り直しは出した語で行う）
    private string _shownQuery = string.Empty;

    private void ApplyResult(SearchResult result)
    {
        _shownQuery = Query;
        IsActive = true;
        ShowNoResults = result.Items.Count == 0;
        ShowTruncated = result.Truncated;
    }

    private void Clear()
    {
        _generation++;
        Query = string.Empty;
        if (_list.IsSearchActive) _list.Reload();
        ResetState();
    }

    private void OnListReloaded()
    {
        if (_list.IsSearchActive || !IsActive) return;
        // 結果の表示中に一覧が読み直された（フォルダ・アカウントの切替）。検索語も消す
        _generation++;
        Query = string.Empty;
        ResetState();
    }

    private void ResetState()
    {
        IsActive = false;
        ShowNoResults = false;
        ShowTruncated = false;
        _shownQuery = string.Empty;
    }
}
