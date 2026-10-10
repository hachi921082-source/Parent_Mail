using ParentMail.Core.Models;

namespace ParentMail.Core.Features.AddressBook;

// アドレス帳の並び順。ふりがな（ひらがな化したキーの序数比較）順、ふりがなが空の人は最後に名前順。
public static class ContactSorter
{
    public static List<Contact> Sort(IEnumerable<Contact> contacts)
    {
        return contacts
            .Select(c => (Contact: c, Reading: KanaNormalizer.ToHiraganaKey(c.Reading), Name: KanaNormalizer.ToHiraganaKey(c.Name)))
            .OrderBy(x => x.Reading.Length == 0 ? 1 : 0)
            .ThenBy(x => x.Reading, StringComparer.Ordinal)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.Contact.Id)
            .Select(x => x.Contact)
            .ToList();
    }
}
