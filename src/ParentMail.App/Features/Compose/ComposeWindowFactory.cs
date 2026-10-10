using System.Windows;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.Compose;

namespace ParentMail.App.Features.Compose;

/// <summary>
/// 作成窓を開く本物の実装（IComposeWindowFactory）。新規・返信・転送・下書きの編集・mailto: から呼ばれる。
/// 窓は非モーダルで、いくつでも開ける。呼び出しがどのスレッドでも UI スレッドで開く。
/// </summary>
public sealed class ComposeWindowFactory : IComposeWindowFactory
{
    private readonly IOutboxService _outbox;
    private readonly IDialogService _dialogs;
    private readonly IAccountRepository _accounts;
    private readonly RecipientSuggester _suggester;
    private readonly CachePaths _paths;
    private readonly IAppLog _log;
    private readonly ITextScale _scale;
    private readonly ComposeWindowRegistry _registry;

    public ComposeWindowFactory(IOutboxService outbox, IDialogService dialogs, IAccountRepository accounts,
        RecipientSuggester suggester, CachePaths paths, IAppLog log, ITextScale scale, ComposeWindowRegistry registry)
    {
        _outbox = outbox;
        _dialogs = dialogs;
        _accounts = accounts;
        _suggester = suggester;
        _paths = paths;
        _log = log;
        _scale = scale;
        _registry = registry;
    }

    public void Open(ComposeInit init)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => Open(init));
            return;
        }

        var account = _accounts.Get(init.AccountId);
        if (account is null)
        {
            _log.Warn("Compose", "作成窓を開けない: アカウントが見つからない");
            return;
        }

        var vm = new ComposeViewModel(init, account.DisplayName, _outbox, _dialogs, _suggester, _paths, _log);
        var window = new ComposeWindow(vm, _scale);
        _registry.Register(window);
        window.Show();
        window.Activate();
    }
}
