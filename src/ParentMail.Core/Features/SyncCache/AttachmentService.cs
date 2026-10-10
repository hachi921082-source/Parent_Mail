using ParentMail.Core.Data;
using ParentMail.Core.Models;

namespace ParentMail.Core.Features.SyncCache;

/// <summary>
/// 添付ファイルのオンデマンド取得（sync-cache PLAN-3）。保存・開くを押したときに呼ぶ。
/// 一度ファイルにしたら通信せずに同じファイルを返す。オフラインは Offline（再接続後の自動取得はしない）。
/// </summary>
public sealed class AttachmentService : IAttachmentService
{
    private readonly ISyncCacheStore _store;
    private readonly MailRepository _mail;
    private readonly IAttachmentStore _files;
    private readonly FetchSession _session;

    public AttachmentService(ISyncCacheStore store, MailRepository mail, IAttachmentStore files, FetchSession session)
    {
        _store = store;
        _mail = mail;
        _files = files;
        _session = session;
    }

    public async Task<AttachmentFetchResult> EnsureLocalAsync(string accountId, long attachmentId, CancellationToken ct = default)
    {
        var info = _store.GetAttachment(accountId, attachmentId);
        if (info is null) return Failed;
        if (HasLocalFile(accountId, info)) return Ready(accountId, attachmentId);

        var (outcome, result) = await _session.RunAsync(accountId, async gw =>
        {
            // 待っている間に別の経路で保存済みになっていれば通信しない
            if (_files.Exists(accountId, attachmentId))
            {
                _mail.SetAttachmentPath(accountId, attachmentId, Path.Combine("attachments", attachmentId + ".bin"));
                return true;
            }

            var memberships = _mail.GetMemberships(accountId, info.GmailMsgId)
                .OrderBy(m => m.Folder == FolderKind.AllMail).ThenBy(m => (int)m.Folder).ToList();
            foreach (var m in memberships)
            {
                var path = _mail.GetImapName(accountId, m.Folder);
                if (path is null) continue;
                var stream = await gw.FetchPartAsync(path, m.Uid, info.PartId, ct).ConfigureAwait(false);
                if (stream is null) continue;
                await using (stream.ConfigureAwait(false))
                {
                    var relative = _files.SaveFile(accountId, attachmentId, stream);
                    _mail.SetAttachmentPath(accountId, attachmentId, relative);
                }
                return true;
            }
            return false;
        }, ct).ConfigureAwait(false);

        return outcome switch
        {
            FetchOutcome.Offline => new AttachmentFetchResult(AttachmentFetchStatus.Offline, null),
            FetchOutcome.Ok when result => Ready(accountId, attachmentId),
            _ => Failed,
        };
    }

    private static AttachmentFetchResult Failed => new(AttachmentFetchStatus.Failed, null);

    private AttachmentFetchResult Ready(string accountId, long attachmentId)
        => new(AttachmentFetchStatus.Ready, _files.GetFullPath(accountId, attachmentId));

    // DB に記録があっても実体が消えていたら取り直す
    private bool HasLocalFile(string accountId, AttachmentInfo info)
        => info.LocalPath is not null && _files.Exists(accountId, info.Id);
}
