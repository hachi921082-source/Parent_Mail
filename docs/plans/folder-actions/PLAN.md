# folder-actions

Planning status: complete. Spec: detailed-spec 9, 1 (header buttons); A3, A38; Q22, Q27, Q40.

## Index

1. Scope
2. Action matrix
3. Behavior
4. Functions
5. Tests and done criteria

## 1. Scope

Context menus and the four header buttons per folder, Delete with Undo notice, Spam/Not spam, Restore, Mark read/unread, Add sender, Block sender wiring, folder switching from the top bar.

## 2. Action matrix (context menu; header buttons Reply/Reply all/Forward/Delete are enabled only where the same action exists)

| Folder | Items | Header buttons enabled |
|---|---|---|
| Inbox | 返信, 全員に返信, 転送, 既読/未読にする, 迷惑メールにする, 削除, アドレス帳に追加, 受信拒否に追加 | Reply, Reply all, Forward, Delete |
| Sent | 転送, 削除 | Forward, Delete |
| Drafts | 編集, 削除 | Delete |
| Spam | 迷惑メールではない(受信トレイへ), 削除 | Delete |
| Trash | 元に戻す(受信トレイへ) | none |
| Outbox | 今すぐ送る, 下書きに戻す | none |

No selection: all header buttons disabled (A3).

## 3. Behavior

- Delete (button, menu, Delete key): requires online; IMAP move of the message from the folder it is viewed in to Trash (all memberships replaced by Trash: Gmail semantics = remove labels, add Trash; implementation = MOVE from the viewed folder's uid to Trash, then re-sync that message's memberships). No confirmation. Local update immediately (optimistic): membership changes in cache, list reloads and selection moves to the next row. Notice (A38) 「ごみ箱に移動しました [元に戻す]」 for 5 seconds via `INotifier.ShowNotice`.
- Undo (Q27): stores the previous membership set (folder kinds and uids) of the message at delete time; Undo moves the message back from Trash to the folder it was viewed in. Other Gmail labels it may have had are not restored (only the viewed folder is). Undo is available only during the 5 s notice. A second Delete replaces the notice (only the latest is undoable).
- Move to Spam: MOVE to Spam (no notice; list updates). Not spam: MOVE Spam -> Inbox. Restore (Trash): MOVE Trash -> Inbox always (Q27).
- Mark read/unread: toggles per current state, immediate local update, `SetSeenAsync`.
- Add sender to address book: opens the address-book add dialog prefilled (name = sender name, email = sender address) for the displayed account; if the address already exists, show 「すでに登録されています」.
- Block sender: dialog asking 「このアドレスだけ」/「このドメイン全体」 (default address), adds to the block list, shows 「受信拒否に追加しました」. Existing mail is not moved (A25).
- Drafts Edit: open compose with the draft (compose PLAN-2). Outbox: calls outbox-send.
- Offline: move-type actions refused with 「今はインターネットにつながっていないため操作できません」; Mark read/unread allowed (queued).
- Top-bar folder buttons: click selects folder, loads list (mail-list), clears selection, resets zoom, clears search; the active folder button is highlighted.
- Failed IMAP move: revert local change and show 「操作できませんでした。もう一度お試しください。」.

## 4. Functions

- `IFolderActionService { DeleteAsync(accountId, gmMsgId, fromFolder); UndoDeleteAsync(token); MoveToSpamAsync; NotSpamAsync; RestoreAsync; ToggleReadAsync }`.
- `ContextMenuBuilder.Build(FolderKind, MailMessage?)` returning items with enabled state; `HeaderButtonState.For(folder, selected)`.
- `UndoToken { GmMsgId, FromFolder, Uid, ExpiresUtc }`.

## 5. Tests and done criteria

- Unit (fake gateway): matrix per folder; delete + undo restores membership; undo after expiry not offered; offline refusal; failed move reverts; Restore always Inbox.
- Manual: delete from Inbox, undo within 5 s, check in Gmail web; spam/not spam; context menus per folder.
