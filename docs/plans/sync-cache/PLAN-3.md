# sync-cache PLAN-3: on-demand fetch, offline behavior, backfill

Planning status: complete. Spec: detailed-spec 4, 5; Q30 context.

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Fetching older bodies and attachments when opened, offline "not downloaded yet" behavior with automatic fetch on reconnect, optional idle backfill.

## 2. Behavior

- Opening a message with `body_state = None`: if online, fetch body (by folder uid of any membership), store, then display; show a small busy text 「読み込んでいます…」. If offline: display 「このメールはまだ保存されていません。インターネットにつながると自動で表示します。」 and register the message in `pending_fetch` (memory set); when the next sync succeeds (online) the fetch runs and the preview updates if the message is still displayed.
- Attachments: file downloaded only on Save/Open (`IAttachmentService.EnsureLocalAsync(attachmentId)`); once stored as a file it stays cached. Offline and not downloaded: notice 「この添付ファイルはまだ保存されていません。インターネットにつながってから開いてください。」 (no auto-open after reconnect; Save/Open must be repeated, except forward uses the compose flow).
- Inline cid images: fetched with the body for 12-month mail; for older mail fetched together with the on-demand body (all inline parts with size <= 5 MB); stored as attachment files with `is_inline = 1`.
- Idle backfill (optional, included): when no sync is running and the app has been idle (no user input) for 60 s, fetch bodies of the oldest-unfetched messages, 20 per cycle, newest first, only while online; stops on any user action or sync start. Disabled when the displayed account is syncing.
- Rules never trigger old-body fetches (D12); search never triggers fetches (Q30).

## 3. Functions

- `IBodyFetcher.EnsureBodyAsync(accountId, gmMsgId, ct)` returns `BodyFetchResult { Ready, Offline, Failed }`.
- `IAttachmentService.EnsureLocalAsync(accountId, attachmentId, ct)` returns local path or error kind.
- `PendingFetchTracker.Add/Drain`.
- `BackfillWorker.Start/Stop`, idle detection via `InputManager.PreProcessInput` timestamp in App (interface `IIdleMonitor`).

## 4. Tests and done criteria

- Unit (fake gateway): offline then online drains pending fetch; attachment cached on second request without gateway call; backfill stops on user activity; backfill skips fetched bodies.
- Manual: open an old mail online/offline; reconnect fetch.
