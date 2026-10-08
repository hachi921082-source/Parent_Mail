# compose PLAN-2: drafts, auto-save, close dialog

Planning status: complete. Spec: detailed-spec 7, 4, 5; A12, A20, A32, A46, A56, A71, A74, A75; Q20, Q37, Q75; D15. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Gmail Drafts mechanics
4. Functions
5. Tests and done criteria

## 1. Scope

Draft model, 30-second auto-save, Gmail Drafts upload (online and offline), close dialog, editing drafts, draft cleanup after send, Drafts folder sync interplay.

## 2. Behavior

- Auto-save: every 30 s while the window has content and has changed since the last save (`DraftAutoSaveSeconds`). Saves locally first (`drafts` row + attachment copies), then uploads to Gmail Drafts when online; offline the row stays `dirty = 1` and uploads after reconnect (A12) in the next sync run.
- Close (X, Alt+F4, Esc is not bound): no content (all fields empty, no attachments) -> close silently, and any auto-saved draft row for this window is removed (A20). Otherwise always show the 3-button dialog [WD-23] (Q20; title [CP-15] 「書きかけのメールがあります」 / message [CP-15] 「書きかけのメールがあります。」; MODAL to the compose window): buttons left to right [CP-12] 「下書きに保存」 (DEFAULT, Enter; Q75/A75 - the only exception to "Enter = cancel-type button", because saving loses nothing), [CP-13] 「保存しないで閉じる(前に自動保存された下書きは残ります)」, [CP-14] 「戻る」; Esc = 戻る. Auto-save keeps running while the dialog is open.
  - 下書きに保存: immediate save+upload attempt (offline: local dirty), close.
  - 保存しないで閉じる: discard edits made after the last auto-save; the last auto-saved draft remains (Q37); if no auto-save has happened, nothing remains.
  - 戻る: back to the window.
- Clearing everything in the window after an auto-save already created a draft: the auto-saved draft is deleted on the next auto-save tick or at close (A71).
- Draft upload failure during auto-save is silent: retried each cycle, and the compose window shows the status line [BN-18] 「下書きはこのパソコンに保存しています。インターネットにつながると自動で保存します。」 (in-place text). Only when the user explicitly chose 下書きに保存 and the save fails, show modal notice [BN-17] 「下書きを保存できませんでした。もう一度お試しください。」 (title 「お知らせ」, button 「OK」) and keep the window open (A74).
- After a successful send the corresponding draft (the window's draft row, and the Gmail Drafts copy) is deleted (A46); a failed send keeps the Outbox item and the draft linkage (`source_draft_id`) until it finally succeeds.
- Edit draft (Drafts > Edit): opens `ComposeWindow` with `ComposeInit.SourceDraftId` = the Gmail draft message loaded from cache (To/CC/BCC from headers, plain body from `body_text` or derived text, attachments fetched if present). Further auto-saves replace that same draft.
- Quit with open compose windows (A32): app-lifecycle asks each window to run its close dialog sequentially; a 戻る answer cancels the quit.

## 3. Gmail Drafts mechanics

- A draft is a MIME message appended to `[Gmail]/Drafts` with `\Draft` flag; identity = the draft's own `Message-ID` header (generated at first save, stable across versions) stored in `drafts.payload_json`.
- Update = APPEND the new version then delete the old UID (STORE `\Deleted` + EXPUNGE via UID), after the APPEND succeeds. The new UID is stored in `drafts.gmail_uid`. If the old UID is unknown (sync not yet seen), look it up by `HEADER Message-ID` search.
- Deleting a draft (after send, or the empty-close case) deletes its Gmail copy the same way (local-only drafts just delete the row).
- Sync of the Drafts folder (sync-cache) stores Gmail drafts as ordinary messages; drafts created by this app appear once the next sync lists them; the local `drafts` row is matched by Message-ID and then removed from the "pending local" set to avoid duplicates in the Drafts view (view = cache messages in Drafts membership union local dirty drafts not yet uploaded).

## 4. Functions

- `IDraftService { SaveAsync(DraftSnapshot, bool uploadNow); DeleteAsync(draftId); FlushDirtyAsync(accountId); OpenForEditAsync(gmMsgId) }`.
- `DraftAutoSaver (DispatcherTimer 30 s)` per `ComposeViewModel`.
- `CloseDialogResult ShowCloseDialog()`.
- `ComposeViewModel.HasContent`, `IsDirtySinceLastSave`.

## 5. Tests and done criteria

- Unit (fake gateway): autosave timing with fake clock, no save when unchanged, offline marks dirty then uploads, APPEND-then-delete ordering, empty close leaves nothing, discard keeps last autosave, deletion after send, Message-ID stability.
- Manual (test account): draft shows in Gmail web Drafts; editing replaces rather than duplicates; offline draft uploads after reconnect.
