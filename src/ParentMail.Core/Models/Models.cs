namespace ParentMail.Core.Models;

// 共有データモデル（docs/plans/README.md 5 章）。各機能はこの型だけを介してやり取りする。

/// <summary>アカウント。Id は Guid 文字列。</summary>
public sealed record Account(string Id, string Email, string DisplayName, int SortOrder);

/// <summary>フォルダ種別。Outbox はローカル専用、AllMail は画面として表示しない。</summary>
public enum FolderKind { Inbox, Outbox, Sent, Trash, Drafts, Spam, AllMail }

/// <summary>本文の取得状態。</summary>
public enum BodyState { None, Fetched }

/// <summary>返信・転送のスレッド用ヘッダ（In-Reply-To と References）。</summary>
public sealed record ReplyHeaders(string? InReplyTo, string? References);

/// <summary>メール 1 通。GmailMsgId は X-GM-MSGID。</summary>
public sealed record MailMessage
{
    public long GmailMsgId { get; init; }
    public string MessageIdHeader { get; init; } = "";
    /// <summary>References ヘッダの値（返信時に引き継ぐ）。</summary>
    public string? References { get; init; }
    /// <summary>Reply-To ヘッダの先頭の 1 件（「名前 &lt;アドレス&gt;」形式）。無い・読めないときは null（返信は From 宛て）。</summary>
    public string? ReplyTo { get; init; }
    public string Subject { get; init; } = "";
    public string FromAddress { get; init; } = "";
    public string FromName { get; init; } = "";
    public IReadOnlyList<string> To { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();
    public DateTime DateUtc { get; init; }
    public bool IsRead { get; init; }
    public bool HasAttachments { get; init; }
    public BodyState BodyState { get; init; }
    public string? BodyText { get; init; }
    public string? BodyHtml { get; init; }
    public string? DerivedText { get; init; }
}

/// <summary>フォルダ所属（1 通が複数フォルダに属する）。</summary>
public sealed record MessageFolder(long GmailMsgId, FolderKind Folder, uint Uid, uint UidValidity);

/// <summary>添付ファイル情報。実体は ID 名のファイルで保存し、元の名前は DB にだけ持つ。</summary>
public sealed record AttachmentInfo
{
    public long Id { get; init; }
    public long GmailMsgId { get; init; }
    public string PartId { get; init; } = "";
    public string FileName { get; init; } = "";
    public string ContentType { get; init; } = "";
    public long Size { get; init; }
    public bool IsInline { get; init; }
    public string? ContentId { get; init; }
    public string? LocalPath { get; init; }
}

/// <summary>送信箱の 1 件。</summary>
public sealed record OutboxItem
{
    public long Id { get; init; }
    public string AccountId { get; init; } = "";
    public IReadOnlyList<string> To { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Bcc { get; init; } = Array.Empty<string>();
    public string Subject { get; init; } = "";
    public string Body { get; init; } = "";
    public IReadOnlyList<AttachmentInfo> Attachments { get; init; } = Array.Empty<AttachmentInfo>();
    public int RetryCount { get; init; }
    public bool FailureNotified { get; init; }
    public DateTime CreatedUtc { get; init; }
    public long? SourceDraftId { get; init; }
    public ReplyHeaders? ReplyHeaders { get; init; }
}

/// <summary>下書き。項目は OutboxItem と同じ。</summary>
public sealed record DraftItem
{
    public long LocalId { get; init; }
    public string AccountId { get; init; } = "";
    public IReadOnlyList<string> To { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Bcc { get; init; } = Array.Empty<string>();
    public string Subject { get; init; } = "";
    public string Body { get; init; } = "";
    public IReadOnlyList<AttachmentInfo> Attachments { get; init; } = Array.Empty<AttachmentInfo>();
    public uint? GmailUid { get; init; }
    public bool Dirty { get; init; }
    public ReplyHeaders? ReplyHeaders { get; init; }
}

/// <summary>作成ウィンドウの内容の写し。下書き保存と送信箱への積み込みで共通に使う。</summary>
public sealed record DraftSnapshot
{
    /// <summary>ローカル下書き行の ID（新規なら null）。</summary>
    public long? DraftId { get; init; }
    public string AccountId { get; init; } = "";
    public IReadOnlyList<string> To { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Bcc { get; init; } = Array.Empty<string>();
    public string Subject { get; init; } = "";
    public string Body { get; init; } = "";
    public IReadOnlyList<AttachmentInfo> Attachments { get; init; } = Array.Empty<AttachmentInfo>();
    public ReplyHeaders? ReplyHeaders { get; init; }
}

/// <summary>フォルダごとの同期状態（UIDVALIDITY など）。</summary>
public sealed record FolderSyncState(FolderKind Folder, uint UidValidity, uint UidNext, ulong? HighestModSeq);

/// <summary>アドレス帳の連絡先。</summary>
public sealed record Contact(long Id, string AccountId, string Name, string Reading, IReadOnlyList<string> Emails, string Memo);

/// <summary>拒否の種別。</summary>
public enum BlockKind { Address, Domain }

/// <summary>受信拒否リストの 1 件。</summary>
public sealed record BlockEntry(long Id, string AccountId, BlockKind Kind, string Value);

/// <summary>振り分けルールの動作。</summary>
public enum FilterAction { MoveToTrash, MoveToSpam, MarkRead }

/// <summary>振り分けルール。</summary>
public sealed record FilterRule
{
    public long Id { get; init; }
    public string AccountId { get; init; } = "";
    public int SortOrder { get; init; }
    public bool Enabled { get; init; } = true;
    public string? SenderContains { get; init; }
    public string? SubjectContains { get; init; }
    public string? BodyContains { get; init; }
    public FilterAction Action { get; init; }
}

/// <summary>アプリ設定。</summary>
public sealed record AppSettings
{
    public double TextSizePt { get; init; } = Constants.DefaultTextSizePt;
    public double? WindowWidth { get; init; }
    public double? WindowHeight { get; init; }
    public double? WindowLeft { get; init; }
    public double? WindowTop { get; init; }
    public string WindowState { get; init; } = "Normal";
    public double? SplitterWidth { get; init; }
    public string? LastAccountId { get; init; }
}
