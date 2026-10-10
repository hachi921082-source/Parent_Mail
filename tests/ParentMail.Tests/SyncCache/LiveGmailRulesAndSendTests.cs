using ParentMail.Core;
using ParentMail.Core.Features.OutboxSend;
using ParentMail.Core.Features.BlockList;
using ParentMail.Core.Features.FilterRules;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Tests.Data;
using ParentMail.Tests.Fakes;
using Xunit.Abstractions;

namespace ParentMail.Tests.SyncCache;

/// <summary>
/// 実際の Gmail（テスト用アカウント専用）に対する手動確認。通常の dotnet test では何もしない。
/// 実行するときだけ環境変数 PARENTMAIL_LIVE_TESTS=1 を付ける。
/// 確認する内容: 自分宛の SMTP 送信と受信、新着へのフィルタ規則（既読）、新着へのブロック（迷惑メールへ移動）。
/// 出力するのは件数と結果だけで、件名・宛先・本文は出さない。テストで作ったメールは最後に完全削除する。
/// </summary>
public class LiveGmailRulesAndSendTests(ITestOutputHelper output)
{
    private static string? Env(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v
            ? v
            : Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) is { Length: > 0 } u ? u : null;

    private static bool Enabled => Environment.GetEnvironmentVariable("PARENTMAIL_LIVE_TESTS") == "1"
                                   && Env("PARENTMAIL_TEST_USER") is not null && Env("PARENTMAIL_TEST_APPPASSWORD") is not null;

    private sealed class ClockForTest : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime LocalNow => DateTime.Now;
    }

    private static byte[] Raw(string email, string subject)
    {
        var m = new MimeKit.MimeMessage();
        m.From.Add(new MimeKit.MailboxAddress("ルール試験", email));
        m.To.Add(new MimeKit.MailboxAddress("", email));
        m.Subject = subject;
        m.Body = new MimeKit.TextPart("plain") { Text = "ルール動作確認用です。" };
        using var ms = new MemoryStream();
        m.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task Live_SendToSelf_FilterMarksRead_BlockMovesToSpam()
    {
        if (!Enabled) return;

        var email = Env("PARENTMAIL_TEST_USER")!;
        var account = new Account(CacheFixture.AccountId, email, "テスト", 0);
        var creds = new FakeCredentialStore();
        creds.Save(account.Id, Env("PARENTMAIL_TEST_APPPASSWORD")!);

        using var cache = new CacheFixture();
        var accounts = new InMemoryAccountRepository();
        accounts.Add(account);
        var log = new FakeAppLog();
        var blocks = new InMemoryBlockRepository();
        var filters = new InMemoryFilterRepository();
        var store = new SyncCacheStore(cache.Db);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var sync = new SyncService(accounts, new ImapGatewayFactory(creds), cache.Mail, store, log, new ClockForTest(),
            new IIncomingRule[] { new BlockRule(blocks), new FilterRuleHandler(filters) });

        var created = new List<string>();   // 後始末の対象にする件名
        async Task<MailMessage?> WaitFor(FolderKind folder, string subject, int tries = 12)
        {
            for (var i = 0; i < tries; i++)
            {
                await sync.SyncNowAsync(account.Id, SyncTrigger.Timer);
                var hit = cache.Mail.GetList(account.Id, folder).FirstOrDefault(m => m.Subject == subject);
                if (hit != null) return hit;
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            return null;
        }

        await sync.SyncNowAsync(account.Id, SyncTrigger.Startup);       // 初回同期（規則は動かない）
        Assert.True(store.IsInitialSyncDone(account.Id));

        // 1. 自分宛に SMTP 送信 → 受信トレイと送信済みに現れる
        var sendSubject = $"送信試験 {tag}";
        created.Add(sendSubject);
        var item = new OutboxItem { AccountId = account.Id, To = new[] { email }, Subject = sendSubject, Body = "自分宛の送信確認です。" };
        var smtp = new MailKitSmtpGateway(accounts, creds, log);
        var result = await smtp.SendAsync(MessageBuilder.Build(item, email, cache.Paths.AccountDir(account.Id)), account.Id);
        output.WriteLine("smtp result: " + result);
        Assert.Equal(SendResult.Ok, result);
        var received = await WaitFor(FolderKind.Inbox, sendSubject);
        output.WriteLine("received in Inbox: " + (received != null));
        Assert.NotNull(received);
        var sentCopy = cache.Mail.GetList(account.Id, FolderKind.Sent).Any(m => m.Subject == sendSubject);
        output.WriteLine("copy in Sent: " + sentCopy);

        // 2. フィルタ規則「件名に〈tag〉を含む → 既読」を新着に適用
        var filterSubject = $"フィルタ試験 {tag}";
        created.Add(filterSubject);
        filters.Save(new FilterRule { AccountId = account.Id, Enabled = true, SubjectContains = $"フィルタ試験 {tag}", Action = FilterAction.MarkRead });
        await using (var gw = new ImapGateway(account, creds))
        {
            await gw.TestLoginAsync();
            Assert.NotEqual(0u, await gw.AppendAsync("INBOX", Raw(email, filterSubject), seen: false) ?? 0);
        }
        var filtered = await WaitFor(FolderKind.Inbox, filterSubject);
        Assert.NotNull(filtered);
        output.WriteLine("filter: new mail is read = " + filtered!.IsRead);
        Assert.True(filtered.IsRead);

        // 3. ブロック（差出人アドレス）→ 新着は迷惑メールへ移動
        var blockSubject = $"ブロック試験 {tag}";
        created.Add(blockSubject);
        blocks.Add(account.Id, BlockKind.Address, email);
        await using (var gw = new ImapGateway(account, creds))
        {
            await gw.TestLoginAsync();
            Assert.NotEqual(0u, await gw.AppendAsync("INBOX", Raw(email, blockSubject), seen: false) ?? 0);
        }
        var blocked = await WaitFor(FolderKind.Spam, blockSubject);
        output.WriteLine("block: moved to Spam = " + (blocked != null));
        Assert.NotNull(blocked);
        Assert.DoesNotContain(cache.Mail.GetList(account.Id, FolderKind.Inbox), m => m.Subject == blockSubject);

        // 4. 後始末: テストで作ったメールを、ごみ箱へ移してから完全削除
        await sync.SyncNowAsync(account.Id, SyncTrigger.Timer);
        await using (var gw = new ImapGateway(account, creds))
        {
            await gw.TestLoginAsync();
            var all = await gw.ListFoldersAsync();
            var trash = all.First(f => f.Kind == FolderKind.Trash).Path;
            var toDelete = new List<uint>();
            foreach (var kind in new[] { FolderKind.Inbox, FolderKind.Sent, FolderKind.Spam })
            {
                var path = all.First(f => f.Kind == kind).Path;
                foreach (var m in cache.Mail.GetList(account.Id, kind).Where(m => created.Contains(m.Subject)).ToList())
                {
                    var uid = cache.Mail.GetMemberships(account.Id, m.GmailMsgId).First(x => x.Folder == kind).Uid;
                    var moved = await gw.MoveAsync(path, uid, trash);
                    if (moved is uint t) toDelete.Add(t);
                }
            }
            if (toDelete.Count > 0) await gw.DeleteAsync(trash, toDelete);
        }
        await sync.SyncNowAsync(account.Id, SyncTrigger.Timer);
        var left = new[] { FolderKind.Inbox, FolderKind.Sent, FolderKind.Spam, FolderKind.Trash }
            .Sum(k => cache.Mail.GetList(account.Id, k).Count(m => created.Contains(m.Subject)));
        output.WriteLine("test mails left after cleanup: " + left);
        Assert.Equal(0, left);
    }
}
