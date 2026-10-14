using System.Windows;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.Compose;

namespace ParentMail.App.Features.Compose;

/// <summary>
/// 作成窓を開く本物の実装（IComposeWindowFactory）。新規・返信・転送・下書きの編集・mailto: から呼ばれる。
/// 窓は非モーダルで、同時に 1 つだけ（開いていれば前面に出すだけ）。呼び出しがどのスレッドでも UI スレッドで開く。
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
    private readonly IDraftService? _draftService;
    private readonly IDraftRepository? _draftRows;
    private readonly DraftSyncHook? _draftHook;
    private readonly IAddressPickerService? _picker;

    public ComposeWindowFactory(IOutboxService outbox, IDialogService dialogs, IAccountRepository accounts,
        RecipientSuggester suggester, CachePaths paths, IAppLog log, ITextScale scale, ComposeWindowRegistry registry,
        IDraftService? draftService = null, IDraftRepository? draftRows = null, DraftSyncHook? draftHook = null,
        IAddressPickerService? picker = null)
    {
        _draftService = draftService;
        _draftRows = draftRows;
        _draftHook = draftHook;
        _picker = picker;
        _outbox = outbox;
        _dialogs = dialogs;
        _accounts = accounts;
        _suggester = suggester;
        _paths = paths;
        _log = log;
        _scale = scale;
        _registry = registry;
    }

    public bool TryActivateExisting()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
            return dispatcher.Invoke(TryActivateExisting);
        return _registry.TryActivateExisting();
    }

    public void Open(ComposeInit init)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => Open(init));
            return;
        }

        // 既に窓があれば前面に出すだけ（下書きの用意などの前に判定する）
        if (_registry.TryActivateExisting()) return;

        var account = _accounts.Get(init.AccountId);
        if (account is null)
        {
            _log.Warn("Compose", "作成窓を開けない: アカウントが見つからない");
            return;
        }

        // 未アップロードの下書きを同期のたびに送る仕組みが、まだ結ばれていなければここで結ぶ
        _draftHook?.Attach();

        _registry.OpenOnce(() =>
        {
            var vm = new ComposeViewModel(init, account.DisplayName, _outbox, _dialogs, _suggester, _paths, _log, _draftService, _draftRows, _picker);
            return new ComposeWindow(vm, _scale);
        });
    }
}
