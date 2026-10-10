using ParentMail.Core;
using ParentMail.Core.Features.Compose;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.Compose;

// 宛先の入力補完（compose PLAN-1 2 章、D17/A19）。アドレス帳が先、送信済みの宛先が後、重複は 1 件。
public class RecipientSuggesterTests
{
    private const string Acc = "acc";

    private static (RecipientSuggester Suggester, InMemoryContactRepository Contacts, InMemoryMailRepository Mail) Make()
    {
        var contacts = new InMemoryContactRepository();
        var mail = new InMemoryMailRepository();
        return (new RecipientSuggester(contacts, mail), contacts, mail);
    }

    private static void AddSent(InMemoryMailRepository mail, long id, DateTime utc, string[] to, string[]? cc = null)
        => mail.Upsert(Acc, new MailMessage { GmailMsgId = id, DateUtc = utc, To = to, Cc = cc ?? Array.Empty<string>() },
            new[] { new MessageFolder(id, FolderKind.Sent, (uint)id, 1) });

    [Fact]
    public void No_suggestions_for_empty_prefix()
    {
        var (s, _, _) = Make();
        Assert.Empty(s.Suggest(Acc, ""));
        Assert.Empty(s.Suggest(Acc, "  "));
    }

    [Fact]
    public void Contacts_match_name_reading_and_address_with_width_normalization()
    {
        var (s, c, _) = Make();
        c.Add(new Contact(0, Acc, "山田 太郎", "やまだ たろう", new[] { "taro@example.com" }, ""));
        Assert.Single(s.Suggest(Acc, "山田"));
        Assert.Single(s.Suggest(Acc, "やま"));
        Assert.Single(s.Suggest(Acc, "TARO"));
        Assert.Single(s.Suggest(Acc, "ｔａｒｏ"));
        var hit = s.Suggest(Acc, "山田")[0];
        Assert.Equal("山田 太郎 <taro@example.com>", hit.InsertText);
        Assert.Equal("taro@example.com", hit.Address);
    }

    [Fact]
    public void Contact_with_two_addresses_lists_both_when_name_matches()
    {
        var (s, c, _) = Make();
        c.Add(new Contact(0, Acc, "鈴木花子", "すずき", new[] { "h1@example.com", "h2@example.com" }, ""));
        Assert.Equal(2, s.Suggest(Acc, "鈴木").Count);
        Assert.Single(s.Suggest(Acc, "h2@"));
    }

    [Fact]
    public void Previously_used_recipients_come_after_contacts_most_recent_first_and_distinct()
    {
        var (s, c, m) = Make();
        c.Add(new Contact(0, Acc, "田中", "たなか", new[] { "tanaka@example.com" }, ""));
        AddSent(m, 1, new DateTime(2026, 1, 1), new[] { "tora@example.com" });
        AddSent(m, 2, new DateTime(2026, 3, 1), new[] { "tomo@example.com", "tanaka@example.com" });
        AddSent(m, 3, new DateTime(2026, 2, 1), new[] { "TOMO@example.com" }, new[] { "tora@example.com" });

        var result = s.Suggest(Acc, "t");
        // アドレス帳 1 件 → 新しい順の送信済み（tomo は 3 月、tora は 2 月に使用）
        Assert.Equal(new[] { "tanaka@example.com", "tomo@example.com", "tora@example.com" }, result.Select(r => r.Address));
        Assert.Equal(SuggestionSource.AddressBook, result[0].Source);
        Assert.Equal(SuggestionSource.History, result[1].Source);
    }

    [Fact]
    public void History_entries_with_display_names_match_on_name_and_keep_it()
    {
        var (s, _, m) = Make();
        AddSent(m, 1, new DateTime(2026, 1, 1), new[] { "佐藤 一郎 <ichiro@example.com>" });
        var hit = Assert.Single(s.Suggest(Acc, "佐藤"));
        Assert.Equal("ichiro@example.com", hit.Address);
        Assert.Equal("佐藤 一郎 <ichiro@example.com>", hit.InsertText);
    }

    [Fact]
    public void At_most_eight_candidates()
    {
        var (s, c, _) = Make();
        for (var i = 0; i < 12; i++) c.Add(new Contact(0, Acc, $"友だち{i}", "", new[] { $"f{i}@example.com" }, ""));
        Assert.Equal(8, s.Suggest(Acc, "友").Count);
    }

    [Fact]
    public void Other_accounts_and_non_sent_folders_are_ignored()
    {
        var (s, c, m) = Make();
        c.Add(new Contact(0, "other", "山田", "", new[] { "y@example.com" }, ""));
        m.Upsert(Acc, new MailMessage { GmailMsgId = 9, To = new[] { "yama@example.com" } },
            new[] { new MessageFolder(9, FolderKind.Inbox, 9, 1) });
        Assert.Empty(s.Suggest(Acc, "y"));
    }
}
