using System.Windows;
using System.Windows.Threading;
using ParentMail.App.Resources;
using ParentMail.Core;

namespace ParentMail.App.Services;

/// <summary>
/// 全体例外処理（app-shell PLAN 3.5）。例外の種類・メッセージ・スタックだけをログに書き、
/// モーダル通知 [BN-04] を出す。致命的でない例外ではアプリを続行する。
/// </summary>
public sealed class GlobalErrorHandler
{
    private readonly IAppLog _log;
    private readonly IDialogService _dialogs;
    private bool _showing;

    public GlobalErrorHandler(IAppLog log, IDialogService dialogs)
    {
        _log = log;
        _dialogs = dialogs;
    }

    public void Attach(Application app)
    {
        app.DispatcherUnhandledException += OnDispatcherUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
        TaskScheduler.UnobservedTaskException += OnUnobservedTask;
    }

    private void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Report("Dispatcher", e.Exception);
        e.Handled = true;
    }

    private void OnDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
        => Report("AppDomain", e.ExceptionObject as Exception);

    private void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Report("Task", e.Exception);
        e.SetObserved();
    }

    private void Report(string area, Exception? ex)
    {
        _log.Error(area, "unhandled exception", ex);

        // 通知の表示中にさらに例外が出ても、通知を重ねない。
        if (_showing) return;
        _showing = true;
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null) return;
            if (dispatcher.CheckAccess()) _dialogs.ShowNotice(Strings.WD_30, Strings.BN_04);
            else dispatcher.Invoke(() => _dialogs.ShowNotice(Strings.WD_30, Strings.BN_04));
        }
        catch (Exception inner)
        {
            _log.Error(area, "failed to show error notice", inner);
        }
        finally
        {
            _showing = false;
        }
    }
}
