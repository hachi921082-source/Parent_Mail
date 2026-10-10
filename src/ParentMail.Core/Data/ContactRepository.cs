using Microsoft.Data.Sqlite;
using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Models;
using static ParentMail.Core.Data.DbUtil;

namespace ParentMail.Core.Data;

/// <summary>同じアカウントの別の人に、同じメールアドレスが登録済み。</summary>
public sealed class DuplicateContactEmailException : Exception
{
    public string Email { get; }
    public DuplicateContactEmailException(string email) : base("duplicate contact email") => Email = email;
}

// settings.db の contacts / contact_emails を読み書きする。アカウント削除時は FK の連鎖削除で消える。
// 同じアドレスかどうかは TextNormalizer.NormalizeKey（全角半角・大文字小文字無視）で比べる。
public sealed class ContactRepository : IContactRepository
{
    private readonly ISettingsDb _db;

    public ContactRepository(ISettingsDb db) => _db = db;

    public IReadOnlyList<Contact> GetAll(string accountId)
    {
        using var conn = _db.Open();
        return Load(conn, accountId);
    }

    public Contact Add(Contact contact)
    {
        using var conn = _db.Open();
        using var tx = Begin(conn);
        CheckDuplicates(conn, contact, excludeId: 0);
        Run(conn, "INSERT INTO contacts(account_id,name,reading,memo) VALUES($a,$n,$r,$m)",
            ("$a", contact.AccountId), ("$n", contact.Name), ("$r", contact.Reading), ("$m", contact.Memo));
        var id = Convert.ToInt64(Scalar(conn, "SELECT last_insert_rowid()"));
        InsertEmails(conn, id, contact.Emails);
        tx.Commit();
        return contact with { Id = id };
    }

    public void Update(Contact contact)
    {
        using var conn = _db.Open();
        using var tx = Begin(conn);
        CheckDuplicates(conn, contact, excludeId: contact.Id);
        Run(conn, "UPDATE contacts SET name=$n, reading=$r, memo=$m WHERE id=$i",
            ("$n", contact.Name), ("$r", contact.Reading), ("$m", contact.Memo), ("$i", contact.Id));
        Run(conn, "DELETE FROM contact_emails WHERE contact_id=$i", ("$i", contact.Id));
        InsertEmails(conn, contact.Id, contact.Emails);
        tx.Commit();
    }

    public void Delete(long id)
    {
        using var conn = _db.Open();
        Run(conn, "DELETE FROM contacts WHERE id=$i", ("$i", id)); // メールは連鎖削除
    }

    public Contact? FindByEmail(string accountId, string email)
    {
        var key = TextNormalizer.NormalizeKey(email.Trim());
        if (key.Length == 0) return null;
        return GetAll(accountId).FirstOrDefault(c => c.Emails.Any(e => TextNormalizer.NormalizeKey(e) == key));
    }

    public IReadOnlyList<Contact> Search(string accountId, string prefix)
    {
        var text = prefix.Trim();
        if (text.Length == 0) return Array.Empty<Contact>();
        var key = TextNormalizer.NormalizeKey(text);
        var kana = KanaNormalizer.ToHiraganaKey(text);
        return GetAll(accountId).Where(c =>
            KanaNormalizer.ToHiraganaKey(c.Name).StartsWith(kana, StringComparison.Ordinal) ||
            KanaNormalizer.ToHiraganaKey(c.Reading).StartsWith(kana, StringComparison.Ordinal) ||
            c.Emails.Any(e => TextNormalizer.NormalizeKey(e).StartsWith(key, StringComparison.Ordinal))).ToList();
    }

    private static void InsertEmails(SqliteConnection conn, long contactId, IReadOnlyList<string> emails)
    {
        foreach (var e in emails)
        {
            Run(conn, "INSERT INTO contact_emails(contact_id,email) VALUES($c,$e)", ("$c", contactId), ("$e", e));
        }
    }

    // 自分以外の人に同じアドレスがあれば例外。トランザクション内で呼ぶので競合しない。
    private static void CheckDuplicates(SqliteConnection conn, Contact contact, long excludeId)
    {
        var taken = new HashSet<string>();
        foreach (var c in Load(conn, contact.AccountId))
        {
            if (c.Id == excludeId) continue;
            foreach (var e in c.Emails) taken.Add(TextNormalizer.NormalizeKey(e));
        }
        foreach (var e in contact.Emails)
        {
            if (taken.Contains(TextNormalizer.NormalizeKey(e.Trim()))) throw new DuplicateContactEmailException(e);
        }
    }

    private static List<Contact> Load(SqliteConnection conn, string accountId)
    {
        var emails = new Dictionary<long, List<string>>();
        using (var cmd = Cmd(conn,
            "SELECT e.contact_id, e.email FROM contact_emails e JOIN contacts c ON c.id=e.contact_id WHERE c.account_id=$a ORDER BY e.id",
            ("$a", accountId)))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var id = r.GetInt64(0);
                if (!emails.TryGetValue(id, out var list)) emails[id] = list = new List<string>();
                list.Add(r.GetString(1));
            }
        }

        var result = new List<Contact>();
        using (var cmd = Cmd(conn, "SELECT id,name,reading,memo FROM contacts WHERE account_id=$a", ("$a", accountId)))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var id = r.GetInt64(0);
                result.Add(new Contact(id, accountId, r.GetString(1), r.GetString(2),
                    emails.TryGetValue(id, out var list) ? list : new List<string>(), r.GetString(3)));
            }
        }
        return ContactSorter.Sort(result);
    }
}
