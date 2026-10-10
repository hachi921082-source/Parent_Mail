using System.Windows;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Features.AccountsAuth;

namespace ParentMail.App.Features.AccountsAuth;

/// <summary>
/// 同期や送信が「パスワードが使えない」と分かったときの対応（A8）。
/// 赤い帯（最優先）に「入れ直す」ボタンを出し、アプリを起動してから一度だけ、入れ直しの窓を自動で開く。
/// 両方のアカウントが失敗した場合は、1 つ目の窓を閉じてから 2 つ目を開く（重ねない）。
/// 入れ直しが成功すると ISyncService.ClearAuthFailed が呼ばれ、状態の変化で帯が消える。
/// </summary>
public sealed class AuthFailureCoordinator
{
    private readonly ISyncService _sync;
    private readonly IAuthFailureProbe _probe;
    private readonly IAccountRepository _accounts;
    private readonly AccountService _service;
    private readonly INotifier _notifier;
    private readonly IAccountDialogs _dialogs;
    private readonly Action<Action> _dispatch;
    private readonly HashSet<string> _autoOpened = new();
    private readonly Queue<string> _pending = new();
    private bool _opening;

    public AuthFailureCoordinator(ISyncService sync, IAuthFailureProbe probe, IAccountRepository accounts, AccountService service,
        INotifier notifier, IAccountDialogs dialogs, Action<Action>? dispatch = null)
    {
        _sync = sync;
        _probe = probe;
        _accounts = accounts;
        _service = service;
        _notifier = notifier;
        _dialogs = dialogs;
        _dispatch = dispatch ?? PostToUi;
    }

    // 同期のスレッドから呼ばれるので、窓は UI スレッドに任せて待たない
    private static void PostToUi(Action action)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null) action();
        else d.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>状態の変化を見張り始める。起動時に 1 回だけ呼ぶ。</summary>
    public void Attach()
    {
        _sync.StateChanged += (_, _) => Evaluate();
        _service.AccountRemoved += (_, _) => Evaluate();
        Evaluate();
    }

    /// <summary>失敗中のアカウントを調べ直し、帯と自動表示の予約を更新する。</summary>
    public void Evaluate()
    {
        var failed = _accounts.GetAll().Where(a => _probe.IsAuthFailed(a.Id)).ToList();
        if (failed.Count == 0)
        {
            _notifier.HideBanner(BannerKind.WrongPassword);
            return;
        }

        // 帯のボタンは、いま失敗している最初のアカウントの入れ直しを開く
        var first = failed[0].Id;
        _notifier.ShowBanner(BannerKind.WrongPassword, Strings.AC_16, (Strings.AC_17, () => _dispatch(() => Open(first))));

        var queued = false;
        // 同期のスレッドからも呼ばれるので、予約の更新は _pending の lock でまとめて守る
        lock (_pending)
        {
            foreach (var a in failed)
            {
                if (_autoOpened.Add(a.Id))
                {
                    _pending.Enqueue(a.Id);
                    queued = true;
                }
            }
        }
        if (queued) _dispatch(DrainQueue);
    }

    // 自動表示: 窓が開いている間は積むだけにして、閉じたら次を開く
    private void DrainQueue()
    {
        if (_opening) return;
        _opening = true;
        try
        {
            while (true)
            {
                string id;
                lock (_pending)
                {
                    if (_pending.Count == 0) break;
                    id = _pending.Dequeue();
                }
                ShowFor(id);
            }
        }
        finally
        {
            _opening = false;
        }
    }

    /// <summary>帯の「入れ直す」: 注意ダイアログは挟まずに入れ直しの窓を開く。</summary>
    private void Open(string accountId)
    {
        if (_opening) return;
        _opening = true;
        try
        {
            ShowFor(accountId);
        }
        finally
        {
            _opening = false;
        }
        // 窓が開いている間に積まれた分があれば続けて開く
        DrainQueue();
    }

    private void ShowFor(string accountId)
    {
        // 窓を開く前に、すでに入れ直し済み・削除済みなら何もしない
        if (!_probe.IsAuthFailed(accountId) || _accounts.Get(accountId) is not { } account) return;
        _dialogs.ShowReenter(new ReenterPasswordViewModel(_service, account));
    }
}
