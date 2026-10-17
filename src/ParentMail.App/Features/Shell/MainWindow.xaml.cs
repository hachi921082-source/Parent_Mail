using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParentMail.App.Services;
using ParentMail.Core;

namespace ParentMail.App.Features.Shell;

// メイン画面。スロットへのビュー差し込みは各機能が行う。
// ここでは窓の大きさ・位置・分割幅の保存と復元、前面表示を受け持つ。
public partial class MainWindow : Window
{
    private readonly ISettingsStore _settings;
    private readonly MainViewModel _viewModel;
    private WindowState _lastNonMinimizedState = WindowState.Normal;

    public MainWindow(MainViewModel viewModel, ISettingsStore settings, IAppLog? log = null)
    {
        _log = log;
        _viewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;
        InitializeComponent();

        MinWidth = Constants.MinWindowWidth;
        MinHeight = Constants.MinWindowHeight;
        WindowRules.BindRefreshKey(this, viewModel.RefreshCommand);

        RestoreWindowState();
        StateChanged += (_, _) =>
        {
            if (WindowState != WindowState.Minimized) _lastNonMinimizedState = WindowState;
        };
        Splitter.DragCompleted += (_, _) => SaveWindowState();
        Closing += OnClosing;
    }

    /// <summary>
    /// 窓を閉じる前の終了確認（app-lifecycle が設定する）。true を返したら閉じてよい。
    /// 未設定なら確認なしで閉じる。
    /// </summary>
    public Func<Task<bool>>? QuitGuard { get; set; }

    private bool _quitApproved;
    private QuitCloseRunner? _quitRunner;
    private readonly IAppLog? _log;

    /// <summary>終了が承認済みであることを知らせる（Windows の終了の承認後など、再確認させないため）。</summary>
    public void ApproveQuit() => _quitApproved = true;

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveWindowState();
        // 確認済み、確認の仕組みが無い、アプリがすでに終了処理中のときは、そのまま閉じる
        if (_quitApproved || QuitGuard is null || Application.Current?.Dispatcher.HasShutdownStarted == true) return;

        // いったん止めて確認し、続けてよければ改めて閉じる
        e.Cancel = true;
        _ = ConfirmQuitAndCloseAsync();
    }

    private Task ConfirmQuitAndCloseAsync()
    {
        // 確認・例外の記録・Closing の外での Close() は QuitCloseRunner が受け持つ
        _quitRunner ??= new QuitCloseRunner(() => QuitGuard!(), ApproveQuit, Close, _log);
        return _quitRunner.RunAsync();
    }

    /// <summary>2 回目の起動などから呼ばれ、窓を元の状態に戻して前面に出す。</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = _lastNonMinimizedState;
        // 別アプリが前面にいても出るよう、一瞬だけ最前面にしてから戻す。
        Topmost = true;
        Activate();
        Topmost = false;
        Focus();
    }

    // 初回: 最大化 + 一覧ペイン約 35%（XAML の初期比率）。2 回目以降は前回の大きさ・位置・状態・分割幅。
    private void RestoreWindowState()
    {
        var s = _settings.Get();
        if (s.WindowWidth is null || s.WindowHeight is null)
        {
            WindowState = WindowState.Maximized;
            return;
        }

        Width = Math.Max(s.WindowWidth.Value, Constants.MinWindowWidth);
        Height = Math.Max(s.WindowHeight.Value, Constants.MinWindowHeight);

        if (s.WindowLeft is { } left && s.WindowTop is { } top && IsOnScreen(left, top, Width, Height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        else
        {
            // 画面外に出てしまう位置は使わず、画面の中央に出す。
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (Enum.TryParse<WindowState>(s.WindowState, out var state) && state != WindowState.Minimized)
        {
            WindowState = state;
        }

        if (s.SplitterWidth is { } splitter && splitter >= ListColumn.MinWidth)
        {
            ListColumn.Width = new GridLength(Math.Min(splitter, Math.Max(Width - 300 - 6, ListColumn.MinWidth)));
        }
    }

    // 窓の上辺付近（タイトルバー）が仮想画面の中に入っているかで判定する。
    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var titleBar = new Rect(left, top, width, 48);
        return screen.IntersectsWith(titleBar) && screen.Contains(new Point(left + Math.Min(width, 200), top + 10));
    }

    private void SaveWindowState()
    {
        try
        {
            // 最大化・最小化中は元の大きさ（RestoreBounds）を保存する。
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            var state = WindowState == WindowState.Minimized ? _lastNonMinimizedState : WindowState;
            _settings.Save(_settings.Get() with
            {
                WindowWidth = bounds.Width,
                WindowHeight = bounds.Height,
                WindowLeft = bounds.Left,
                WindowTop = bounds.Top,
                WindowState = state.ToString(),
                SplitterWidth = ListColumn.ActualWidth,
            });
        }
        catch
        {
            // 保存に失敗しても終了や操作は止めない。
        }
    }
}
