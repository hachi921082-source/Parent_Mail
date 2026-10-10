namespace ParentMail.Core.Features.AddressBook;

// 「アドレス帳に載っているか」の照会（プレビューの未登録通知用）。
public sealed class ContactLookup : IContactLookup
{
    private readonly IContactRepository _repo;

    public ContactLookup(IContactRepository repo) => _repo = repo;

    public bool IsKnown(string accountId, string address)
        => !string.IsNullOrWhiteSpace(address) && _repo.FindByEmail(accountId, address) is not null;
}
