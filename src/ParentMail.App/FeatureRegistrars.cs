using Microsoft.Extensions.DependencyInjection;
using ParentMail.App.Features.AccountsAuth;
using ParentMail.App.Features.Settings;
using ParentMail.App.Features.Shell;
using ParentMail.App.SafetyWarnings;
using ParentMail.Core.Data;

namespace ParentMail.App;

// 機能ごとの DI 登録の一覧。
// 各機能は自分のフォルダに Add<Feature>(this IServiceCollection) を 1 つだけ作り、
// ここに 1 行だけ追加する（ほかのファイルは触らない）。
public static class FeatureRegistrars
{
    public static IReadOnlyList<Action<IServiceCollection>> All { get; } = new List<Action<IServiceCollection>>
    {
        s => s.AddAppShell(),
        s => s.AddSettings(),
        s => s.AddSyncCache(),
        s => s.AddSafetyWarnings(),
        s => s.AddAccountsAuth(),
        s => ParentMail.App.Features.OutboxSend.OutboxSendRegistration.AddOutboxSend(s),
        s => Features.AddressBook.AddressBookRegistration.AddAddressBook(s),
        s => s.AddSyncEngine(),
    };
}
