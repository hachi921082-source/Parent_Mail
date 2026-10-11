using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Models;

namespace ParentMail.Core.Features.Compose;

/// <summary>アドレス帳から選ぶ画面の 1 行（メールアドレス 1 つ分）。AlreadyEntered は、その欄にすでにある宛先。</summary>
public sealed record PickerRow(string Name, string Address, bool AlreadyEntered);

/// <summary>
/// アドレス帳から選ぶ画面の行を作る（compose PLAN-3）。
/// 並び順はアドレス帳と同じ（ふりがな順、ふりがなが無い人は最後に名前順）。1 つのメールアドレスにつき 1 行。
/// </summary>
public static class AddressPickerRows
{
    public static IReadOnlyList<PickerRow> Build(IEnumerable<Contact> contacts, IEnumerable<string> existingAddresses)
    {
        // 大文字・小文字は区別しない
        var existing = new HashSet<string>(
            existingAddresses.Select(a => a.Trim()).Where(a => a.Length > 0), StringComparer.OrdinalIgnoreCase);

        var rows = new List<PickerRow>();
        foreach (var contact in ContactSorter.Sort(contacts))
        {
            foreach (var email in contact.Emails)
            {
                var address = email.Trim();
                if (address.Length == 0) continue;
                rows.Add(new PickerRow(contact.Name, address, existing.Contains(address)));
            }
        }
        return rows;
    }
}
