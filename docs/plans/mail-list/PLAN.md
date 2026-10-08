# mail-list

Planning status: complete. Spec: detailed-spec 1 (list), 3 (view rule), 4 (read state); A45; Q17 (bold), P2 defaults.

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

List pane: folder view binding, rows, selection, unread count, bottom action-button strip, date formatting, empty states. Search UI is in `search`; folder-specific actions in `folder-actions`.

## 2. Behavior

- Pane layout (top to bottom): search box area (owned by `search`), list, bottom buttons in order: 新しいメール (compose), 検索, 設定, アドレス帳, 受信拒否リスト, 振り分けルール (labels in `Strings`; a click on 検索 focuses the search box, A47).
- Row: sender (display name, else address), subject, date/time, attachment icon; unread rows in bold. Row text uses `AppFontSize`. Row height adapts to font size.
- Data: `ListViewModel.LoadFolder(folderKind)` queries `GetFolderView` (cache) sorted by date DESC; virtualized `ListView` (`VirtualizingStackPanel`), incrementally loaded 200 rows at a time as the user scrolls (keyset paging). Reloads after sync completion and after local actions while keeping the selection when the message still exists.
- Outbox folder: rows come from the `outbox` table (sender column shows the first recipient; date = created time; failed items show a red marker 「送信できませんでした」).
- Drafts rows: recipients shown in the sender column (「宛先: …」) when sender is the account itself.
- Sent rows: show recipient in the sender column (「宛先: …」).
- Date format (Q15): today `HH:mm`; this year `M月d日 H:mm`; older `yyyy年M月d日 H:mm`; local time via `IClock`.
- Selecting a message raises `IAppState.SelectedMessageChanged`; selecting marks it read (A11) after the body is displayed (delegated to mail-display).
- Inbox button text: `受信トレイ (N)` where N = unread Inbox count when > 0; refreshed after sync and after read changes.
- Empty folder: centered 「メールはありません」.
- Keyboard: Up/Down changes selection; Delete key = Delete action (folder-actions).

## 3. Functions

- `ListViewModel { Items, SelectedItem, LoadFolder(kind), Refresh(), LoadMore() }`, `MailListItem { GmMsgId?, OutboxId?, Sender, Subject, DateText, IsUnread, HasAttachment, FolderLabel? }`.
- `DateFormatter.Format(DateTimeOffset utc, DateTimeOffset now)`.
- `UnreadCounter.GetInboxUnread()`.

## 4. Tests and done criteria

- Unit: DateFormatter for today/this year/older/midnight edge/year boundary; keyset paging; selection preserved on reload; unread count.
- Manual: scrolling through a large mailbox is smooth; bold unread; Japanese subjects and senders display correctly.
