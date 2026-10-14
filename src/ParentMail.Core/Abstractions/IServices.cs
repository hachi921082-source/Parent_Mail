using ParentMail.Core.Models;

namespace ParentMail.Core;

// 機能間で共有する業務サービスの契約。名前と形は各機能の PLAN に合わせる。

/// <summary>受信パイプラインから規則へ渡す、移動・既読化の操作口。</summary>
public interface IRuleActions
{
    /// <summary>メールを別フォルダへ移す（IMAP と所属情報を更新）。失敗時は例外。</summary>
    Task MoveAsync(long gmailMsgId, FolderKind target);
    Task MarkReadAsync(long gmailMsgId);
}

/// <summary>規則の実行文脈。</summary>
public sealed record RuleContext(string AccountId, IRuleActions Actions);

/// <summary>受信時の規則（拒否リスト、振り分け）。実装は block-list と filter-rules が登録し、この順に実行される。</summary>
public interface IIncomingRule
{
    /// <summary>メールを処理した場合 true（以降の規則は止める）。</summary>
    Task<bool> ApplyAsync(MailMessage message, RuleContext context);
}

/// <summary>本文取得の結果。</summary>
public enum BodyFetchResult { Ready, Offline, Failed }

/// <summary>本文のオンデマンド取得。</summary>
public interface IBodyFetcher
{
    Task<BodyFetchResult> EnsureBodyAsync(string accountId, long gmailMsgId, CancellationToken ct = default);
}

/// <summary>添付取得の結果の種類。</summary>
public enum AttachmentFetchStatus { Ready, Offline, Failed }

/// <summary>添付取得の結果。Ready のとき LocalPath が入る。</summary>
public sealed record AttachmentFetchResult(AttachmentFetchStatus Status, string? LocalPath);

/// <summary>添付のオンデマンド取得。</summary>
public interface IAttachmentService
{
    Task<AttachmentFetchResult> EnsureLocalAsync(string accountId, long attachmentId, CancellationToken ct = default);
}

/// <summary>入力のない時間の監視（先読みの開始・停止の判断に使う）。</summary>
public interface IIdleMonitor
{
    /// <summary>最後のユーザー操作からの経過時間。</summary>
    TimeSpan IdleFor { get; }
    /// <summary>操作があったとき（先読みを止める契機）。</summary>
    event EventHandler? UserActivity;
}

public interface IDraftService
{
    /// <summary>下書きをローカルに保存する。uploadNow が true なら Drafts へ即アップロードする。</summary>
    Task<DraftItem> SaveAsync(DraftSnapshot snapshot, bool uploadNow, CancellationToken ct = default);
    Task DeleteAsync(long draftId, CancellationToken ct = default);
    /// <summary>ローカルの行だけ今すぐ消し、Gmail 側の削除はバックグラウンドで行う（窓を閉じるとき用）。</summary>
    Task DeleteLocalAndQueueRemoteAsync(long draftId, CancellationToken ct = default);
    /// <summary>このアカウントの Dirty な下書きをバックグラウンドで Drafts へ上げる。すぐ戻り、窓の寿命に縛られない。</summary>
    void QueueUpload(string accountId);
    /// <summary>裏の反映・削除が終わるか timeout が過ぎるまで待つ（終了時用）。例外は出さない。</summary>
    Task WaitForBackgroundAsync(TimeSpan timeout);
    /// <summary>バックグラウンドの反映・削除が終わったとき（一覧の更新を促すため）。</summary>
    event EventHandler<AccountEventArgs>? BackgroundCompleted;
    /// <summary>未アップロード（Dirty）の下書きをまとめて Drafts へ送る。</summary>
    Task FlushDirtyAsync(string accountId, CancellationToken ct = default);
    /// <summary>ローカル下書き行から編集用の初期値を作る。</summary>
    Task<ComposeInit?> OpenForEditAsync(long draftId, CancellationToken ct = default);
    /// <summary>Drafts にある Gmail メールから編集用の初期値を作る。</summary>
    Task<ComposeInit?> OpenForEditByMessageAsync(string accountId, long gmailMsgId, CancellationToken ct = default);
}

/// <summary>全件送信の結果（終了時の送信に使う）。</summary>
public enum SendAllResult { AllSent, SomeFailed }

public interface IOutboxService
{
    /// <summary>送信箱に積み、すぐに送信を試みる。</summary>
    Task<OutboxItem> EnqueueAndSendAsync(DraftSnapshot snapshot, CancellationToken ct = default);
    /// <summary>再送の時期が来たものを送る（同期のたびに呼ばれる）。</summary>
    Task RetryDueAsync(string accountId, CancellationToken ct = default);
    Task<SendResult> SendNowAsync(long outboxId, CancellationToken ct = default);
    /// <summary>送信箱から下書きに戻す。</summary>
    Task<DraftItem> ReturnToDraftsAsync(long outboxId, CancellationToken ct = default);
    /// <summary>両アカウントの未送信を全部送る（終了時、最大 60 秒）。</summary>
    Task<SendAllResult> SendAllPendingAsync(CancellationToken ct = default);
    /// <summary>両アカウントの未送信件数。</summary>
    int PendingCount();
    int PendingCount(string accountId);
    /// <summary>失敗が通知済みになっている件数。</summary>
    int FailedCount(string accountId);
    event EventHandler? Changed;
}

/// <summary>同期の状態。</summary>
public enum SyncState { Idle, Syncing, Offline, Error }

/// <summary>同期の起動理由。</summary>
public enum SyncTrigger { Startup, Timer, Manual, AccountAdded, AfterSend, Reconnect }

/// <summary>アカウントを示すイベント引数。</summary>
public sealed class AccountEventArgs(string accountId) : EventArgs
{
    public string AccountId { get; } = accountId;
}

/// <summary>新着として確定したメールのイベント引数。</summary>
public sealed class NewMailEventArgs(string accountId, MailMessage message) : EventArgs
{
    public string AccountId { get; } = accountId;
    public MailMessage Message { get; } = message;
}

/// <summary>同期サービス。アカウント ID を引数に取る単一のサービス。</summary>
public interface ISyncService
{
    SyncState GetState(string accountId);
    Task SyncNowAsync(string accountId, SyncTrigger trigger, CancellationToken ct = default);
    /// <summary>パスワード不正を記録し、同期を止める（入れ直しまで）。</summary>
    void MarkAuthFailed(string accountId);
    void ClearAuthFailed(string accountId);
    event EventHandler<AccountEventArgs>? StateChanged;
    event EventHandler<AccountEventArgs>? SyncCompleted;
    /// <summary>拒否・振り分けのあと新着として確定したメール。</summary>
    event EventHandler<NewMailEventArgs>? NewMailProcessed;
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
    public ReplyHeaders? ReplyHeaders { get; init; }
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
