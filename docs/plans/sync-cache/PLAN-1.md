# sync-cache PLAN-1: cache schema, repositories, folder mapping

Planning status: complete. Spec: detailed-spec 3, 4 (storage); A9, A52; PQ7, PQ8, Q42. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. cache.db schema
3. Folder mapping
4. Repository functions
5. Tests and done criteria

## 1. Scope

Per-account `cache.db` (`accounts\<id>\cache.db`): schema, migrations, repositories, attachment file store, folder view queries. No network code (see PLAN-2/3).

## 2. cache.db schema (user_version migrations, WAL)

```
folders(kind INTEGER PK, imap_name TEXT, uid_validity INTEGER, highest_uid INTEGER, highest_modseq INTEGER NULL, last_sync_utc INTEGER)
messages(gm_msgid INTEGER PK, message_id TEXT, subject TEXT, from_addr TEXT, from_name TEXT, to_json TEXT, cc_json TEXT,
         date_utc INTEGER, is_read INTEGER, has_attachments INTEGER,
         body_state INTEGER DEFAULT 0, body_text TEXT NULL, body_html TEXT NULL, derived_text TEXT NULL,
         search_key TEXT,                       -- normalized (case+width) sender+subject for LIKE
         body_key TEXT NULL)                    -- normalized body_text/derived_text for LIKE (set by SetBody)
message_folders(gm_msgid INTEGER FK CASCADE, folder INTEGER, uid INTEGER, PRIMARY KEY(gm_msgid, folder))
CREATE INDEX ix_mf_folder_uid ON message_folders(folder, uid)
attachments(id INTEGER PK AUTOINCREMENT, gm_msgid INTEGER FK CASCADE, part_id TEXT, file_name TEXT, content_type TEXT,
            size INTEGER, is_inline INTEGER, content_id TEXT NULL, local_path TEXT NULL)
outbox(id INTEGER PK, payload_json TEXT, attachment_dir TEXT, retry_count INTEGER, failure_notified INTEGER, created_utc INTEGER, source_draft_id INTEGER NULL)
drafts(id INTEGER PK, payload_json TEXT, attachment_dir TEXT, gmail_uid INTEGER NULL, dirty INTEGER, updated_utc INTEGER)
cache_meta(key TEXT PK, value TEXT)             -- initial_sync_done, etc.
pending_ops(id INTEGER PK, op TEXT, gm_msgid INTEGER, arg TEXT)   -- offline \Seen changes
```
- `uid_validity` stored per folder in `folders`; `message_folders.uid` is valid only for the stored validity (PQ7: on change the rows of that folder are deleted and re-resolved, messages/bodies/attachments kept).
- Attachment files: `attachments\<attachments.id>.bin`; `local_path` relative to the account dir. Original file name only in DB. Outbox/draft attachment copies: `outbox\<id>\<n>.bin`, `drafts\<id>\<n>.bin` (original names in payload_json).
- Body text/HTML are stored in DB (PQ8). Derived text computed once when the body is stored.
- Dates stored as UTC unix seconds (INTERNALDATE is used for ordering, the 12-month rule AND the displayed time, A68; the Date header is not used for display).

## 3. Folder mapping

- Resolve IMAP folder names from LIST special-use flags: `\Sent`, `\Drafts`, `\Trash`, `\Junk`, `\All`; INBOX literal. Store in `folders.imap_name`. If a special-use folder is missing (hidden in Gmail IMAP settings) log a warning (no content), show red banner [BN-03] 「Gmailの設定でIMAPの表示を確認してください。詳しい方に相談してください。」, and skip that folder.
- Folder view rule (Q42): view(F) = messages with a membership in F and, for F in {Inbox, Sent, Drafts}, no membership in Trash or Spam. AllMail membership is stored but never shown as a view.
- Outbox view = `outbox` table rows presented as list rows.

## 4. Repository functions

- `IMailRepository`: `UpsertHeader(msg, folder, uid)`, `GetFolderView(folder, limit/offset)`, `GetMessage(gmMsgId)`, `SetBody(gmMsgId, text, html, derived)`, `SetRead(gmMsgId, bool)`, `GetUnreadInboxCount()`, `GetMemberships(gmMsgId)`, `RemoveMembership(gmMsgId, folder)`, `AddMembership`, `DeleteMessage(gmMsgId)` (and its attachment files), `ResetFolderUids(folder)` (PQ7), `GetKnownUids(folder)`, `GetFolderState/SaveFolderState`.
- `IAttachmentStore`: `SaveFile(attachmentId, Stream)`, `OpenRead(attachmentId)`, `Delete(...)`.
- `IOutboxRepository`, `IDraftRepository`: CRUD with payload (de)serialization.
- `CacheDb.Open(accountId)` with `Migrate()`.
- Sort: date_utc DESC; paging via keyset (`date_utc, gm_msgid`).

## 5. Tests and done criteria

- Unit (in-memory/temp SQLite): view rule (Trash/Spam exclusion, All-Mail-only hidden), multi-folder single storage, `ResetFolderUids` keeps bodies, unread count excludes Trash/Spam, deletion removes attachment files, migrations, Japanese temp path.
