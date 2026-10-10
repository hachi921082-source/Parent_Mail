using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.Tests.Fakes;

// 機能間サービスの手書きフェイク。戻り値は設定プロパティで切り替え、呼び出しは記録する。

public sealed class FakeSettingsStore : ISettingsStore
{
    public AppSettings Current { get; set; } = new();
    public int SaveCount { get; private set; }
    public AppSettings Get() => Current;
    public void Save(AppSettings settings) { Current = settings; SaveCount++; }
}

public sealed class FakeRuleActions : IRuleActions
{
    public List<(long GmailMsgId, FolderKind Target)> Moves { get; } = new();
    public List<long> MarkedRead { get; } = new();
    public Task MoveAsync(long gmailMsgId, FolderKind target) { Moves.Add((gmailMsgId, target)); return Task.CompletedTask; }
    public Task MarkReadAsync(long gmailMsgId) { MarkedRead.Add(gmailMsgId); return Task.CompletedTask; }
}

public sealed class FakeIncomingRule : IIncomingRule
{
    public bool Handled { get; set; }
    public List<MailMessage> Applied { get; } = new();
    public Task<bool> ApplyAsync(MailMessage message, RuleContext context) { Applied.Add(message); return Task.FromResult(Handled); }
}

public sealed class FakeBodyFetcher : IBodyFetcher
{
    public BodyFetchResult Result { get; set; } = BodyFetchResult.Ready;
    public List<long> Requested { get; } = new();
    public Task<BodyFetchResult> EnsureBodyAsync(string accountId, long gmailMsgId, CancellationToken ct = default)
    {
        Requested.Add(gmailMsgId);
        return Task.FromResult(Result);
    }
}

public sealed class FakeAttachmentService : IAttachmentService
{
    public AttachmentFetchResult Result { get; set; } = new(AttachmentFetchStatus.Ready, @"C:\temp\attachment.bin");
    public List<long> Requested { get; } = new();
    public Task<AttachmentFetchResult> EnsureLocalAsync(string accountId, long attachmentId, CancellationToken ct = default)
    {
        Requested.Add(attachmentId);
        return Task.FromResult(Result);
    }
}

public sealed class FakeIdleMonitor : IIdleMonitor
{
    public TimeSpan IdleFor { get; set; }
    public event EventHandler? UserActivity;
    public void RaiseUserActivity() => UserActivity?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeDraftService : IDraftService
{
    public List<(DraftSnapshot Snapshot, bool UploadNow)> Saved { get; } = new();
    public List<long> Deleted { get; } = new();
    public List<string> Flushed { get; } = new();
    public ComposeInit? OpenResult { get; set; }
    private long _nextId = 1;

    public Task<DraftItem> SaveAsync(DraftSnapshot snapshot, bool uploadNow, CancellationToken ct = default)
    {
        Saved.Add((snapshot, uploadNow));
        var item = new DraftItem
        {
            LocalId = snapshot.DraftId ?? _nextId++,
            AccountId = snapshot.AccountId, To = snapshot.To, Cc = snapshot.Cc, Bcc = snapshot.Bcc,
            Subject = snapshot.Subject, Body = snapshot.Body, Attachments = snapshot.Attachments,
            ReplyHeaders = snapshot.ReplyHeaders, Dirty = !uploadNow,
        };
        return Task.FromResult(item);
    }

    public Task DeleteAsync(long draftId, CancellationToken ct = default) { Deleted.Add(draftId); return Task.CompletedTask; }
    public Task FlushDirtyAsync(string accountId, CancellationToken ct = default) { Flushed.Add(accountId); return Task.CompletedTask; }
    public Task<ComposeInit?> OpenForEditAsync(long draftId, CancellationToken ct = default) => Task.FromResult(OpenResult);
    public Task<ComposeInit?> OpenForEditByMessageAsync(string accountId, long gmailMsgId, CancellationToken ct = default) => Task.FromResult(OpenResult);
}

public sealed class FakeOutboxService : IOutboxService
{
    public List<DraftSnapshot> Enqueued { get; } = new();
    public List<string> RetryAccounts { get; } = new();
    public Dictionary<string, int> Pending { get; } = new();
    public Dictionary<string, int> Failed { get; } = new();
    public SendResult SendNowResult { get; set; } = SendResult.Ok;
    public SendAllResult SendAllResult { get; set; } = SendAllResult.AllSent;
    private long _nextId = 1;

    public Task<OutboxItem> EnqueueAndSendAsync(DraftSnapshot snapshot, CancellationToken ct = default)
    {
        Enqueued.Add(snapshot);
        Pending[snapshot.AccountId] = PendingCount(snapshot.AccountId) + 1;
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(new OutboxItem
        {
            Id = _nextId++, AccountId = snapshot.AccountId, To = snapshot.To, Cc = snapshot.Cc, Bcc = snapshot.Bcc,
            Subject = snapshot.Subject, Body = snapshot.Body, Attachments = snapshot.Attachments,
            ReplyHeaders = snapshot.ReplyHeaders, SourceDraftId = snapshot.DraftId,
        });
    }

    public Task RetryDueAsync(string accountId, CancellationToken ct = default) { RetryAccounts.Add(accountId); return Task.CompletedTask; }
    public Task<SendResult> SendNowAsync(long outboxId, CancellationToken ct = default) => Task.FromResult(SendNowResult);
    public Task<DraftItem> ReturnToDraftsAsync(long outboxId, CancellationToken ct = default) => Task.FromResult(new DraftItem { Dirty = true });
    public Task<SendAllResult> SendAllPendingAsync(CancellationToken ct = default) => Task.FromResult(SendAllResult);
    public int PendingCount() => Pending.Values.Sum();
    public int PendingCount(string accountId) => Pending.GetValueOrDefault(accountId);
    public int FailedCount(string accountId) => Failed.GetValueOrDefault(accountId);
    public event EventHandler? Changed;
}

public sealed class FakeSyncService : ISyncService
{
    public Dictionary<string, SyncState> States { get; } = new();
    public List<(string AccountId, SyncTrigger Trigger)> Requests { get; } = new();
    public HashSet<string> AuthFailed { get; } = new();

    public SyncState GetState(string accountId) => States.GetValueOrDefault(accountId);

    public Task SyncNowAsync(string accountId, SyncTrigger trigger, CancellationToken ct = default)
    {
        Requests.Add((accountId, trigger));
        SyncCompleted?.Invoke(this, new AccountEventArgs(accountId));
        return Task.CompletedTask;
    }

    public void MarkAuthFailed(string accountId) => AuthFailed.Add(accountId);
    public void ClearAuthFailed(string accountId) => AuthFailed.Remove(accountId);

    public void RaiseStateChanged(string accountId, SyncState state)
    {
        States[accountId] = state;
        StateChanged?.Invoke(this, new AccountEventArgs(accountId));
    }

    public void RaiseNewMail(string accountId, MailMessage message) => NewMailProcessed?.Invoke(this, new NewMailEventArgs(accountId, message));

    public event EventHandler<AccountEventArgs>? StateChanged;
    public event EventHandler<AccountEventArgs>? SyncCompleted;
    public event EventHandler<NewMailEventArgs>? NewMailProcessed;
}

public sealed class FakeComposeWindowFactory : IComposeWindowFactory
{
    /// <summary>Open の呼び出し記録。</summary>
    public List<ComposeInit> Opened { get; } = new();
    public void Open(ComposeInit init) => Opened.Add(init);
}
