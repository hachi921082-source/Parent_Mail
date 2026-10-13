using ParentMail.App.Features.FolderActions;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.ReplyForward;

/// <summary>
/// 見出しボタンと右クリックメニューからの返信・全員に返信・転送の入口（IReplyForwardHandler の本物）。
/// 本文が未取得ならまず取り寄せ、オフラインなら BN-23 で断る。初期値は ReplyBuilder / ForwardBuilder に任せ、作成窓を開く。
/// </summary>
public sealed class ReplyForwardHandler : IReplyForwardHandler
{
    private readonly IMailRepository _mail;
    private readonly IAccountRepository _accounts;
    private readonly IBodyFetcher _bodyFetcher;
    private readonly ForwardBuilder _forward;
    private readonly IComposeWindowFactory _compose;
    private readonly IDialogService _dialogs;
    private readonly IAppLog _log;

    public ReplyForwardHandler(IMailRepository mail, IAccountRepository accounts, IBodyFetcher bodyFetcher,
        ForwardBuilder forward, IComposeWindowFactory compose, IDialogService dialogs, IAppLog log)
    {
        _mail = mail;
        _accounts = accounts;
        _bodyFetcher = bodyFetcher;
        _forward = forward;
        _compose = compose;
        _dialogs = dialogs;
        _log = log;
    }

    public Task ReplyAsync(string accountId, long gmailMsgId) => ReplyCoreAsync(accountId, gmailMsgId, replyAll: false);

    public Task ReplyAllAsync(string accountId, long gmailMsgId) => ReplyCoreAsync(accountId, gmailMsgId, replyAll: true);

    private async Task ReplyCoreAsync(string accountId, long gmailMsgId, bool replyAll)
    {
        if (await EnsureSourceAsync(accountId, gmailMsgId).ConfigureAwait(true) is not { } source) return;
        var email = _accounts.Get(accountId)?.Email ?? "";
        _compose.Open(ReplyBuilder.BuildReply(accountId, source, email, replyAll));
    }

    public async Task ForwardAsync(string accountId, long gmailMsgId)
    {
        if (await EnsureSourceAsync(accountId, gmailMsgId).ConfigureAwait(true) is not { } source) return;
        var result = await _forward.BuildForwardAsync(accountId, source).ConfigureAwait(true);
        _compose.Open(result.Init);

        // 作成窓を開いたあとに、その窓の上で知らせる
        if (result.Notices.RejectedFileNames.Count > 0)
            _dialogs.ShowNotice(Strings.WD_10, ReplyForwardText.RejectedFiles(result.Notices.RejectedFileNames));
        if (result.Notices.SomeNotDownloaded)
            _dialogs.ShowNotice(Strings.WD_10, Strings.RF_06);
    }

    // 本文が取れていないときは取り寄せる。取れなければ断って null を返す
    private async Task<MailMessage?> EnsureSourceAsync(string accountId, long gmailMsgId)
    {
        var message = _mail.Get(accountId, gmailMsgId);
        if (message is null) return null;
        if (message.BodyState == BodyState.Fetched) return message;

        var fetched = await _bodyFetcher.EnsureBodyAsync(accountId, gmailMsgId).ConfigureAwait(true);
        if (fetched == BodyFetchResult.Ready) return _mail.Get(accountId, gmailMsgId) ?? message;

        if (fetched == BodyFetchResult.Offline)
        {
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_23);
        }
        else
        {
            _log.Warn("ReplyForward", "返信・転送の元の本文を取得できなかった");
            _dialogs.ShowNotice(Strings.WD_10, Strings.BN_06);
        }
        return null;
    }
}
