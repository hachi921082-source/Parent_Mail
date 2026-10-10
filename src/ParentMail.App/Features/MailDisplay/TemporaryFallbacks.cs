using ParentMail.Core;

namespace ParentMail.App.Features.MailDisplay;

// 本文取得（sync-cache PLAN-3）・添付取得・作成窓（compose PLAN-1）が入るまでの仮実装。
// TryAdd で登録するので、本物の登録があればそちらが使われる。契約（Core の Abstractions）にだけ従う。

internal sealed class UnavailableBodyFetcher : IBodyFetcher
{
    public Task<BodyFetchResult> EnsureBodyAsync(string accountId, long gmailMsgId, CancellationToken ct = default)
        => Task.FromResult(BodyFetchResult.Failed);
}

internal sealed class UnavailableAttachmentService : IAttachmentService
{
    public Task<AttachmentFetchResult> EnsureLocalAsync(string accountId, long attachmentId, CancellationToken ct = default)
        => Task.FromResult(new AttachmentFetchResult(AttachmentFetchStatus.Failed, null));
}

internal sealed class UnavailableComposeWindowFactory : IComposeWindowFactory
{
    public void Open(ComposeInit init)
    {
        // 作成窓ができるまでは何もしない
    }
}
