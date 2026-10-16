using System.IO;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.OutboxSend;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.OutboxSend;

/// <summary>
/// 送信箱の本体（outbox-send PLAN 2 章）。積み込み、即時送信、再送、失敗の通知と目印、
/// 「今すぐ送る」「下書きに戻す」、終了時の全件送信を受け持つ。
/// 送信は 1 通ずつ直列（_gate）。同期サービスと下書きサービスは循環や未登録を避けるため遅延取得にする。
/// </summary>
public sealed class OutboxService : IOutboxService
{
    /// <summary>終了時の全件送信の待ち時間。</summary>
    private static readonly TimeSpan SendAllTimeout = TimeSpan.FromSeconds(60);

    private readonly IOutboxRepository _outbox;
    private readonly IDraftRepository _drafts;
    private readonly IAccountRepository _accounts;
    private readonly ISmtpGateway _smtp;
    private readonly CachePaths _paths;
    private readonly INotifier _notifier;
    private readonly IDialogService _dialogs;
    private readonly IAppLog _log;
    private readonly Func<ISyncService?> _sync;
    private readonly Func<IDraftService?> _draftService;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _bgLock = new();
    private readonly List<Task> _background = new();

    public event EventHandler? Changed;

    public OutboxService(IOutboxRepository outbox, IDraftRepository drafts, IAccountRepository accounts, ISmtpGateway smtp,
        CachePaths paths, INotifier notifier, IDialogService dialogs, IAppLog log,
        Func<ISyncService?> sync, Func<IDraftService?> draftService)
    {
        _outbox = outbox;
        _drafts = drafts;
        _accounts = accounts;
        _smtp = smtp;
        _paths = paths;
        _notifier = notifier;
        _dialogs = dialogs;
        _log = log;
        _sync = sync;
        _draftService = draftService;
    }

    // ---- 積み込み ----

    public Task<OutboxItem> EnqueueAndSendAsync(DraftSnapshot snapshot, CancellationToken ct = default)
    {
        var item = _outbox.Add(new OutboxItem
        {
            AccountId = snapshot.AccountId, To = snapshot.To, Cc = snapshot.Cc, Bcc = snapshot.Bcc,
            Subject = snapshot.Subject, Body = snapshot.Body, ReplyHeaders = snapshot.ReplyHeaders,
            SourceDraftId = snapshot.DraftId,
        });
        try
        {
            item = item with { Attachments = CopyAttachments(snapshot.AccountId, snapshot.Attachments, "outbox", item.Id) };
            _outbox.Update(item);
        }
        catch (Exception ex)
        {
            // 添付のコピーに失敗したら行ごと取り消し、呼び出し側（作成画面）へ伝える
            _log.Error("OutboxSend", "添付のコピーに失敗", ex);
            _outbox.Remove(item.Id);
            throw;
        }
        RaiseChanged();

        // 作成画面は閉じているので、送信は待たずに裏で始める（呼び出し側のキャンセルは引き継がない）
        var id = item.Id;
        var task = Task.Run(async () =>
        {
            try { await AttemptByIdAsync(id, interactive: false, CancellationToken.None).ConfigureAwait(false); }
            catch (Exception ex) { _log.Error("OutboxSend", "送信タスクで想定外の例外", ex); }
        });
        lock (_bgLock) _background.Add(task);
        return Task.FromResult(item);
    }

    /// <summary>裏で動いている送信がすべて終わるまで待つ（主にテスト用）。</summary>
    public async Task WaitIdleAsync()
    {
        Task[] tasks;
        lock (_bgLock) tasks = _background.ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        lock (_bgLock) _background.RemoveAll(t => t.IsCompleted);
    }

    // ---- 再送・操作 ----

    public async Task RetryDueAsync(string accountId, CancellationToken ct = default)
    {
        foreach (var item in _outbox.GetAll(accountId))
        {
            ct.ThrowIfCancellationRequested();
            await AttemptByIdAsync(item.Id, interactive: false, ct).ConfigureAwait(false);
        }
    }

    public async Task<SendResult> SendNowAsync(long outboxId, CancellationToken ct = default)
    {
        var (result, tooLarge) = await AttemptByIdAsync(outboxId, interactive: true, ct).ConfigureAwait(false);
        // 大きすぎる場合は OB_06 を出し済み。それ以外の失敗は「まだ送れませんでした」
        if (result != SendResult.Ok && !tooLarge) _dialogs.ShowNotice(Strings.WD_10, Strings.OB_05);
        return result;
    }

    public async Task<DraftItem> ReturnToDraftsAsync(long outboxId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var item = FindItem(outboxId) ?? throw new InvalidOperationException("送信箱に該当の項目がない");
            // 元の下書きが残っていれば同じ行へ戻す（Gmail 側 UID も引き継ぐ）
            var existing = item.SourceDraftId is { } sd ? _drafts.GetAll(item.AccountId).FirstOrDefault(d => d.LocalId == sd) : null;
            var draft = _drafts.Save(new DraftItem
            {
                LocalId = existing?.LocalId ?? 0, AccountId = item.AccountId,
                To = item.To, Cc = item.Cc, Bcc = item.Bcc, Subject = item.Subject, Body = item.Body,
                ReplyHeaders = item.ReplyHeaders, GmailUid = existing?.GmailUid, Dirty = true,
            });
            draft = draft with { Attachments = CopyAttachments(item.AccountId, item.Attachments, "drafts", draft.LocalId) };
            draft = _drafts.Save(draft);
            _outbox.Remove(item.Id);
            UpdateFailureBanner();
            RaiseChanged();
            return draft;
        }
        finally { _gate.Release(); }
    }

    public async Task<SendAllResult> SendAllPendingAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(SendAllTimeout);
        try
        {
            // アカウントごとに 1 つずつ順に送る（並列にしない、A90）。途中で失敗しても残りは試す
            foreach (var a in _accounts.GetAll()) await RetryDueAsync(a.Id, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        return PendingCount() == 0 ? SendAllResult.AllSent : SendAllResult.SomeFailed;
    }

    public int PendingCount() => _accounts.GetAll().Sum(a => PendingCount(a.Id));

    public int PendingCount(string accountId) => _outbox.GetAll(accountId).Count;

    public int FailedCount(string accountId) => _outbox.GetAll(accountId).Count(i => i.FailureNotified);

    // ---- 送信 1 通 ----

    private async Task<(SendResult Result, bool TooLarge)> AttemptByIdAsync(long id, bool interactive, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 待っている間に別の経路で送信済みになっていることがある
            var item = FindItem(id);
            if (item == null) return (SendResult.Ok, false);
            return await AttemptAsync(item, interactive, ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<(SendResult Result, bool TooLarge)> AttemptAsync(OutboxItem item, bool interactive, CancellationToken ct)
    {
        var account = _accounts.Get(item.AccountId);
        if (account == null)
        {
            _log.Warn("OutboxSend", "送信箱の項目に対応するアカウントがない");
            return (SendResult.Permanent, false);
        }

        SendResult result;
        var tooLarge = false;
        try
        {
            var message = MessageBuilder.Build(item, account.Email, _paths.AccountDir(item.AccountId));
            if (EncodedLength(message) > Constants.MaxEncodedMessageBytes)
            {
                tooLarge = true;
                result = SendResult.Permanent;
            }
            else
            {
                result = await _smtp.SendAsync(message, item.AccountId, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 終了時の待ち時間切れなど。失敗には数えない
            return (SendResult.Transient, false);
        }
        catch (Exception ex)
        {
            // 添付ファイルの欠落など、組み立てで失敗した場合
            _log.Error("OutboxSend", "メールの組み立てまたは送信で例外", ex);
            result = SendResult.Permanent;
        }

        switch (result)
        {
            case SendResult.Ok:
                await OnSucceededAsync(item).ConfigureAwait(false);
                break;
            case SendResult.AuthFailed:
                // パスワード入れ直しの流れへ回す。失敗回数には数えない
                _sync()?.MarkAuthFailed(item.AccountId);
                break;
            default:
                OnFailed(item, tooLarge, interactive);
                break;
        }
        return (result, tooLarge);
    }

    private async Task OnSucceededAsync(OutboxItem item)
    {
        _outbox.Remove(item.Id);
        if (item.SourceDraftId is { } draftId)
        {
            try
            {
                var svc = _draftService();
                if (svc != null) await svc.DeleteAsync(draftId).ConfigureAwait(false);
                else _drafts.Remove(draftId);
            }
            catch (Exception ex) { _log.Error("OutboxSend", "送信済みの下書きの削除に失敗", ex); }
        }
        UpdateFailureBanner();
        RaiseChanged();
        // Gmail が Sent に保存したコピーを取り込む（APPEND はしない）
        _ = TriggerSyncAsync(item.AccountId);
    }

    private async Task TriggerSyncAsync(string accountId)
    {
        try
        {
            var sync = _sync();
            if (sync != null) await sync.SyncNowAsync(accountId, SyncTrigger.AfterSend).ConfigureAwait(false);
        }
        catch (Exception ex) { _log.Error("OutboxSend", "送信後の同期に失敗", ex); }
    }

    private void OnFailed(OutboxItem item, bool tooLarge, bool interactive)
    {
        var count = item.RetryCount + 1;
        var notify = count >= Constants.SendFailureNoticeThreshold && !item.FailureNotified;
        _outbox.Update(item with { RetryCount = count, FailureNotified = item.FailureNotified || notify });
        if (tooLarge && (interactive || count == 1)) _dialogs.ShowNotice(Strings.WD_10, Strings.OB_06);
        // 3 回目の失敗で 1 回だけ赤い帯を出す（以降の再送では出さない）
        if (notify) _notifier.ShowBanner(BannerKind.OutboxFailure, Strings.OB_01);
        RaiseChanged();
    }

    /// <summary>失敗の目印が付いた項目が 1 件もなくなったら帯を消す。</summary>
    private void UpdateFailureBanner()
    {
        if (_accounts.GetAll().All(a => FailedCount(a.Id) == 0)) _notifier.HideBanner(BannerKind.OutboxFailure);
    }

    // ---- ヘルパー ----

    private OutboxItem? FindItem(long id)
    {
        foreach (var a in _accounts.GetAll())
        {
            var found = _outbox.GetAll(a.Id).FirstOrDefault(i => i.Id == id);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>添付を &lt;アカウント&gt;\&lt;kind&gt;\&lt;id&gt;\&lt;n&gt;.bin へコピーし、相対パスを入れた一覧を返す。元の名前は DB 側にだけ持つ。</summary>
    private IReadOnlyList<AttachmentInfo> CopyAttachments(string accountId, IReadOnlyList<AttachmentInfo> source, string kind, long id)
    {
        if (source.Count == 0) return source;
        var accountDir = _paths.AccountDir(accountId);
        var relDir = Path.Combine(kind, id.ToString());
        var dir = Path.Combine(accountDir, relDir);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        var result = new List<AttachmentInfo>(source.Count);
        for (var n = 0; n < source.Count; n++)
        {
            var a = source[n];
            if (string.IsNullOrEmpty(a.LocalPath)) throw new FileNotFoundException("添付ファイルの場所が未設定");
            var from = Path.IsPathRooted(a.LocalPath) ? a.LocalPath : Path.Combine(accountDir, a.LocalPath);
            var rel = Path.Combine(relDir, n + ".bin");
            File.Copy(from, Path.Combine(accountDir, rel), overwrite: true);
            result.Add(a with { LocalPath = rel });
        }
        return result;
    }

    /// <summary>送信時のバイト数（ヘッダ込み）。</summary>
    private static long EncodedLength(MimeKit.MimeMessage message)
    {
        using var counter = new CountingStream();
        message.WriteTo(counter);
        return counter.Length;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>書き込まれたバイト数だけ数えて捨てるストリーム。</summary>
    private sealed class CountingStream : Stream
    {
        private long _length;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override long Position { get => _length; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _length += count;
    }
}
