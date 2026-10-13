using ParentMail.Core.Models;

namespace ParentMail.Core;

// IMAP / SMTP ゲートウェイの契約。アカウントごとに 1 インスタンス。

/// <summary>IMAP のフォルダ状態。</summary>
public sealed record ImapFolderStatus(string FolderPath, uint UidValidity, uint UidNext, ulong? HighestModSeq, int MessageCount);

/// <summary>IMAP から取得したヘッダ情報。</summary>
public sealed record ImapHeader(uint Uid, long GmailMsgId, string MessageIdHeader, string? References, string Subject, string FromAddress, string FromName,
    IReadOnlyList<string> To, IReadOnlyList<string> Cc, DateTime DateUtc, bool IsRead, bool HasAttachments, string? ReplyTo = null);

/// <summary>IMAP から取得した本文。</summary>
public sealed record ImapBody(uint Uid, string? Text, string? Html, IReadOnlyList<AttachmentInfo> Attachments);

/// <summary>special-use 属性つきのフォルダ一覧の 1 件。Kind が null のものは対象外。</summary>
public sealed record ImapFolderInfo(string Path, FolderKind? Kind);

/// <summary>ログイン確認の結果。</summary>
public enum LoginResult { Ok, AuthFailed, NetworkError }

public interface IImapGateway : IAsyncDisposable
{
    Task<LoginResult> TestLoginAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ImapFolderInfo>> ListFoldersAsync(CancellationToken ct = default);
    Task<ImapFolderStatus> GetFolderStatusAsync(string folderPath, CancellationToken ct = default);
    Task<IReadOnlyList<uint>> SearchUidsAsync(string folderPath, DateTime? sinceUtc, CancellationToken ct = default);
    Task<IReadOnlyList<ImapHeader>> FetchHeadersAsync(string folderPath, IReadOnlyList<uint> uids, CancellationToken ct = default);
    Task<ImapBody?> FetchBodyAsync(string folderPath, uint uid, CancellationToken ct = default);
    Task<Stream?> FetchPartAsync(string folderPath, uint uid, string partId, CancellationToken ct = default);
    Task SetSeenAsync(string folderPath, IReadOnlyList<uint> uids, bool seen, CancellationToken ct = default);
    /// <summary>移動後の UID を返す（取得できない場合は null）。</summary>
    Task<uint?> MoveAsync(string fromFolderPath, uint uid, string toFolderPath, CancellationToken ct = default);
    /// <summary>下書きなどを追加し、追加先 UID を返す。</summary>
    Task<uint?> AppendAsync(string folderPath, byte[] rawMessage, bool seen, CancellationToken ct = default);
    Task DeleteAsync(string folderPath, IReadOnlyList<uint> uids, CancellationToken ct = default);
}

/// <summary>IMAP ゲートウェイをアカウントごとに作る工場。</summary>
public interface IImapGatewayFactory
{
    IImapGateway Create(Account account);
}

/// <summary>SMTP 送信の結果。AuthFailed は失敗回数に数えず、パスワード入れ直しの流れへ回す。</summary>
public enum SendResult { Ok, AuthFailed, Transient, Permanent }

public interface ISmtpGateway
{
    /// <summary>組み立て済みのメッセージを accountId のアカウントで送る（outbox-send PLAN 3 章）。</summary>
    Task<SendResult> SendAsync(MimeKit.MimeMessage message, string accountId, CancellationToken ct = default);
}
