using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ParentMail.App.Features.Compose;
using ParentMail.App.Features.Shell;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;

namespace ParentMail.App.Features.AppLifecycle;

/// <summary>開いている作成窓（ComposeWindowRegistry）を終了手順につなぐ。窓ごとに閉じる確認（compose PLAN-2）を出す。</summary>
public sealed class ComposeQuitAdapter : IQuitComposeWindows
{
    private readonly ComposeWindowRegistry _registry;

    public ComposeQuitAdapter(ComposeWindowRegistry registry) => _registry = registry;

    public int Count => _registry.Count;

    public async Task<bool> CloseAllAsync()
    {
        foreach (var window in _registry.Windows)
        {
            // どの窓の確認か分かるよう、その窓を前面に出してから聞く
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            if (!await window.RequestCloseAsync().ConfigureAwait(true)) return false;
        }
        return true;
    }
}

/// <summary>送信待ちの 3 択（WD-25 / QT-01..04）と、送信中の「送っています…」（QT-06）。</summary>
public sealed class DialogQuitPrompt : IQuitPrompt
{
    private readonly IDialogService _dialogs;
    private readonly ITextScale _scale;

    public DialogQuitPrompt(IDialogService dialogs, ITextScale scale)
    {
        _dialogs = dialogs;
        _scale = scale;
    }

    public QuitChoice AskUnsentMail()
    {
        // 既定は「戻る」。Esc・× も「戻る」（-1）
        var index = _dialogs.Choose(Strings.WD_25, Strings.QT_01,
            new[] { Strings.QT_02, Strings.QT_03, Strings.QT_04 }, defaultIndex: 2, safeIndex: 2);
        return index switch
        {
            0 => QuitChoice.SendAndClose,
            1 => QuitChoice.CloseAsIs,
            _ => QuitChoice.Back,
        };
    }

    public IDisposable ShowBusy()
    {
        var window = new Window
        {
            Title = Strings.WD_25,
            Content = new TextBlock
            {
                Text = Strings.QT_06,
                Margin = new Thickness(32),
                FontSize = _scale.FontSizeDip,
                VerticalAlignment = VerticalAlignment.Center,
            },
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false,
            Topmost = true,
        };
        // 送信中は閉じさせない（Dispose だけが閉じる）
        var allowClose = false;
        window.Closing += (_, e) => e.Cancel = !allowClose;
        window.Show();
        return new BusyHandle(() =>
        {
            allowClose = true;
            window.Close();
        });
    }

    private sealed class BusyHandle : IDisposable
    {
        private Action? _close;
        public BusyHandle(Action close) => _close = close;

        public void Dispose()
        {
            _close?.Invoke();
            _close = null;
        }
    }
}

/// <summary>終了の後片付け。同期のタイマーを止める（DB 接続などは、プロセス終了時の ServiceProvider の破棄で閉じる）。</summary>
public sealed class QuitFinalizer : IQuitFinalizer
{
    private readonly SyncCoordinator _coordinator;

    public QuitFinalizer(SyncCoordinator coordinator) => _coordinator = coordinator;

    public void FinishQuit() => _coordinator.Stop();
}

/// <summary>起動手順の実体。主窓を出し、終了確認を主窓につなぎ、同期と本文先読みを始める。</summary>
public sealed class StartupSteps : IStartupSteps
{
    private readonly IServiceProvider _services;

    public StartupSteps(IServiceProvider services) => _services = services;

    public void ShowMainWindow()
    {
        var window = (MainWindow)_services.GetService(typeof(MainWindow))!;
        var quit = (QuitCoordinator)_services.GetService(typeof(QuitCoordinator))!;
        window.QuitGuard = () => quit.RequestQuitAsync(QuitReason.MainWindowClosed);
        Application.Current.MainWindow = window;
        window.Show();
    }

    public void InitializeAccounts()
        => ((Features.AccountsAuth.AccountSwitchViewModel)_services.GetService(typeof(Features.AccountsAuth.AccountSwitchViewModel))!).Initialize();

    public void StartBackground()
    {
        ((SyncCache.SyncStartup)_services.GetService(typeof(SyncCache.SyncStartup))!).Start();
        ((SyncCache.OnDemandFetchStartup)_services.GetService(typeof(SyncCache.OnDemandFetchStartup))!).Start();
    }
}

/// <summary>Windows の終了（SessionEnding）を、同じ終了手順で同期的に処理する。</summary>
public sealed class SessionEndingHandler
{
    private readonly QuitCoordinator _quit;
    private readonly IAppLog _log;

    public SessionEndingHandler(QuitCoordinator quit, IAppLog log)
    {
        _quit = quit;
        _log = log;
    }

    /// <summary>
    /// 戻ったあとに非同期の処理は続けられないので、入れ子のメッセージループで手順の終わりを待つ。
    /// 終了してよければ e.Cancel = false、「戻る」や送信失敗なら true。
    /// </summary>
    public void Handle(SessionEndingCancelEventArgs e, Window? main)
    {
        var hwnd = main is null ? IntPtr.Zero : new System.Windows.Interop.WindowInteropHelper(main).Handle;
        var blocked = false;
        try
        {
            if (_quit.NeedsAttention) blocked = ShutdownBlocker.Create(hwnd, Strings.QT_05);
            var approved = RunSync(_quit.RequestQuitAsync(QuitReason.SessionEnding));
            e.Cancel = !approved;
            if (approved && main is MainWindow shell) shell.ApproveQuit();
        }
        catch (Exception ex)
        {
            // 終了中の例外はログだけ。Windows の終了は止めない
            _log.Error("Quit", "Unhandled exception in SessionEnding", ex);
            e.Cancel = false;
        }
        finally
        {
            if (blocked) ShutdownBlocker.Destroy(hwnd);
        }
    }

    private static T RunSync<T>(Task<T> task)
    {
        var frame = new DispatcherFrame();
        _ = task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        return task.GetAwaiter().GetResult();
    }
}
