using NSubstitute;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using ParentMail.App;
using ParentMail.App.Features.FolderActions;
using ParentMail.App.Features.ReplyForward;
using ParentMail.Core;
using ParentMail.Core.Models;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.ReplyForward;

// 入口（IReplyForwardHandler）: 本文の取り寄せ、オフラインの断り、作成窓の開き方、転送の知らせ。
public sealed class ReplyForwardHandlerTests : IDisposable
{
    private const string Acc = "acc1";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ParentMail_返信_" + Guid.NewGuid().ToString("N"));
    private readonly InMemoryMailRepository _mail = new();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly FakeBodyFetcher _body = new();
    private readonly FakeAttachmentService _attachments = new();
    private readonly FakeComposeWindowFactory _compose = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly ReplyForwardHandler _handler;

    public ReplyForwardHandlerTests()
    {
        Directory.CreateDirectory(_root);
        _accounts.Add(new Account(Acc, "me@example.com", "母のメール", 0));
        var forward = new ForwardBuilder(_mail, _attachments, new FakeAppLog());
        _handler = new ReplyForwardHandler(_mail, _accounts, _body, forward, _compose, _dialogs, new FakeAppLog());
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private void Store(bool fetched, string? text = "本文です", string? replyTo = null)
    {
        _mail.Upsert(Acc, new MailMessage
        {
            GmailMsgId = 7, MessageIdHeader = "<m7@example.com>", Subject = "ご連絡",
            FromAddress = "taro@example.com", FromName = "山田 太郎",
            To = new[] { "me@example.com", "hanako@example.com" }, DateUtc = new DateTime(2026, 10, 5, 0, 3, 0, DateTimeKind.Utc),
            BodyState = fetched ? BodyState.Fetched : BodyState.None, BodyText = fetched ? text : null,
            ReplyTo = replyTo,
        }, new[] { new MessageFolder(7, FolderKind.Inbox, 1, 1) });
    }

    [Fact]
    public async Task Reply_opens_compose_with_reply_init()
    {
        Store(fetched: true);
        await _handler.ReplyAsync(Acc, 7);

        var init = Assert.Single(_compose.Opened);
        Assert.Equal("Re: ご連絡", init.Subject);
        Assert.Equal(Acc, init.AccountId);
        Assert.Empty(init.Cc);
        Assert.Empty(_body.Requested);
    }

    [Fact]
    public async Task Open_compose_window_makes_reply_reply_all_forward_only_activate_it_without_any_work()
    {
        Store(fetched: false);
        _compose.WindowOpen = true;

        await _handler.ReplyAsync(Acc, 7);
        await _handler.ReplyAllAsync(Acc, 7);
        await _handler.ForwardAsync(Acc, 7);

        Assert.Equal(3, _compose.Activated);
        Assert.Empty(_compose.Opened);
        Assert.Empty(_body.Requested);
        Assert.Empty(_attachments.Requested);
        Assert.Empty(_dialogs.Notices);
    }

    [Fact]
    public async Task Second_request_while_first_is_still_preparing_is_ignored()
    {
        Store(fetched: false);
        var gate = new TaskCompletionSource<BodyFetchResult>();
        var body = Substitute.For<IBodyFetcher>();
        body.EnsureBodyAsync(Acc, 7, Arg.Any<CancellationToken>()).Returns(gate.Task);
        var handler = new ReplyForwardHandler(_mail, _accounts, body, new ForwardBuilder(_mail, _attachments, new FakeAppLog()),
            _compose, _dialogs, new FakeAppLog());

        var first = handler.ReplyAsync(Acc, 7);
        await handler.ReplyAsync(Acc, 7);
        await handler.ForwardAsync(Acc, 7);
        await body.Received(1).EnsureBodyAsync(Acc, 7, Arg.Any<CancellationToken>());

        _mail.Upsert(Acc, _mail.Get(Acc, 7)! with { BodyState = BodyState.Fetched, BodyText = "本文" }, new[] { new MessageFolder(7, FolderKind.Inbox, 1, 1) });
        gate.SetResult(BodyFetchResult.Ready);
        await first;

        Assert.Single(_compose.Opened);
        // 準備が終われば、次の要求は受け付ける
        await handler.ReplyAsync(Acc, 7);
        Assert.Equal(2, _compose.Opened.Count);
    }

    [Fact]
    public async Task Reply_and_reply_all_use_stored_reply_to_instead_of_from()
    {
        Store(fetched: true, replyTo: "事務局 <office@example.org>");
        await _handler.ReplyAsync(Acc, 7);
        await _handler.ReplyAllAsync(Acc, 7);

        Assert.Equal(2, _compose.Opened.Count);
        foreach (var init in _compose.Opened)
        {
            Assert.Equal(new[] { "事務局 <office@example.org>" }, init.To);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("これはアドレスではない")]
    public async Task Reply_falls_back_to_from_when_reply_to_is_missing_or_malformed(string? replyTo)
    {
        Store(fetched: true, replyTo: replyTo);
        await _handler.ReplyAsync(Acc, 7);

        Assert.Equal(new[] { "山田 太郎 <taro@example.com>" }, Assert.Single(_compose.Opened).To);
    }

    [Fact]
    public async Task Reply_all_drops_the_accounts_own_address()
    {
        Store(fetched: true);
        await _handler.ReplyAllAsync(Acc, 7);

        var init = Assert.Single(_compose.Opened);
        Assert.Equal(new[] { "hanako@example.com" }, init.Cc);
    }

    [Fact]
    public async Task Missing_body_is_fetched_first_when_online()
    {
        Store(fetched: false);
        _body.Result = BodyFetchResult.Ready;

        await _handler.ReplyAsync(Acc, 7);

        Assert.Equal(new[] { 7L }, _body.Requested);
        Assert.Single(_compose.Opened);
        Assert.Empty(_dialogs.Notices);
    }

    [Fact]
    public async Task Missing_body_while_offline_refuses_with_bn23_and_opens_nothing()
    {
        Store(fetched: false);
        _body.Result = BodyFetchResult.Offline;

        await _handler.ForwardAsync(Acc, 7);

        Assert.Empty(_compose.Opened);
        var n = Assert.Single(_dialogs.Notices);
        Assert.Equal(ParentMail.App.Resources.Strings.WD_10, n.Title);
        Assert.Equal(ParentMail.App.Resources.Strings.BN_23, n.Message);
    }

    [Fact]
    public async Task Fetch_failure_shows_generic_notice_and_opens_nothing()
    {
        Store(fetched: false);
        _body.Result = BodyFetchResult.Failed;

        await _handler.ReplyAsync(Acc, 7);

        Assert.Empty(_compose.Opened);
        Assert.Equal(ParentMail.App.Resources.Strings.BN_06, Assert.Single(_dialogs.Notices).Message);
    }

    [Fact]
    public async Task Unknown_message_does_nothing()
    {
        await _handler.ReplyAsync(Acc, 999);
        Assert.Empty(_compose.Opened);
        Assert.Empty(_dialogs.Notices);
    }

    [Fact]
    public async Task Forward_opens_compose_then_shows_notice_for_dropped_file()
    {
        Store(fetched: true);
        var file = Path.Combine(_root, "1.bin");
        File.WriteAllBytes(file, new byte[] { 1 });
        _mail.AddAttachment(Acc, new AttachmentInfo { Id = 1, GmailMsgId = 7, FileName = "危険.exe", Size = 1 });
        _attachments.Result = new AttachmentFetchResult(AttachmentFetchStatus.Ready, file);

        await _handler.ForwardAsync(Acc, 7);

        Assert.Single(_compose.Opened);
        Assert.Contains(_dialogs.Notices, n => n.Message.StartsWith("次のファイルは添付できませんでした: ") && n.Message.Contains("危険.exe"));
    }

    [Fact]
    public async Task Forward_shows_not_downloaded_notice_when_attachment_cannot_be_fetched_offline()
    {
        Store(fetched: true);
        _mail.AddAttachment(Acc, new AttachmentInfo { Id = 2, GmailMsgId = 7, FileName = "未保存.pdf", Size = 1 });
        _attachments.Result = new AttachmentFetchResult(AttachmentFetchStatus.Offline, null);

        await _handler.ForwardAsync(Acc, 7);

        var init = Assert.Single(_compose.Opened);
        Assert.Empty(init.Attachments);
        Assert.Equal(ParentMail.App.Resources.Strings.RF_06, Assert.Single(_dialogs.Notices).Message);
    }

    [Fact]
    public void Handler_is_registered_for_folder_actions()
    {
        var services = new ServiceCollection();
        foreach (var register in FeatureRegistrars.All) register(services);
        var d = services.Last(x => x.ServiceType == typeof(IReplyForwardHandler));
        Assert.NotNull(d.ImplementationFactory);
        Assert.Contains(services, s => s.ServiceType == typeof(ForwardBuilder));
    }
}
