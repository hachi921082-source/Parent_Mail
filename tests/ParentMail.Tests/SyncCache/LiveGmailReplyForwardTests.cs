using ParentMail.App.Features.ReplyForward;
using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Tests.Data;
using ParentMail.Tests.Fakes;
using Xunit.Abstractions;

namespace ParentMail.Tests.SyncCache;

/// <summary>
/// 実際の Gmail（テスト用アカウント専用）に対する返信・転送の手動確認。通常の dotnet test では何もしない。
/// 実行するときだけ環境変数 PARENTMAIL_LIVE_TESTS=1 を付ける。
/// 確認する内容: Reply-To の保存、返信・全員に返信の宛先と引用、転送の添付（PDF は付く、.exe は断る）と本文。
/// 出力するのは件数と結果だけで、件名・宛先・本文は出さない。テストで作ったメールは最後に完全削除する。
/// </summary>
public class LiveGmailReplyForwardTests(ITestOutputHelper output)
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

    private const string ReplyToAddress = "reply-to-test@example.invalid";
    private const string BodyLine = "返信と転送の確認用の本文です。";

    private static byte[] Raw(string email, string subject, string pdfName, string exeName)
    {
        var m = new MimeKit.MimeMessage();
        m.From.Add(new MimeKit.MailboxAddress("返信試験", email));
        m.To.Add(new MimeKit.MailboxAddress("", email));
        m.ReplyTo.Add(new MimeKit.MailboxAddress("", ReplyToAddress));
        m.Subject = subject;
        var builder = new MimeKit.BodyBuilder { TextBody = BodyLine + "\r\n二行目です。" };
        builder.Attachments.Add(pdfName, System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF\n"),
            MimeKit.ContentType.Parse("application/pdf"));
        builder.Attachments.Add(exeName, new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 },
            MimeKit.ContentType.Parse("application/octet-stream"));
        m.Body = builder.ToMessageBody();
        using var ms = new MemoryStream();
        m.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task Live_ReplyReplyAllForward_UseReplyToAndAttachments()
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
        var store = new SyncCacheStore(cache.Db);
        var factory = new ImapGatewayFactory(creds);
        var sync = new SyncService(accounts, factory, cache.Mail, store, log, new ClockForTest(), Array.Empty<IIncomingRule>());

        var tag = Guid.NewGuid().ToString("N")[..8];
        var subject = $"返信転送試験 {tag}";
        var pdfName = $"請求書_{tag}.pdf";
        var exeName = $"setup_{tag}.exe";

        await sync.SyncNowAsync(account.Id, SyncTrigger.Startup);

        try
        {
            // 1. Reply-To 付きで受信トレイへ直接追加し、同期する
            await using (var gw = new ImapGateway(account, creds))
            {
                await gw.TestLoginAsync();
                Assert.NotEqual(0u, await gw.AppendAsync("INBOX", Raw(email, subject, pdfName, exeName), seen: false) ?? 0);
            }
            MailMessage? mail = null;
            for (var i = 0; i < 12 && mail == null; i++)
            {
                await sync.SyncNowAsync(account.Id, SyncTrigger.Timer);
                mail = cache.Mail.GetList(account.Id, FolderKind.Inbox).FirstOrDefault(m => m.Subject == subject);
                if (mail == null) await Task.Delay(TimeSpan.FromSeconds(5));
            }
            Assert.NotNull(mail);
            output.WriteLine("ReplyTo cached equals expected: " + (mail!.ReplyTo == ReplyToAddress));
            Assert.Equal(ReplyToAddress, mail.ReplyTo);

            // 2. 本物の部品を組み立てる（作成窓・ダイアログ・ログだけフェイク）
            var session = new FetchSession(accounts, factory, log);
            var fetcher = new BodyFetcher(cache.Mail, cache.Files, session, new PendingFetchTracker(), log);
            var attachments = new AttachmentService(store, cache.Mail, cache.Files, session);
            var compose = new FakeComposeWindowFactory();
            var dialogs = new FakeDialogService();
            var forward = new ForwardBuilder(cache.Mail, attachments, log);
            var handler = new ReplyForwardHandler(cache.Mail, accounts, fetcher, forward, compose, dialogs, log);
            var id = mail.GmailMsgId;

            // 3. 返信: 宛先は Reply-To、件名 Re: 、引用あり
            await handler.ReplyAsync(account.Id, id);
            Assert.Single(compose.Opened);
            var reply = compose.Opened[0];
            output.WriteLine($"reply: to count={reply.To.Count}, to is ReplyTo={reply.To.FirstOrDefault() == ReplyToAddress}, subject Re={reply.Subject.StartsWith("Re: ")}, quote={reply.Body.Contains(BodyLine)}");
            Assert.Equal(new[] { ReplyToAddress }, reply.To);
            Assert.StartsWith("Re: ", reply.Subject);
            Assert.Contains(BodyLine, reply.Body);
            Assert.Contains("> " + BodyLine, reply.Body);

            // 4. 全員に返信: 自分のアドレスは宛先・Cc に入らない
            await handler.ReplyAllAsync(account.Id, id);
            var all = compose.Opened[1];
            var ownInAll = all.To.Concat(all.Cc).Any(a => a.Contains(email, StringComparison.OrdinalIgnoreCase));
            output.WriteLine($"reply-all: to={all.To.Count}, cc={all.Cc.Count}, own address included={ownInAll}");
            Assert.False(ownInAll);
            Assert.Contains(ReplyToAddress, all.To);

            // 5. 転送: PDF は付き、.exe は断られて知らせに載る
            await handler.ForwardAsync(account.Id, id);
            var fwd = compose.Opened[2];
            Assert.StartsWith("Fwd: ", fwd.Subject);
            Assert.Contains(BodyLine, fwd.Body);
            Assert.Contains(ReplyForwardText.ForwardRule, fwd.Body);
            Assert.Contains(ReplyForwardText.LabelFrom, fwd.Body);
            var pdf = fwd.Attachments.FirstOrDefault(a => a.FileName == pdfName);
            Assert.NotNull(pdf);
            var pdfPath = pdf!.LocalPath!;
            if (!Path.IsPathRooted(pdfPath)) pdfPath = Path.Combine(cache.Paths.AccountDir(account.Id), pdfPath);
            var pdfReadable = File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0;
            output.WriteLine("forward: pdf accepted=True, pdf readable=" + pdfReadable);
            Assert.True(pdfReadable);

            var exeStored = cache.Mail.GetAttachments(account.Id, id).Any(a => a.FileName == exeName);
            output.WriteLine("forward: exe present in cached mail (kept by Gmail)=" + exeStored);
            if (exeStored)
            {
                var exeAccepted = fwd.Attachments.Any(a => a.FileName == exeName);
                var noticed = dialogs.Notices.Any(n => n.Message.Contains(exeName));
                output.WriteLine($"forward: exe accepted={exeAccepted}, exe named in notice={noticed}");
                Assert.False(exeAccepted);
                Assert.True(noticed);
            }
            output.WriteLine("forward: attachment count=" + fwd.Attachments.Count + ", notices=" + dialogs.Notices.Count);
        }
        finally
        {
            // 6. 後始末: テストで作ったメールを、ごみ箱へ移してから完全削除
            await sync.SyncNowAsync(account.Id, SyncTrigger.Timer);
            await using (var gw = new ImapGateway(account, creds))
            {
                await gw.TestLoginAsync();
                var folders = await gw.ListFoldersAsync();
                var trash = folders.First(f => f.Kind == FolderKind.Trash).Path;
                var toDelete = new List<uint>();
                foreach (var kind in new[] { FolderKind.Inbox, FolderKind.Sent, FolderKind.Spam })
                {
                    var path = folders.First(f => f.Kind == kind).Path;
                    foreach (var m in cache.Mail.GetList(account.Id, kind).Where(m => m.Subject == subject).ToList())
                    {
                        var uid = cache.Mail.GetMemberships(account.Id, m.GmailMsgId).First(x => x.Folder == kind).Uid;
                        if (await gw.MoveAsync(path, uid, trash) is uint t) toDelete.Add(t);
                    }
                }
                if (toDelete.Count > 0) await gw.DeleteAsync(trash, toDelete);
            }
            await sync.SyncNowAsync(account.Id, SyncTrigger.Timer);
            var left = new[] { FolderKind.Inbox, FolderKind.Sent, FolderKind.Spam, FolderKind.Trash }
                .Sum(k => cache.Mail.GetList(account.Id, k).Count(m => m.Subject == subject));
            output.WriteLine("test mails left after cleanup: " + left);
            Assert.Equal(0, left);
        }
    }
}
