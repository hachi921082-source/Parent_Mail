using ParentMail.Core;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Models;
using ParentMail.Tests.Data;
using ParentMail.Tests.Fakes;
using ParentMail.Tests.SyncCache;

namespace ParentMail.Tests.BlockList;

// 受信規則としての動き（迷惑メールへ移動、後続を止める）と、同期エンジンに繋いだときの動き。
public class BlockRuleTests
{
    private const string Acc = "a1";

    private static MailMessage Mail(string from, long id = 10)
        => new() { GmailMsgId = id, FromAddress = from, FromName = "山田太郎", Subject = "お得なご案内" };

    private static (BlockRule rule, InMemoryBlockRepository repo, FakeRuleActions actions) Make()
    {
        var repo = new InMemoryBlockRepository();
        return (new BlockRule(repo), repo, new FakeRuleActions());
    }

    [Fact]
    public async Task Blocked_sender_is_moved_to_spam_and_stops_later_rules()
    {
        var (rule, repo, actions) = Make();
        repo.Add(Acc, BlockKind.Domain, "example.com");

        var stop = await rule.ApplyAsync(Mail("a@mail.example.com", 77), new RuleContext(Acc, actions));

        Assert.True(stop);
        Assert.Equal(new[] { (77L, FolderKind.Spam) }, actions.Moves.ToArray());
    }

    [Fact]
    public async Task Unblocked_sender_is_left_alone_and_pipeline_continues()
    {
        var (rule, repo, actions) = Make();
        repo.Add(Acc, BlockKind.Domain, "example.com");

        var stop = await rule.ApplyAsync(Mail("a@notexample.com"), new RuleContext(Acc, actions));

        Assert.False(stop);
        Assert.Empty(actions.Moves);
    }

    [Fact]
    public async Task Entries_of_another_account_do_not_apply()
    {
        var (rule, repo, actions) = Make();
        repo.Add("a2", BlockKind.Address, "a@example.com");

        Assert.False(await rule.ApplyAsync(Mail("a@example.com"), new RuleContext(Acc, actions)));
        Assert.Empty(actions.Moves);
    }

    [Fact]
    public async Task Block_rule_short_circuits_following_filter_rule_in_sync_pipeline()
    {
        var repo = new InMemoryBlockRepository();
        repo.Add(CacheFixture.AccountId, BlockKind.Domain, "example.com");
        var filter = new ScriptedRule();
        using var h = new SyncHarness(rules: new IIncomingRule[] { new BlockRule(repo), filter });
        h.Put(SyncHarness.Inbox, 1, 101);
        await h.RunAsync();                               // 初回同期: 規則は動かない
        Assert.Empty(filter.Applied);
        Assert.Contains(h.Cache.Mail.GetMemberships(h.AccountId, 101), x => x.Folder == FolderKind.Inbox);

        h.Put(SyncHarness.Inbox, 2, 102, subject: "【至急】お支払いのお願い");
        await h.RunAsync(SyncTrigger.Timer);

        Assert.Empty(filter.Applied);                     // 拒否したメールは振り分けへ渡らない
        var mems = h.Cache.Mail.GetMemberships(h.AccountId, 102);
        Assert.DoesNotContain(mems, x => x.Folder == FolderKind.Inbox);
        Assert.Contains($"Move:{SyncHarness.Inbox}->{SyncHarness.Spam}", h.Gateway.Calls);
        Assert.Empty(h.NewMails);
    }

    [Fact]
    public async Task Initial_sync_mail_is_not_moved_even_when_entry_exists()
    {
        var repo = new InMemoryBlockRepository();
        repo.Add(CacheFixture.AccountId, BlockKind.Address, "taro@example.com");
        using var h = new SyncHarness(rules: new IIncomingRule[] { new BlockRule(repo) });
        h.Put(SyncHarness.Inbox, 1, 101);

        await h.RunAsync(SyncTrigger.Startup);

        Assert.Contains(h.Cache.Mail.GetMemberships(h.AccountId, 101), x => x.Folder == FolderKind.Inbox);
        Assert.DoesNotContain(h.Gateway.Calls, c => c.StartsWith("Move:"));
    }
}
