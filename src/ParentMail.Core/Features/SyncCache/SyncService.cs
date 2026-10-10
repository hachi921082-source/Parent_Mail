using System.Net.Sockets;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using ParentMail.Core.Data;
using ParentMail.Core.Models;

namespace ParentMail.Core.Features.SyncCache;

/// <summary>
/// 同期エンジン（sync-cache PLAN-2）。アカウントごとに 1 本だけ走る（実行中のタイマー起動は捨て、手動更新は合流する）。
/// フォルダを Inbox, Sent, Drafts, Trash, Spam, AllMail の順に、初回同期または差分同期する。
/// 受信規則は「初回同期後に Inbox で初めて見えたメール」にだけ適用する（UIDVALIDITY 変更の再列挙では適用しない）。
/// </summary>
public sealed class SyncService : ISyncService, IReadStateSync
{
    /// <summary>ヘッダを取得する 1 回あたりの件数。</summary>
    public const int BatchSize = 200;
    /// <summary>差分同期で既読状態を取り直す上限（新しい UID から）。</summary>
    public const int FlagRefreshMax = 3000;

    private static readonly FolderKind[] FolderOrder =
    {
        FolderKind.Inbox, FolderKind.Sent, FolderKind.Drafts, FolderKind.Trash, FolderKind.Spam, FolderKind.AllMail,
    };

    private readonly IAccountRepository _accounts;
    private readonly IImapGatewayFactory _gateways;
    private readonly MailRepository _mail;
    private readonly ISyncCacheStore _store;
    private readonly IReadOnlyList<IIncomingRule> _rules;
    private readonly IOutboxService? _outbox;
    private readonly IAppLog _log;
    private readonly IClock _clock;

    private readonly object _lock = new();
    private readonly Dictionary<string, Task> _running = new();
    private readonly Dictionary<string, SyncState> _states = new();
    private readonly HashSet<string> _authFailed = new();

    public SyncService(IAccountRepository accounts, IImapGatewayFactory gateways, MailRepository mail, ISyncCacheStore store,
        IAppLog log, IClock clock, IEnumerable<IIncomingRule> rules, IOutboxService? outbox = null)
    {
        _accounts = accounts;
        _gateways = gateways;
        _mail = mail;
        _store = store;
        _log = log;
        _clock = clock;
        _rules = rules.ToList();
        _outbox = outbox;
    }

    public event EventHandler<AccountEventArgs>? StateChanged;
    public event EventHandler<AccountEventArgs>? SyncCompleted;
    public event EventHandler<NewMailEventArgs>? NewMailProcessed;

    public SyncState GetState(string accountId)
    {
        lock (_lock)
        {
            if (_authFailed.Contains(accountId)) return SyncState.Error;
            return _states.GetValueOrDefault(accountId);
        }
    }

    /// <summary>パスワード不正で止めているか（赤い帯は accounts-auth が出す。ここでは受信失敗の帯を出さない判断に使う）。</summary>
    public bool IsAuthFailed(string accountId)
    {
        lock (_lock) return _authFailed.Contains(accountId);
    }

    public bool IsRunning(string accountId)
    {
        lock (_lock) return _running.ContainsKey(accountId);
    }

    public void MarkAuthFailed(string accountId)
    {
        lock (_lock) _authFailed.Add(accountId);
        Raise(accountId);
    }

    public void ClearAuthFailed(string accountId)
    {
        lock (_lock)
        {
            _authFailed.Remove(accountId);
            _states[accountId] = SyncState.Idle;
        }
        Raise(accountId);
    }

    public Task SyncNowAsync(string accountId, SyncTrigger trigger, CancellationToken ct = default)
    {
        TaskCompletionSource tcs;
        lock (_lock)
        {
            if (_authFailed.Contains(accountId)) return Task.CompletedTask;
            if (_running.TryGetValue(accountId, out var current))
                return trigger == SyncTrigger.Timer ? Task.CompletedTask : current; // タイマーは捨て、手動は合流
            tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _running[accountId] = tcs.Task;
        }
        return ExecuteAsync(accountId, trigger, tcs, ct);
    }

    private async Task ExecuteAsync(string accountId, SyncTrigger trigger, TaskCompletionSource tcs, CancellationToken ct)
    {
        try
        {
            await Task.Run(() => RunAsync(accountId, trigger, ct), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            lock (_lock) _running.Remove(accountId);
            tcs.TrySetResult();
        }
    }

    // ---- 1 回の同期 ----

    private async Task RunAsync(string accountId, SyncTrigger trigger, CancellationToken ct)
    {
        var account = _accounts.Get(accountId);
        if (account is null) { SetState(accountId, SyncState.Idle); return; }

        SetState(accountId, SyncState.Syncing);
        try
        {
            await using var gw = _gateways.Create(account);
            var login = await gw.TestLoginAsync(ct).ConfigureAwait(false);
            if (login == LoginResult.AuthFailed) { MarkAuthFailed(accountId); return; }
            if (login == LoginResult.NetworkError) { SetState(accountId, SyncState.Offline); return; }

            await SyncCoreAsync(accountId, gw, ct).ConfigureAwait(false);
            SetState(accountId, SyncState.Idle);
            SyncCompleted?.Invoke(this, new AccountEventArgs(accountId));

            // 再送の時期が来た送信箱をここで流す（送信箱の失敗は同期の成否に関係させない）
            if (_outbox is not null)
            {
                try { await _outbox.RetryDueAsync(accountId, ct).ConfigureAwait(false); }
                catch (Exception ex) when (ex is not OperationCanceledException) { _log.Warn("Sync", "Outbox retry failed: " + ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            SetState(accountId, SyncState.Idle);
            throw;
        }
        catch (Exception ex)
        {
            Classify(accountId, ex);
        }
    }

    /// <summary>失敗の種類を判別して状態に反映する。内容（件名・宛先など）はログに出さず、例外の型だけを残す。</summary>
    private void Classify(string accountId, Exception ex)
    {
        if (ex is AggregateException agg && agg.InnerException is not null) ex = agg.InnerException;
        if (ex is AuthenticationException)
        {
            _log.Warn("Sync", "Auth failed during sync");
            MarkAuthFailed(accountId);
        }
        else if (IsNetworkError(ex))
        {
            _log.Info("Sync", "Offline: " + ex.GetType().Name);
            SetState(accountId, SyncState.Offline);
        }
        else
        {
            _log.Error("Sync", "Sync failed", ex);
            SetState(accountId, SyncState.Error);
        }
    }

    internal static bool IsNetworkError(Exception ex)
        => ex is IOException or SocketException or TimeoutException or HttpRequestException
            or ServiceNotConnectedException or ServiceNotAuthenticatedException or ImapProtocolException or SslHandshakeException
            or OperationCanceledException;

    private void SetState(string accountId, SyncState state)
    {
        lock (_lock) _states[accountId] = state;
        Raise(accountId);
    }

    private void Raise(string accountId) => StateChanged?.Invoke(this, new AccountEventArgs(accountId));

    // ---- 同期本体 ----

    private sealed class Run
    {
        public required string AccountId { get; init; }
        public required IImapGateway Gateway { get; init; }
        public required Dictionary<FolderKind, string> Paths { get; init; }
        public required bool InitialDone { get; init; }
        public required HashSet<long> Known { get; init; }
        public HashSet<long> DeleteCandidates { get; } = new();
        public required CancellationToken Ct { get; init; }
    }

    private async Task SyncCoreAsync(string accountId, IImapGateway gw, CancellationToken ct)
    {
        var paths = await ResolveFoldersAsync(accountId, gw, ct).ConfigureAwait(false);
        await FlushPendingAsync(accountId, gw, paths, ct).ConfigureAwait(false);

        var run = new Run
        {
            AccountId = accountId,
            Gateway = gw,
            Paths = paths,
            InitialDone = _store.IsInitialSyncDone(accountId),
            Known = _store.GetMessageIds(accountId),
            Ct = ct,
        };

        foreach (var kind in FolderOrder)
        {
            if (!paths.ContainsKey(kind)) continue;
            await SyncFolderAsync(run, kind).ConfigureAwait(false);
        }

        // どのフォルダにも属さなくなったメールはここで消す（移動の途中で一時的に所属が無くなる場合を避けるため最後に判定）
        foreach (var id in run.DeleteCandidates)
            if (_mail.GetMemberships(accountId, id).Count == 0)
                _mail.DeleteMessage(accountId, id);

        if (!run.InitialDone) _store.SetInitialSyncDone(accountId);
    }

    private async Task<Dictionary<FolderKind, string>> ResolveFoldersAsync(string accountId, IImapGateway gw, CancellationToken ct)
    {
        var map = new Dictionary<FolderKind, string>();
        foreach (var f in await gw.ListFoldersAsync(ct).ConfigureAwait(false))
        {
            if (f.Kind is not { } kind || kind == FolderKind.Outbox || map.ContainsKey(kind)) continue;
            map[kind] = f.Path;
            _mail.SaveImapName(accountId, kind, f.Path);
        }
        return map;
    }

    private async Task SyncFolderAsync(Run run, FolderKind kind)
    {
        var acc = run.AccountId;
        var path = run.Paths[kind];
        var status = await run.Gateway.GetFolderStatusAsync(path, run.Ct).ConfigureAwait(false);
        var state = _mail.GetFolderState(acc, kind);

        if (state is not null && state.UidValidity != status.UidValidity)
        {
            // UIDVALIDITY が変わった: UID 対応だけ捨てて再列挙する。本文は残し、規則も新着扱いもしない（PQ7）
            _log.Info("Sync", $"UIDVALIDITY changed: {kind}");
            _mail.ResetFolderUids(acc, kind);
            state = null;
        }

        if (state is null) await InitialFolderAsync(run, kind, path, status).ConfigureAwait(false);
        else await PeriodicFolderAsync(run, kind, path, status, state).ConfigureAwait(false);
    }

    /// <summary>初回（または UIDVALIDITY 変更後）の同期。新しい順に 200 件ずつヘッダを保存し、そのあと本文を新しい順に取る。</summary>
    private async Task InitialFolderAsync(Run run, FolderKind kind, string path, ImapFolderStatus status)
    {
        var acc = run.AccountId;
        var all = await run.Gateway.SearchUidsAsync(path, null, run.Ct).ConfigureAwait(false);
        var knownUids = _mail.GetKnownUids(acc, kind);
        // 途中で止まった初回同期の再開: 保存済みの UID は飛ばす
        var todo = all.Where(u => !knownUids.ContainsKey(u)).OrderByDescending(u => u).ToList();

        foreach (var batch in todo.Chunk(BatchSize))
        {
            run.Ct.ThrowIfCancellationRequested();
            var headers = await run.Gateway.FetchHeadersAsync(path, batch, run.Ct).ConfigureAwait(false);
            foreach (var h in headers)
            {
                var membership = new MessageFolder(h.GmailMsgId, kind, h.Uid, status.UidValidity);
                if (run.Known.Contains(h.GmailMsgId))
                {
                    _mail.AddMembership(acc, membership);
                }
                else
                {
                    _mail.Upsert(acc, ToMessage(h), new[] { membership });
                    run.Known.Add(h.GmailMsgId);
                }
            }
            Raise(acc); // 一覧が段階的に埋まるように進捗を知らせる（Q23）
        }

        // 本文: 12 か月以内、新しい順
        var since = _clock.UtcNow.AddMonths(-Constants.InitialBodyMonths);
        foreach (var (id, uid) in _store.GetBodylessInFolder(acc, kind, since))
        {
            run.Ct.ThrowIfCancellationRequested();
            await FetchAndStoreBodyAsync(run, path, uid, id).ConfigureAwait(false);
        }

        _mail.SaveFolderState(acc, new FolderSyncState(kind, status.UidValidity, status.UidNext, status.HighestModSeq));
    }

    /// <summary>差分同期: 新着、既読状態の変化、削除・移動（所属の消滅）を反映する。</summary>
    private async Task PeriodicFolderAsync(Run run, FolderKind kind, string path, ImapFolderStatus status, FolderSyncState state)
    {
        var acc = run.AccountId;
        var knownUids = _mail.GetKnownUids(acc, kind);

        // 何も変わっていなければ飛ばす（CONDSTORE の変更番号・次の UID・件数が一致）
        if (state.HighestModSeq is not null && status.HighestModSeq == state.HighestModSeq
            && status.UidNext == state.UidNext && status.MessageCount == knownUids.Count)
            return;

        var all = await run.Gateway.SearchUidsAsync(path, null, run.Ct).ConfigureAwait(false);
        var allSet = all.ToHashSet();

        // 消えた UID: 所属を外す（メール自体は最後にどこにも属さなければ消す）
        foreach (var (uid, id) in knownUids)
        {
            if (allSet.Contains(uid)) continue;
            _mail.RemoveMembership(acc, id, kind);
            run.DeleteCandidates.Add(id);
        }

        // 新しい UID: 古い順に処理する（規則も古い順に適用される）
        var newUids = all.Where(u => !knownUids.ContainsKey(u)).OrderBy(u => u).ToList();
        var fresh = new HashSet<uint>(newUids);
        foreach (var batch in newUids.Chunk(BatchSize))
        {
            run.Ct.ThrowIfCancellationRequested();
            var headers = await run.Gateway.FetchHeadersAsync(path, batch, run.Ct).ConfigureAwait(false);
            foreach (var h in headers.OrderBy(h => h.Uid))
            {
                var membership = new MessageFolder(h.GmailMsgId, kind, h.Uid, status.UidValidity);
                if (run.Known.Contains(h.GmailMsgId))
                {
                    // 他フォルダから移動してきた等。既に知っているメールは所属だけ足す
                    _mail.AddMembership(acc, membership);
                    continue;
                }
                _mail.Upsert(acc, ToMessage(h), new[] { membership });
                run.Known.Add(h.GmailMsgId);
                await FetchAndStoreBodyAsync(run, path, h.Uid, h.GmailMsgId).ConfigureAwait(false); // 新着は必ず本文まで取る（Q17）
                if (kind == FolderKind.Inbox && run.InitialDone) await ProcessIncomingAsync(run, h.GmailMsgId).ConfigureAwait(false);
            }
            Raise(acc);
        }

        await RefreshReadStatesAsync(run, kind, path, knownUids, allSet, fresh).ConfigureAwait(false);

        _mail.SaveFolderState(acc, new FolderSyncState(kind, status.UidValidity, status.UidNext, status.HighestModSeq));
    }

    /// <summary>既知メールの既読状態を Gmail 側に合わせる。未反映の自分の操作があるメールは上書きしない。</summary>
    private async Task RefreshReadStatesAsync(Run run, FolderKind kind, string path,
        IReadOnlyDictionary<uint, long> knownUids, HashSet<uint> allSet, HashSet<uint> fresh)
    {
        var targets = knownUids.Keys.Where(u => allSet.Contains(u) && !fresh.Contains(u))
            .OrderByDescending(u => u).Take(FlagRefreshMax).ToList();
        if (targets.Count == 0) return;

        var local = _store.GetReadStates(run.AccountId);
        // 同期中に画面から積まれた操作も守るため、積んだ一覧はここで毎回読む
        var pending = _store.GetPendingMessageIds(run.AccountId);
        foreach (var batch in targets.Chunk(BatchSize))
        {
            run.Ct.ThrowIfCancellationRequested();
            var headers = await run.Gateway.FetchHeadersAsync(path, batch, run.Ct).ConfigureAwait(false);
            foreach (var h in headers)
            {
                if (pending.Contains(h.GmailMsgId)) continue;
                if (local.TryGetValue(h.GmailMsgId, out var isRead) && isRead != h.IsRead)
                    _mail.SetRead(run.AccountId, h.GmailMsgId, h.IsRead);
            }
        }
    }

    private async Task<bool> FetchAndStoreBodyAsync(Run run, string path, uint uid, long gmailMsgId)
    {
        var body = await run.Gateway.FetchBodyAsync(path, uid, run.Ct).ConfigureAwait(false);
        if (body is null) return false;
        var derived = body.Html is null ? null : BodyParser.HtmlToText(body.Html);
        _mail.SetBody(run.AccountId, gmailMsgId, body.Text, body.Html, derived);
        if (body.Attachments.Count > 0) _mail.SaveAttachments(run.AccountId, gmailMsgId, body.Attachments);
        return true;
    }

    // ---- 受信パイプライン ----

    /// <summary>新着 1 通に規則（拒否リスト、振り分けの順）を適用し、残っていれば新着として通知する。</summary>
    private async Task ProcessIncomingAsync(Run run, long gmailMsgId)
    {
        var acc = run.AccountId;
        var message = _mail.Get(acc, gmailMsgId);
        if (message is null) return;

        var ctx = new RuleContext(acc, new RuleActions(this, run));
        try
        {
            foreach (var rule in _rules)
                if (await rule.ApplyAsync(message, ctx).ConfigureAwait(false)) break;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 規則の動作に失敗（通信など）: メールはそのまま受信トレイに残し、ログだけ残す
            _log.Warn("Sync", "Incoming rule failed: " + ex.GetType().Name);
        }

        // 規則で受信トレイから出たメールは新着として通知しない
        if (_mail.GetMemberships(acc, gmailMsgId).All(m => m.Folder != FolderKind.Inbox)) return;
        var latest = _mail.Get(acc, gmailMsgId);
        if (latest is not null) NewMailProcessed?.Invoke(this, new NewMailEventArgs(acc, latest));
    }

    private sealed class RuleActions : IRuleActions
    {
        private readonly SyncService _s;
        private readonly Run _run;

        public RuleActions(SyncService s, Run run) { _s = s; _run = run; }

        public async Task MoveAsync(long gmailMsgId, FolderKind target)
        {
            var acc = _run.AccountId;
            var mems = _s._mail.GetMemberships(acc, gmailMsgId);
            var src = mems.FirstOrDefault(m => m.Folder == FolderKind.Inbox) ?? mems.FirstOrDefault();
            if (src is null || !_run.Paths.TryGetValue(src.Folder, out var from) || !_run.Paths.TryGetValue(target, out var to))
                throw new InvalidOperationException("move source or target folder unknown");
            var newUid = await _run.Gateway.MoveAsync(from, src.Uid, to, _run.Ct).ConfigureAwait(false);
            _s._mail.RemoveMembership(acc, gmailMsgId, src.Folder);
            if (newUid is { } uid) _s._mail.AddMembership(acc, new MessageFolder(gmailMsgId, target, uid, 0));
        }

        public async Task MarkReadAsync(long gmailMsgId)
        {
            var acc = _run.AccountId;
            foreach (var m in _s._mail.GetMemberships(acc, gmailMsgId))
                if (_run.Paths.TryGetValue(m.Folder, out var p))
                    await _run.Gateway.SetSeenAsync(p, new[] { m.Uid }, true, _run.Ct).ConfigureAwait(false);
            _s._mail.SetRead(acc, gmailMsgId, true);
        }
    }

    // ---- 既読・未読の書き込み ----

    public async Task SetSeenAsync(string accountId, long gmailMsgId, bool seen, CancellationToken ct = default)
    {
        _mail.SetRead(accountId, gmailMsgId, seen);
        // 同期中は走っている同期が古い状態で上書きしないよう、積んで次回に回す
        if (IsRunning(accountId) || IsAuthFailed(accountId) || _accounts.Get(accountId) is not { } account)
        {
            _store.EnqueueSeen(accountId, gmailMsgId, seen);
            return;
        }

        try
        {
            var targets = _mail.GetMemberships(accountId, gmailMsgId)
                .Select(m => (Path: _mail.GetImapName(accountId, m.Folder), m.Uid)).ToList();
            if (targets.Count == 0 || targets.Any(t => t.Path is null))
            {
                _store.EnqueueSeen(accountId, gmailMsgId, seen);
                return;
            }
            await using var gw = _gateways.Create(account);
            if (await gw.TestLoginAsync(ct).ConfigureAwait(false) != LoginResult.Ok)
            {
                _store.EnqueueSeen(accountId, gmailMsgId, seen);
                return;
            }
            foreach (var t in targets)
                await gw.SetSeenAsync(t.Path!, new[] { t.Uid }, seen, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.Info("Sync", "Seen flag queued: " + ex.GetType().Name);
            _store.EnqueueSeen(accountId, gmailMsgId, seen);
        }
    }

    /// <summary>積んでおいた既読・未読を反映する。成功したものだけ消す（通信失敗は例外のまま同期を中断し、オフライン扱い）。</summary>
    private async Task FlushPendingAsync(string accountId, IImapGateway gw, Dictionary<FolderKind, string> paths, CancellationToken ct)
    {
        foreach (var op in _store.GetPending(accountId))
        {
            var mems = _mail.GetMemberships(accountId, op.GmailMsgId);
            foreach (var m in mems)
                if (paths.TryGetValue(m.Folder, out var p))
                    await gw.SetSeenAsync(p, new[] { m.Uid }, op.Op == SyncCacheStore.OpSeen, ct).ConfigureAwait(false);
            _store.RemovePending(accountId, op.Id);
        }
    }

    private static MailMessage ToMessage(ImapHeader h) => new()
    {
        GmailMsgId = h.GmailMsgId,
        MessageIdHeader = h.MessageIdHeader,
        References = h.References,
        Subject = h.Subject,
        FromAddress = h.FromAddress,
        FromName = h.FromName,
        To = h.To,
        Cc = h.Cc,
        DateUtc = h.DateUtc,
        IsRead = h.IsRead,
        HasAttachments = h.HasAttachments,
    };
}
