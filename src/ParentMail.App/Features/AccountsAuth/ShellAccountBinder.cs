using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using ParentMail.App.Features.Shell;
using ParentMail.Core.Features.AccountsAuth;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>
/// 主窓の上部バー（app-shell の MainViewModel）に、アカウントボタン（Items）をつなぐ。
/// 上部バーの XAML はそのまま使い、MainViewModel の AccountButtons にだけ触れる。
/// 起動直後にアカウントが 0 件なら、初回起動としてアカウント追加の窓を出す。
/// </summary>
public sealed class ShellAccountBinder
{
    private readonly AccountSwitchViewModel _switch;
    private readonly AccountService _service;
    private readonly AccountAddLauncher _launcher;

    public ShellAccountBinder(AccountSwitchViewModel switchVm, AccountService service, AccountAddLauncher launcher)
    {
        _switch = switchVm;
        _service = service;
        _launcher = launcher;
    }

    public void Attach(MainViewModel shell)
    {
        void Push()
        {
            shell.AccountButtons = _switch.Items;
            shell.AccountButtonMaxWidth = _switch.ButtonMaxWidthDip;
        }

        _switch.PropertyChanged += (_, _) => Push();
        _service.AccountAdded += (_, _) => RunOnUi(_switch.Refresh);
        // 名前変更・削除でもアカウントボタンと、現在のアカウント（削除で残る方へ）を取り直す
        _service.AccountRenamed += (_, _) => RunOnUi(_switch.Refresh);
        _service.AccountRemoved += (_, _) => RunOnUi(_switch.Refresh);

        // 主窓の表示が済んでから初期化する（初回起動の窓は主窓より前に出す）
        RunOnUi(() =>
        {
            _switch.Initialize();
            Push();
            if (_switch.Items.Count == 0 && _launcher.ShowAddWindow(firstLaunch: true))
            {
                _switch.Refresh();
            }
        }, DispatcherPriority.Loaded);
    }

    private static void RunOnUi(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) action();
        else dispatcher.BeginInvoke(action, priority);
    }
}
