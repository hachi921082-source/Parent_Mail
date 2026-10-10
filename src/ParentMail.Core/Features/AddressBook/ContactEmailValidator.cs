using System.Text.RegularExpressions;

namespace ParentMail.Core.Features.AddressBook;

// アドレス帳に登録するメールアドレスの形の確認（ASCII の user@domain.tld のみ）。
public static partial class ContactEmailValidator
{
    [GeneratedRegex(@"^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)+$")]
    private static partial Regex Shape();

    public static bool IsValid(string? address)
        => !string.IsNullOrWhiteSpace(address) && Shape().IsMatch(address.Trim());
}
