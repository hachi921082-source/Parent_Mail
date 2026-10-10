using System.Diagnostics;
using System.Text;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Tests.Data;
using ParentMail.Tests.Fakes;
using Xunit.Abstractions;

namespace ParentMail.Tests.SyncCache;

/// <summary>
/// 実際の Gmail（テスト用アカウント専用）に対する手動確認。通常の dotnet test では何もしない。
/// 実行するときだけ環境変数 PARENTMAIL_LIVE_TESTS=1 を付ける。
/// アドレスとアプリパスワードは環境変数 PARENTMAIL_TEST_USER / PARENTMAIL_TEST_APPPASSWORD から読む（値は出力しない）。
/// 出力するのは件数と経過時間だけで、件名・宛先・本文は出さない。
/// </summary>
public class LiveGmailTests(ITestOutputHelper output)
{
    private static string? Env(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v
            ? v
            : Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) is { Length: > 0 } u ? u : null;

    private static bool Enabled => Environment.GetEnvironmentVariable("PARENTMAIL_LIVE_TESTS") == "1"
                                   && Env("PARENTMAIL_TEST_USER") is not null && Env("PARENTMAIL_TEST_APPPASSWORD") is not null;

    [Fact]
    public async Task Live_InitialSync_Periodic_MirrorOfServerSideMoves_AndNewMail()
    {
        if (!Enabled) return;

        var email = Env("PARENTMAIL_TEST_USER")!;
        var account = new Account(CacheFixture.AccountId, email, "テスト", 0);
        var creds = new FakeCredentialStore();
        creds.Save(account.Id, Env("PARENTMAIL_TEST_APPPASSWORD")!);

        using var cache = new CacheFixture();      // 日本語を含む一時フォルダ
        var accounts = new InMemoryAccountRepository();
        accounts.Add(account);
        var rule = new ScriptedRule();
        var log = new FakeAppLog();
        var store = new SyncCacheStore(cache.Db);
        var sync = new SyncService(accounts, new ImapGatewayFactory(creds), cache.Mail, store, log, new SystemClockForTest(),
            new IIncomingRule[] { rule });
        var progress = 0;
        var newMails = new List<long>();
        sync.StateChanged += (_, _) => progress++;
        sync.NewMailProcessed += (_, e) => newMails.Add(e.Message.GmailMsgId);

        // 1. ログインとフォルダ一覧
        await using (var gw = new ImapGateway(account, creds))
        {
            Assert.Equal(LoginResult.Ok, await gw.TestLoginAsync());
            var kinds = (await gw.ListFoldersAsync()).Select(f => f.Kind).ToList();
            output.WriteLine("folders: " + string.Join(",", kinds));
            Assert.Contains(FolderKind.Inbox, kinds);
            Assert.Contains(FolderKind.Trash, kinds);
            Assert.Contains(FolderKind.AllMail, kinds);
        }

        // 2. 初回同期
        var sw = Stopwatch.StartNew();
        await sync.SyncNowAsync(account.Id, SyncTrigger.Startup);
        foreach (var e in log.Entries) output.WriteLine("log: " + e);   // 例外の型だけで内容は含まない
        output.WriteLine($"initial sync: {sw.Elapsed.TotalSeconds:F1}s, state={sync.GetState(account.Id)}, progress events={progress}");
        Assert.Equal(SyncState.Idle, sync.GetState(account.Id));
        Assert.True(store.IsInitialSyncDone(account.Id));
        foreach (var k in new[] { FolderKind.Inbox, FolderKind.Sent, FolderKind.Drafts, FolderKind.Trash, FolderKind.Spam })
            output.WriteLine($"{k}: {cache.Mail.GetList(account.Id, k).Count} (unread {cache.Mail.UnreadCount(account.Id, k)})");
        output.WriteLine($"total messages: {store.GetMessageIds(account.Id).Count}");
        Assert.Empty(rule.Applied);
        Assert.Empty(newMails);

        // 3. 差分同期（変化なし）
        sw.Restart();
        await sync.SyncNowAsync(account.Id, SyncTrigger.Timer);
        output.WriteLine($"periodic (no change): {sw.Elapsed.TotalSeconds:F1}s");
        Assert.Equal(SyncState.Idle, sync.GetState(account.Id));
        Assert.Empty(rule.Applied);

        // 4. テスト用アカウントへ新着を 1 通届ける（IMAP APPEND で受信トレイへ）→ 規則が 1 回だけ動く
        var raw = new MimeKit.MimeMessage();
        raw.From.Add(new MimeKit.MailboxAddress("同期テスト", email));
        raw.To.Add(new MimeKit.MailboxAddress("", email));
        raw.Subject = "同期テスト " + Guid.NewGuid().ToString("N");
        raw.Body = new MimeKit.TextPart("plain") { Text = "同期エンジンの動作確認用です。" };
        using var ms = new MemoryStream();
        raw.WriteTo(ms);

        uint appended;
        await using (var gw = new ImapGateway(account, creds))
        {
            await gw.TestLoginAsync();
            appended = (await gw.AppendAsync("INBOX", ms.ToArray(), seen: false)) ?? 0;
        }
        Assert.NotEqual(0u, appended);

        await sync.SyncNowAsync(account.Id, SyncTrigger.Timer);
        output.WriteLine($"after append: rules applied={rule.Applied.Count}, new mail events={newMails.Count}");
        Assert.Single(rule.Applied);
        Assert.Single(newMails);
        var id = newMails[0];
        Assert.Equal(BodyState.Fetched, cache.Mail.Get(account.Id, id)!.BodyState);
        Assert.Contains(cache.Mail.GetMemberships(account.Id, id), m => m.Folder == FolderKind.Inbox);

        // 5. Gmail 側でごみ箱へ移動 → 同期で所属が移る（本文は残る）
        await using (var gw = new ImapGateway(account, creds))
        {
            await gw.TestLoginAsync();
            var folders = await gw.ListFoldersAsync();
            var inbox = folders.First(f => f.Kind == FolderKind.Inbox).Path;
            var trash = folders.First(f => f.Kind == FolderKind.Trash).Path;
            var moved = await gw.MoveAsync(inbox, appended, trash);
            Assert.NotNull(moved);
        }
        await sync.SyncNowAsync(account.Id, SyncTrigger.Timer);
        var mems = cache.Mail.GetMemberships(account.Id, id);
        output.WriteLine("after server-side move: memberships=" + string.Join(",", mems.Select(m => m.Folder)));
        Assert.Contains(mems, m => m.Folder == FolderKind.Trash);
        Assert.DoesNotContain(mems, m => m.Folder == FolderKind.Inbox);
        Assert.NotNull(cache.Mail.Get(account.Id, id)!.BodyText);
        Assert.Single(rule.Applied);                      // 移動は新着ではない

        // 6. 後始末: テスト用に届けたメールをごみ箱から完全に削除
        await using (var gw = new ImapGateway(account, creds))
        {
            await gw.TestLoginAsync();
            var trash = (await gw.ListFoldersAsync()).First(f => f.Kind == FolderKind.Trash).Path;
            var uid = cache.Mail.GetMemberships(account.Id, id).First(m => m.Folder == FolderKind.Trash).Uid;
            await gw.DeleteAsync(trash, new[] { uid });
        }
        await sync.SyncNowAsync(account.Id, SyncTrigger.Timer);
        Assert.Null(cache.Mail.Get(account.Id, id));     // 完全削除も反映される
        output.WriteLine("cleanup mirrored");
    }

    private sealed class SystemClockForTest : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime LocalNow => DateTime.Now;
    }
}
