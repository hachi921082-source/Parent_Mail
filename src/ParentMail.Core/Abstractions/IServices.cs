using ParentMail.Core.Models;

namespace ParentMail.Core;

// 機能間で共有する業務サービスの契約。

/// <summary>受信時の拒否リスト・振り分けルール適用。</summary>
public interface IIncomingRule
{
    /// <summary>受信メール 1 通に適用する。適用した動作を返す（なければ null）。</summary>
    FilterAction? Evaluate(string accountId, MailMessage message);
    bool IsBlocked(string accountId, string fromAddress);
}

/// <summary>本文のオンデマンド取得。</summary>
public interface IBodyFetcher
{
    Task<MailMessage?> EnsureBodyAsync(string accountId, long gmailMsgId, CancellationToken ct = default);
}

/// <summary>添付のオンデマンド取得。</summary>
public interface IAttachmentService
{
    /// <summary>ローカルに保存し、保存先パスを返す。</summary>
    Task<string?> EnsureLocalAsync(string accountId, long attachmentId, CancellationToken ct = default);
}

/// <summary>アイドル時の本文先読み。</summary>
public interface IIdleMonitor
{
    void Start();
    void Stop();
}

public interface IDraftService
{
    Task<DraftItem> SaveAsync(DraftItem draft, CancellationToken ct = default);
    Task DiscardAsync(long localId, CancellationToken ct = default);
}

public interface IOutboxService
{
    /// <summary>送信箱に積み、送信を試みる。</summary>
    Task<OutboxItem> EnqueueAsync(OutboxItem item, CancellationToken ct = default);
    Task ProcessAsync(string accountId, CancellationToken ct = default);
    int PendingCount(string accountId);
    event EventHandler? Changed;
}

/// <summary>同期の状態。</summary>
public enum SyncState { Idle, Syncing, Offline, Error }

/// <summary>アカウント単位の同期。</summary>
public interface ISyncService
{
    string AccountId { get; }
    SyncState State { get; }
    Task SyncNowAsync(CancellationToken ct = default);
    event EventHandler? SyncCompleted;
    /// <summary>ブロック・振り分け後に新着として確定したメールの通知。</summary>
    event EventHandler<MailMessage>? NewMailProcessed;
}

/// <summary>アカウントごとの ISyncService を引く入口。</summary>
public interface ISyncServiceProvider
{
    ISyncService Get(string accountId);
}

/// <summary>返信・転送・新規作成の初期値。</summary>
public sealed record ComposeInit
{
    public string AccountId { get; init; } = "";
    public IReadOnlyList<string> To { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();
    public string Subject { get; init; } = "";
    public string Body { get; init; } = "";
    public IReadOnlyList<AttachmentInfo> Attachments { get; init; } = Array.Empty<AttachmentInfo>();
    public long? SourceDraftId { get; init; }
}

/// <summary>作成ウィンドウを開く（他機能から利用）。</summary>
public interface IComposeWindowFactory
{
    void Open(ComposeInit init);
}

/// <summary>アドレス帳照会。</summary>
public interface IContactLookup
{
    bool IsKnown(string accountId, string address);
}
