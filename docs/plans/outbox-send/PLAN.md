# outbox-send

Planning status: complete. Spec: detailed-spec 5, 7, 16; A13-A15, A40, A46, A53; Q22, Q64, Q75, PQ25, Q43. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Outbox storage, immediate SMTP submission, retry, failure notices and markers, Outbox actions (Send now, Return to drafts), send-then-quit hook.

## 2. Behavior

- `EnqueueAndSendAsync(snapshot)`: write an `outbox` row (payload JSON incl. recipients, subject, body, `In-Reply-To`/`References`, attachment copies under `outbox\<id>\`), keep the draft linkage (`source_draft_id`), then start a send task immediately. The compose window has already closed.
- Message build (`MessageBuilder`): MimeKit `MimeMessage`; From = account address only (PQ18); Date now; Message-ID generated with the account's domain; text/plain UTF-8; attachments with original names (RFC 2231 encoding for non-ASCII); BCC recipients are in the envelope but not the headers.
- SMTP: `smtp.gmail.com:465` SSL, app password, timeout 30 s. Success: delete the Outbox row and files, delete the linked draft (A46), trigger a sync for the account so Sent appears (Gmail stores the copy; no APPEND).
- Offline/unreachable/failure: the row stays; `retry_count` incremented on each failed attempt (persisted, A14); retry runs at the start of every sync cycle (5 min) and on reconnect/next successful sync.
- Failure notice (Q22, PQ25, A13, A40): when `retry_count` reaches 3 and `failure_notified = 0`: set `failure_notified = 1`, show red banner [OB-01] 「メールを送れませんでした。送信トレイを確認してください。」 once (the only banner with a 「×」 close button [TB-25]; after closing it does not return for the same failure), and mark the Outbox row (red pill [OB-02] 「送信できませんでした」) and the Outbox top-bar button (red badge !N [TB-08]). Retries continue every cycle after 3; no repeated notice; the marker stays until the item is sent or returned to drafts. Authentication failure -> accounts-auth PLAN-2 flow (not counted as a failure notice).
- Not counted as failures: offline detection before any attempt (no network) increments nothing until an actual SMTP attempt fails.
- Outbox right-click menu (right-click only, no header buttons; Q63): [OB-03] 「今すぐ送る」 (attempt now, resets nothing, shows busy text [OB-07] 「送っています…」; failure gives modal notice [OB-05] 「まだ送れませんでした。しばらくしてからもう一度お試しください。」), [OB-04] 「下書きに戻す」 (move payload into `drafts` as dirty local draft, delete Outbox row, open Drafts view message; draft linkage kept).
- Send-then-quit hook (app-lifecycle): `SendAllPendingAsync(timeout 60 s)` for both accounts; returns `AllSent` or `SomeFailed`.
- Outbox count of unsent items (both accounts) is exposed for the quit warning.
- Message size is rechecked at build (estimator) before SMTP; over-limit -> stays Outbox with modal notice [OB-06] 「メールが大きすぎて送れません」 (should not occur as compose checks it).

## 3. Functions

- `IOutboxService { EnqueueAndSendAsync(snapshot); RetryDueAsync(accountId); SendNowAsync(outboxId); ReturnToDraftsAsync(outboxId); SendAllPendingAsync(ct); int PendingCount(); int FailedCount(accountId) }`.
- `MessageBuilder.Build(OutboxItem, accountEmail)`.
- `SmtpGateway.SendAsync(MimeMessage, accountId, ct)` returns `SendResult { Ok, AuthFailed, Transient, Permanent }`.
- Sync coordinator calls `RetryDueAsync` before each account's sync run.

## 4. Tests and done criteria

- Unit (fake SMTP): success removes row and deletes draft; failures increment counter and persist across a simulated restart; notice only once at 3; retries continue after 3; Return to drafts moves payload; build output has no display name in From, BCC absent from headers, Japanese attachment name encoded.
- Manual (test account): send to the account itself, appears in Sent; break the network to see Outbox behavior and the failure marker; Send now works after recovery.
