# sync-cache PLAN-2: sync engine

Planning status: complete. Spec: detailed-spec 4, 5; A11, A39, A43, A53; Q51, Q69, Q74; PQ7, PQ23. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Sync flow
3. Incoming-mail pipeline
4. Functions
5. Tests and done criteria

## 1. Scope

`ISyncService` per account: initial sync, periodic sync, UIDVALIDITY handling, mirror rules, read-state sync, mutual exclusion, timer, refresh button, offline detection and banner, hooks for block list/filter rules/outbox retry. MailKit implementation of `IImapGateway`.

## 2. Sync flow

- Timer: `SyncCoordinator` starts at app start (sync both accounts) then every 5 minutes; a tick is skipped while that account's sync runs. Manual refresh while running is merged (no second run) and the button shows busy. Refresh button state bound to the displayed account's sync state.
- Gateway: connect to `imap.gmail.com:993`, login with the stored password; timeouts 30 s; disconnect after each run (no IDLE). Network/auth failures: `Offline` state -> yellow offline banner [BN-01] 「インターネットにつながっていません。保存済みのメールは読めます。」; auth failure -> accounts-auth PLAN-2 flow. Banner clears on next success.
- Per run, per folder in order Inbox, Sent, Drafts, Trash, Spam, AllMail:
  1. SELECT; compare UIDVALIDITY with the stored value. First time: initial sync of that folder.
  2. Initial sync: fetch UID, X-GM-MSGID, FLAGS, INTERNALDATE, envelope, BODYSTRUCTURE (attachment presence) for all messages in batches of 200, writing progressively so the list fills while syncing (Q23). Light-blue banner [BN-02] 「メールを読み込んでいます…」 (the empty-folder text [ML-01] is NOT shown during this time) shown until the account's initial sync completes. Bodies: after headers, fetch bodies for messages with INTERNALDATE within 12 months, newest first, for Inbox/Sent/Drafts/Spam/Trash (AllMail only headers plus bodies for messages not present in other folders within 12 months). Body parse: choose text/plain and text/html parts; store; compute derived text; record attachment metadata (parts with disposition attachment or non-text inline). Block/filter rules NOT applied during initial sync (A43). After the account's initial sync finishes, set `initial_sync_done` = 1 in `cache_meta` (PLAN-1 schema).
  3. Periodic sync: if validity matches: fetch new UIDs > highest_uid (headers + body, always body for new mail, Q17); use CONDSTORE CHANGEDSINCE highest_modseq for flag changes when supported, otherwise fetch FLAGS for known UIDs in batches; detect expunged UIDs by comparing `UID SEARCH ALL` with known UIDs.
  4. Mirror: UID missing from folder -> remove that membership; if message has no memberships left and is not in AllMail -> delete message. A message removed from AllMail has been permanently deleted -> delete locally. Gmail trash move appears as membership change (Trash added, Inbox removed).
  5. UIDVALIDITY changed (PQ7): `ResetFolderUids(folder)`, list all UID+X-GM-MSGID of the folder, rebuild memberships for known msgids, fetch headers only for unknown msgids; none of these are "new" mail (no rules, no badge inflation); then continue as normal.
- Read state: remote \Seen changes update `is_read`; local opens/Mark read/unread are written by `SetSeenAsync(gmMsgId, bool)` to all folder memberships (STORE \Seen by UID) immediately when online; when offline queue in `pending_ops` (PLAN-1 schema) and flush at the next sync.
- Moves (delete, spam, restore, undo) require being online; offline they are refused with modal notice [BN-05] 「今はインターネットにつながっていないため操作できません。つながってからもう一度お試しください。」 (shown by folder-actions). A sync error that is not offline/auth shows the red banner [BN-19] 「メールの受信がうまくいきませんでした。しばらくしてから「更新」を押してください。」 (details only in the log).

## 3. Incoming-mail pipeline

New mail = message first seen after initial sync (and not from UIDVALIDITY re-listing). For each new message after its body is stored: (1) block list check -> move to Spam via IMAP and update memberships; stop; (2) filter rules top-to-bottom, first enabled match applies action (MoveToTrash/MoveToSpam/MarkRead); (3) update counts and raise `NewMailProcessed`. Pipeline calls `IIncomingRule` implementations registered by block-list and filter-rules (`IEnumerable<IIncomingRule>` in order block, filter). If a rule action fails (network) the message stays where it is and is logged.

## 4. Functions

- `ISyncService { Task SyncNowAsync(accountId, SyncTrigger, ct); SyncState State; event StateChanged; event SyncCompleted }`.
- `SyncCoordinator.Start()/Stop()`, `RequestRefresh(accountId)`.
- `ImapGateway : IImapGateway` (MailKit): `ListSpecialFolders`, `OpenFolder`, `FetchSummaries(range)`, `FetchBody(uid)`, `FetchPart(uid, partId)`, `SearchAllUids`, `StoreFlags`, `MoveTo(uids, dest)`, `Append(message, folder)`, `DeleteUids`.
- `IIncomingRule { Task<bool> ApplyAsync(MailMessage msg, RuleContext ctx) }` (returns true when the message was handled and later rules must stop).
- `BodyParser.Parse(MimeMessage)` -> text, html, derived text (HtmlAgilityPack: strip script/style, block elements to newlines, decode entities), attachments.

## 5. Tests and done criteria

- Unit with a fake `IImapGateway`: initial sync populates and does not run rules; periodic sync handles new, flag change, expunge; Trash move mirrored; UIDVALIDITY change keeps bodies and fires no rules; mutual exclusion (tick skipped, refresh merged); offline sets banner state; pending \Seen ops flush.
- Manual (test account): initial sync with real mailbox, progressive list, 5-minute cycle, move in Gmail web reflected, All-Mail-only message searchable later.
