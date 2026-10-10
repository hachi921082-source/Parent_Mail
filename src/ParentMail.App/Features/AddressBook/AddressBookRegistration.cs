using Microsoft.Extensions.DependencyInjection;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.AddressBook;

namespace ParentMail.App.Features.AddressBook;

// address-book 機能の DI 登録（FeatureRegistrars から 1 行で呼ぶ）。
// ISettingsDb は settings 機能、IAccountRepository / IDialogService / ITextScale は他機能が登録する。
public static class AddressBookRegistration
{
    public static IServiceCollection AddAddressBook(this IServiceCollection services)
    {
        services.AddSingleton<IContactRepository, ContactRepository>();
        services.AddSingleton<IContactLookup, ContactLookup>();
        services.AddSingleton<AddressBookService>();
        services.AddSingleton<IAddressBookService>(sp => sp.GetRequiredService<AddressBookService>());
        return services;
    }
}
