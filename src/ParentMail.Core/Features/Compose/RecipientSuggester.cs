using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Models;

namespace ParentMail.Core.Features.Compose;

/// <summary>候補の出どころ。</summary>
public enum SuggestionSource { AddressBook, History }

/// <summary>入力補完の候補 1 件。InsertText を宛先欄に入れる。</summary>
public sealed record Suggestion(string DisplayName, string Address, string InsertText, SuggestionSource Source);

/// <summary>
/// 宛先の入力補完（compose PLAN-1 2 章、D17/A19）。
/// アドレス帳（名前・読み・アドレスの前方一致）を先に、次にそのアカウントの送信済みメールで使った宛先を新しい順に並べ、
/// アドレス（全角半角・大文字小文字を無視）が同じものは 1 件にして、最大 8 件を返す。
/// </summary>
public sealed class RecipientSuggester
{
    public const int MaxCandidates = 8;

    // 送信済みを何通まで見るか（古いものまで舐めない）
    private const int HistoryScanLimit = 300;

    private readonly IContactRepository _contacts;
    private readonly IMailRepository _mail;

    public RecipientSuggester(IContactRepository contacts, IMailRepository mail)
    {
        _contacts = contacts;
        _mail = mail;
    }

    public IReadOnlyList<Suggestion> Suggest(string accountId, string prefix)
    {
        var text = (prefix ?? "").Trim();
        if (text.Length == 0) return Array.Empty<Suggestion>();

        var key = TextNormalizer.NormalizeKey(text);
        var kana = KanaNormalizer.ToHiraganaKey(text);
        var seen = new HashSet<string>();
        var result = new List<Suggestion>();

        bool Add(string name, string address, SuggestionSource source)
        {
            if (!seen.Add(TextNormalizer.NormalizeKey(address))) return true;
            result.Add(new Suggestion(name, address, RecipientParser.Format(name, address), source));
            return result.Count < MaxCandidates;
        }

        foreach (var c in _contacts.Search(accountId, text))
        {
            // 名前か読みに当たったときは全アドレス、アドレスに当たったときはそのアドレスだけ
            var byName = KanaNormalizer.ToHiraganaKey(c.Name).StartsWith(kana, StringComparison.Ordinal)
                || KanaNormalizer.ToHiraganaKey(c.Reading).StartsWith(kana, StringComparison.Ordinal);
            foreach (var email in c.Emails)
            {
                if (!byName && !TextNormalizer.NormalizeKey(email).StartsWith(key, StringComparison.Ordinal)) continue;
                if (!Add(c.Name, email, SuggestionSource.AddressBook)) return result;
            }
        }

        foreach (var message in _mail.GetList(accountId, FolderKind.Sent, HistoryScanLimit))
        {
            foreach (var token in RecipientParser.Parse(string.Join(", ", message.To.Concat(message.Cc))))
            {
                if (!token.IsValid || token.Address is null) continue;
                var nameKey = TextNormalizer.NormalizeKey(token.Name);
                var hit = TextNormalizer.NormalizeKey(token.Address).StartsWith(key, StringComparison.Ordinal)
                    || (nameKey.Length > 0 && nameKey.StartsWith(key, StringComparison.Ordinal));
                if (hit && !Add(token.Name ?? "", token.Address, SuggestionSource.History)) return result;
            }
        }
        return result;
    }
}
