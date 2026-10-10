using ParentMail.Core;
using ParentMail.Core.Features.SyncCache;
using ParentMail.Core.Models;
using ParentMail.Tests.Data;
using ParentMail.Tests.Fakes;

namespace ParentMail.Tests.SyncCache;

/// <summary>適用時の動きを差し替えられる受信規則（FakeIncomingRule は動作を足せないため）。</summary>
public sealed class ScriptedRule : IIncomingRule
{
    public bool Handled { get; set; }
    public Func<MailMessage, RuleContext, Task<bool>>? OnApply { get; set; }
    public List<MailMessage> Applied { get; } = new();

    public Task<bool> ApplyAsync(MailMessage message, RuleContext context)
    {
        Applied.Add(message);
        return OnApply is null ? Task.FromResult(Handled) : OnApply(message, context);
    }
}

/// <summary>同期エンジンのテスト基盤。日本語を含む一時フォルダの cache.db とフェイクのゲートウェイで SyncService を組む。</summary>
public sealed class SyncHarness : IDisposable
{
    public const string Inbox = "INBOX";
    public const string Sent = "[Gmail]/送信済みメール";
    public const string Drafts = "[Gmail]/下書き";
    public const string Trash = "[Gmail]/ゴミ箱";
    public const string Spam = "[Gmail]/迷惑メール";
    public const string AllMail = "[Gmail]/すべてのメール";

    public CacheFixture Cache { get; } = new();
    public string AccountId => CacheFixture.AccountId;
    public FakeImapGateway Gateway { get; } = new();
    public InMemoryAccountRepository Accounts { get; } = new();
    public ScriptedRule Rule { get; } = new();
    public FakeOutboxService Outbox { get; } = new();
    public FakeAppLog Log { get; } = new();
    public SyncCacheStore Store { get; }
    public SyncService Sync { get; }
    public List<MailMessage> NewMails { get; } = new();
    public int StateChangedCount;

    public SyncHarness(IImapGatewayFactory? factory = null, IIncomingRule[]? rules = null, bool withOutbox = true)
    {
        Accounts.Add(new Account(AccountId, "テスト@gmail.com", "母", 0));
        Gateway.Folders.AddRange(new[]
        {
            new ImapFolderInfo(Inbox, FolderKind.Inbox), new ImapFolderInfo(Sent, FolderKind.Sent),
            new ImapFolderInfo(Drafts, FolderKind.Drafts), new ImapFolderInfo(Trash, FolderKind.Trash),
            new ImapFolderInfo(Spam, FolderKind.Spam), new ImapFolderInfo(AllMail, FolderKind.AllMail),
        });
        Store = new SyncCacheStore(Cache.Db);
        var gf = factory ?? new FakeImapGatewayFactory { ByAccountId = { [AccountId] = Gateway } };
        Sync = new SyncService(Accounts, gf, Cache.Mail, Store, Log, Cache.Clock, rules ?? new IIncomingRule[] { Rule },
            withOutbox ? Outbox : null);
        Sync.NewMailProcessed += (_, e) => NewMails.Add(e.Message);
        Sync.StateChanged += (_, _) => Interlocked.Increment(ref StateChangedCount);
    }

    public DateTime Now => Cache.Clock.UtcNow;

    /// <summary>ヘッダと本文をフェイクのフォルダに足す。</summary>
    public ImapHeader Put(string folder, uint uid, long gmId, DateTime? date = null, bool read = false, string subject = "件名テスト",
        bool withBody = true)
    {
        var h = new ImapHeader(uid, gmId, $"<{gmId}@example.com>", null, subject, "taro@example.com", "山田太郎",
            new[] { "me@example.com" }, Array.Empty<string>(), date ?? Now.AddDays(-1), read, false);
        if (!Gateway.Headers.TryGetValue(folder, out var list)) Gateway.Headers[folder] = list = new List<ImapHeader>();
        list.RemoveAll(x => x.Uid == uid);
        list.Add(h);
        if (withBody) Gateway.Bodies[(folder, uid)] = new ImapBody(uid, "本文" + gmId, null, Array.Empty<AttachmentInfo>());
        return h;
    }

    public void Remove(string folder, uint uid) => Gateway.Headers[folder].RemoveAll(x => x.Uid == uid);

    public void SetStatus(string folder, uint validity, uint uidNext = 1000)
        => Gateway.Statuses[folder] = new ImapFolderStatus(folder, validity, uidNext, null, Gateway.Headers.GetValueOrDefault(folder)?.Count ?? 0);

    public Task RunAsync(SyncTrigger t = SyncTrigger.Manual) => Sync.SyncNowAsync(AccountId, t);

    public void Dispose() => Cache.Dispose();
}

/// <summary>ゲートウェイを作る処理をラムダで渡すための工場。</summary>
public sealed class DelegateGatewayFactory(Func<Account, IImapGateway> create) : IImapGatewayFactory
{
    public IImapGateway Create(Account account) => create(account);
}

/// <summary>内側のフェイクへ呼び出しを流す土台。必要な操作だけ派生で差し替える。</summary>
public class PassThroughGateway(FakeImapGateway inner) : IImapGateway
{
    public virtual Task<LoginResult> TestLoginAsync(CancellationToken ct = default) => inner.TestLoginAsync(ct);
    public Task<IReadOnlyList<ImapFolderInfo>> ListFoldersAsync(CancellationToken ct = default) => inner.ListFoldersAsync(ct);
    public virtual Task<ImapFolderStatus> GetFolderStatusAsync(string p, CancellationToken ct = default) => inner.GetFolderStatusAsync(p, ct);
    public Task<IReadOnlyList<uint>> SearchUidsAsync(string p, DateTime? s, CancellationToken ct = default) => inner.SearchUidsAsync(p, s, ct);
    public Task<IReadOnlyList<ImapHeader>> FetchHeadersAsync(string p, IReadOnlyList<uint> u, CancellationToken ct = default) => inner.FetchHeadersAsync(p, u, ct);
    public virtual Task<ImapBody?> FetchBodyAsync(string p, uint u, CancellationToken ct = default) => inner.FetchBodyAsync(p, u, ct);
    public Task<Stream?> FetchPartAsync(string p, uint u, string part, CancellationToken ct = default) => inner.FetchPartAsync(p, u, part, ct);
    public Task SetSeenAsync(string p, IReadOnlyList<uint> u, bool seen, CancellationToken ct = default) => inner.SetSeenAsync(p, u, seen, ct);
    public Task<uint?> MoveAsync(string f, uint u, string t, CancellationToken ct = default) => inner.MoveAsync(f, u, t, ct);
    public Task<uint?> AppendAsync(string p, byte[] raw, bool seen, CancellationToken ct = default) => inner.AppendAsync(p, raw, seen, ct);
    public Task DeleteAsync(string p, IReadOnlyList<uint> u, CancellationToken ct = default) => inner.DeleteAsync(p, u, ct);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>TestLogin で止めて、同期の実行中を作るためのゲートウェイ。</summary>
public sealed class GatedGateway(FakeImapGateway inner) : PassThroughGateway(inner)
{
    private readonly FakeImapGateway _inner = inner;
    public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Logins;

    public override async Task<LoginResult> TestLoginAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref Logins);
        await Gate.Task;
        return await _inner.TestLoginAsync(ct);
    }
}

/// <summary>フォルダ状態の取得で想定外の例外を出すゲートウェイ（ログに内容が出ないことの確認用）。</summary>
public sealed class ThrowingGateway(FakeImapGateway inner) : PassThroughGateway(inner)
{
    public override Task<ImapFolderStatus> GetFolderStatusAsync(string p, CancellationToken ct = default)
        => throw new InvalidOperationException("件名: 秘密の件名");
}

/// <summary>本文取得が通信断で失敗するゲートウェイ（FailBodies が true の間）。</summary>
public sealed class FlakyBodyGateway(FakeImapGateway inner) : PassThroughGateway(inner)
{
    private readonly FakeImapGateway _inner = inner;
    public bool FailBodies { get; set; }

    public override Task<ImapBody?> FetchBodyAsync(string p, uint u, CancellationToken ct = default)
        => FailBodies ? throw new IOException("lost") : _inner.FetchBodyAsync(p, u, ct);
}
