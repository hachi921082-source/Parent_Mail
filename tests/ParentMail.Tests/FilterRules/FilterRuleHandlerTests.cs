using ParentMail.Core;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Features.FilterRules;
using ParentMail.Core.Models;
using ParentMail.Tests.Data;
using ParentMail.Tests.Fakes;
using ParentMail.Tests.SyncCache;

namespace ParentMail.Tests.FilterRules;

// 受信規則としての動き（動作の実行、最初の一致だけ、アカウント別）と、同期エンジンに繋いだときの順序。
public class FilterRuleHandlerTests
{
    private const string Acc = "a1";

    private static MailMessage Mail(string subject = "【重要】お支払い", long id = 10)
        => new() { GmailMsgId = id, FromAddress = "taro@example.com", FromName = "山田太郎", Subject = subject,
                   BodyState = BodyState.Fetched, BodyText = "本文です" };

    private static FilterRule Rule(string subject, FilterAction action, string account = Acc)
        => new() { AccountId = account, SubjectContains = subject, Action = action };

    [Theory]
    [InlineData(FilterAction.MoveToSpam, FolderKind.Spam)]
    [InlineData(FilterAction.MoveToTrash, FolderKind.Trash)]
    public async Task Move_actions_go_through_rule_actions(FilterAction action, FolderKind target)
    {
        var repo = new InMemoryFilterRepository();
        repo.Save(Rule("お支払い", action));
        var actions = new FakeRuleActions();

        var stop = await new FilterRuleHandler(repo).ApplyAsync(Mail(id: 55), new RuleContext(Acc, actions));

        Assert.True(stop);
        Assert.Equal(new[] { (55L, target) }, actions.Moves.ToArray());
        Assert.Empty(actions.MarkedRead);
    }

    [Fact]
    public async Task MarkRead_action_marks_read_without_moving()
    {
        var repo = new InMemoryFilterRepository();
        repo.Save(Rule("お支払い", FilterAction.MarkRead));
        var actions = new FakeRuleActions();

        await new FilterRuleHandler(repo).ApplyAsync(Mail(id: 56), new RuleContext(Acc, actions));

        Assert.Equal(new[] { 56L }, actions.MarkedRead.ToArray());
        Assert.Empty(actions.Moves);
    }

    [Fact]
    public async Task Only_first_matching_rule_acts()
    {
        var repo = new InMemoryFilterRepository();
        repo.Save(Rule("お支払い", FilterAction.MoveToSpam));
        repo.Save(Rule("重要", FilterAction.MarkRead));
        var actions = new FakeRuleActions();

        await new FilterRuleHandler(repo).ApplyAsync(Mail(), new RuleContext(Acc, actions));

        Assert.Single(actions.Moves);
        Assert.Empty(actions.MarkedRead);
    }

    [Fact]
    public async Task No_match_or_other_account_does_nothing_and_continues()
    {
        var repo = new InMemoryFilterRepository();
        repo.Save(Rule("お支払い", FilterAction.MoveToTrash, account: "a2"));
        repo.Save(Rule("別の件名", FilterAction.MoveToTrash));
        var actions = new FakeRuleActions();

        var stop = await new FilterRuleHandler(repo).ApplyAsync(Mail(), new RuleContext(Acc, actions));

        Assert.False(stop);
        Assert.Empty(actions.Moves);
        Assert.Empty(actions.MarkedRead);
    }

    [Fact]
    public async Task Pipeline_runs_block_first_then_filter_and_filter_sees_only_unblocked_mail()
    {
        var blocks = new InMemoryBlockRepository();
        blocks.Add(CacheFixture.AccountId, BlockKind.Address, "taro@example.com");
        var filters = new InMemoryFilterRepository();
        filters.Save(Rule("お知らせ", FilterAction.MarkRead, CacheFixture.AccountId));
        using var h = new SyncHarness(rules: new IIncomingRule[] { new BlockRule(blocks), new FilterRuleHandler(filters) });
        h.Put(SyncHarness.Inbox, 1, 101);
        await h.RunAsync();

        // taro@example.com は拒否されるので、振り分けの「既読にする」は動かない
        h.Put(SyncHarness.Inbox, 2, 102, subject: "お知らせ");
        await h.RunAsync(SyncTrigger.Timer);

        Assert.Contains($"Move:{SyncHarness.Inbox}->{SyncHarness.Spam}", h.Gateway.Calls);
        Assert.False(h.Cache.Mail.Get(h.AccountId, 102)!.IsRead);
    }

    [Fact]
    public async Task Pipeline_filter_marks_new_inbox_mail_read_and_ignores_initial_sync()
    {
        var filters = new InMemoryFilterRepository();
        filters.Save(Rule("テスト", FilterAction.MarkRead, CacheFixture.AccountId));
        using var h = new SyncHarness(rules: new IIncomingRule[] { new FilterRuleHandler(filters) });
        h.Put(SyncHarness.Inbox, 1, 101, subject: "テスト送信（初回）");
        await h.RunAsync();
        Assert.False(h.Cache.Mail.Get(h.AccountId, 101)!.IsRead);   // 初回同期には適用しない

        h.Put(SyncHarness.Inbox, 2, 102, subject: "テストのお知らせ");
        await h.RunAsync(SyncTrigger.Timer);

        Assert.True(h.Cache.Mail.Get(h.AccountId, 102)!.IsRead);
        Assert.False(h.Cache.Mail.Get(h.AccountId, 101)!.IsRead);
    }
}
