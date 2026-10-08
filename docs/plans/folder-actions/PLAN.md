# folder-actions

Planning status: complete (updated 2026-10-08). Spec: detailed-spec 9, 1 (header buttons), 22 (multi-select delete); A3, A51, A58-A65, A76; Q22, Q40, Q55, Q62, Q63, Q64, Q71, Q74, Q76; PQ27. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Action matrix
3. Behavior
4. Multi-select delete
5. Functions
6. Tests and done criteria

## 1. Scope

Context menus and the header buttons per folder, Delete (single and multi-select) with Undo notice, Spam/Not spam, Restore, Mark read/unread, Add sender, Block sender wiring, folder switching from the top bar.

## 2. Action matrix

Right-click menu items (rows are the same size as buttons, min height 48 DIP) and enabled preview-header buttons. Header buttons: 返信, 全員に返信, 転送, 削除 [TB-20]; 編集 only in Drafts [TB-24]. Preview header layout: 返信/全員に返信/転送 (and 編集) at the left, zoom group in the middle (mail-display PLAN-2), 削除 at the FAR RIGHT, apart from the others, red outline (`DeleteOutline`; A51).

| Folder | Right-click items | Header buttons enabled |
|---|---|---|
| Inbox | 返信, 全員に返信, 転送 [FA-01]; 既読にする/未読にする [FA-02]; 迷惑メールにする [FA-03]; 削除 [FA-04]; アドレス帳に追加 [FA-05]; 受信拒否に追加 [FA-06] | 返信, 全員に返信, 転送, 削除 |
| Sent | 転送, 削除 | 転送, 削除 |
| Drafts | 編集 [FA-07], 削除 | 編集, 削除 |
| Spam | 迷惑メールではない [FA-08], 削除 | 削除 |
| Trash | 受信トレイに戻す [FA-09] | none |
| Outbox | 今すぐ送る [OB-03], 下書きに戻す [OB-04] | none |

迷惑メールではない, 受信トレイに戻す, 今すぐ送る and 下書きに戻す are right-click only (Q63). No selection: all header buttons disabled (A3). While rows are checked (multi-select) only 削除 is enabled, labeled [TB-23] 「選んだN通を削除」.

## 3. Behavior

- Delete (button, menu, Delete key with list focus): requires online. IMAP MOVE of the mail from the folder it is viewed in to Trash. No confirmation. Local update immediately (optimistic): membership changes in cache, list reloads. After the delete, the NEXT OLDER mail is selected automatically; the previous one if none; the guide text [PV-12] if the list is empty (Q62). This auto-selection is visual only and does NOT mark the mail read (A58, Q74); only a selection by the user's explicit click or key marks read (Inbox/Spam only; mail-list PLAN).
- Single delete notice: transient notice [BN-07] 「ごみ箱に移動しました [元に戻す]」 for `UndoNoticeSeconds` = 10 s (Q64) via `INotifier.ShowNotice` (colour `NoticeOk`).
- Undo (Q55, PQ27): `UndoToken` stores ONLY the folder the mail was viewed in (and its uid) for each deleted mail. Undo moves each mail back from Trash to that folder; other Gmail labels it may have had are not restored. Undo is available only during the 10 s notice. A new delete replaces the notice (only the latest delete is undoable). Offline Undo is refused with [BN-05].
- Move to Spam: MOVE to Spam, no notice, list updates, next-older auto-selection (no mark-read). Not spam: MOVE Spam -> Inbox (same selection rule). Restore (Trash): MOVE Trash -> Inbox always (PQ27, same selection rule).
- Mark read/unread: toggles per current state, immediate local update, `SetSeenAsync`; allowed offline (queued).
- Add sender to address book: opens the address-book add dialog [WD-12] prefilled (name = sender name, email = sender address) for the displayed account; if the address already exists show modal notice [FA-12] 「すでに登録されています」.
- Block sender: dialog [WD-16] (title 「受信拒否に追加」, buttons 追加* / やめる) with the options [FA-10] 「このアドレスだけ」/「このドメイン全体」 (default address); adds to the block list and shows transient notice [FA-11] 「受信拒否に追加しました」 (4 s). Existing mail is not moved (A25).
- Drafts 編集: open compose with the draft (compose PLAN-2). Outbox actions call outbox-send.
- Offline: move-type actions (delete, spam, restore, undo) are refused with modal notice [BN-05] 「今はインターネットにつながっていないため操作できません。つながってからもう一度お試しください。」 (title 「お知らせ」, button 「OK」).
- Failed IMAP move: revert the local change and show modal notice [BN-06].
- Top-bar folder buttons: click selects folder, loads list (mail-list), clears selection, checks and search, resets zoom; the active folder button is drawn pressed.

## 4. Multi-select delete (detailed-spec 22)

- Which mails: with no row checked, 削除 deletes the selected mail (single delete above). With one or more rows checked, it deletes exactly the checked rows; the selected row is ignored unless also checked (A59). Right-click 削除 on a checked row deletes all checked rows; on an unchecked row only that row (A76). Delete key (list focus) follows the same rule as the button.
- Limit: more than 200 checked -> nothing happens except modal notice [BN-22] 「一度に削除できるのは200通までです。選びなおしてください。」.
- Folders: Inbox, Sent, Spam, Drafts and search results (Trash and Outbox have no checkboxes). Search results: checked mails already in Trash are skipped; each other mail is moved from the folder shown on its result row; a mail that was only in All Mail is moved from All Mail and returns to All Mail only on Undo (A63).
- Execution: group by source folder, IMAP MOVE each group to Trash; local state is updated per mail as the moves succeed.
- Result: all succeeded -> transient notice [BN-11] 「N通をごみ箱に移動しました [元に戻す]」 for 10 s; checks cleared; next-older auto-selection after the oldest deleted position (previous if none; guide if empty), no mark-read.
- Partial failure (A64): mails that were moved stay moved; the ones that failed are restored in the list; modal notice [BN-21] 「N通のうちM通を移せませんでした。もう一度お試しください。」; the Undo notice (if shown) covers the moved ones only.
- Offline: [BN-05], nothing changes.
- Undo: `UndoToken` is a list of `UndoEntry { GmMsgId, FromFolder, Uid }` plus `ExpiresUtc`; one latest undo notice.

## 5. Functions

- `IFolderActionService { DeleteAsync(accountId, IReadOnlyList<DeleteTarget> targets); UndoDeleteAsync(token); MoveToSpamAsync; NotSpamAsync; RestoreAsync; ToggleReadAsync }`, `DeleteTarget { GmMsgId, FromFolder, Uid }`.
- `ContextMenuBuilder.Build(FolderKind, MailMessage?, bool isChecked, int checkedCount)` returning items with enabled state; `HeaderButtonState.For(folder, selected, checkedCount)` (also yields the Delete label).
- `UndoToken { IReadOnlyList<UndoEntry> Entries; DateTimeOffset ExpiresUtc }`.

## 6. Tests and done criteria

- Unit (fake gateway): matrix per folder (Drafts header = 編集+削除; Spam/Trash/Outbox menus right-click only); single delete + undo restores the viewed folder only; multi-delete grouping, limit 200, skip Trash in search, All-Mail-only undo, partial failure (moved stay moved, modal text with N and M), right-click on checked vs unchecked row; undo after expiry not offered; offline refusal for delete and undo; failed move reverts; Restore always Inbox; auto-selection after delete does not mark read.
- Manual: delete from Inbox, undo within 10 s, check in Gmail web; multi-delete of several checked mails; spam/not spam; context menus per folder; 削除 sits at the far right in red outline.
