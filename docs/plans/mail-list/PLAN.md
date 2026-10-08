# mail-list

Planning status: complete (updated 2026-10-08). Spec: detailed-spec 1 (list), 3 (view rule), 4 (read state), 22 (multi-select); Q44, Q61, Q62, Q69, Q71, Q74; A11, A52, A58-A65, A67, A68; PQ17 (bold). Layout: `window-layouts.md` 2.2. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Multi-select
4. Functions
5. Tests and done criteria

## 1. Scope

List pane: folder view binding, rows, checkboxes and multi-select state, selection, unread count, bottom action-button strip, date formatting, empty states. Search UI is in `search`; folder actions (including the delete itself) in `folder-actions`.

## 2. Behavior

- Pane layout (top to bottom): search box + クリア (owned by `search`), list header row, rows, bottom strip. The strip is a 2 x 3 grid (app-shell PLAN 3.2) in the order 新しいメール (primary), 検索, 設定, アドレス帳, 受信拒否リスト, 振り分けルール [TB-14..TB-19]; a click on 検索 focuses the search box (A47). Text cap `ChromeTextMaxPt` = 20 pt.
- Row (Q61): two lines, minimum height 2 text lines + 16 DIP.
  - Checkbox at the left (48 x 48 hit target, accessible name [ML-08]); ALWAYS shown, except in Trash and Outbox where the checkbox column is hidden (A62).
  - Line 1: sender (display name, else address; [ML-12] 「(差出人なし)」 when none) left with ellipsis; date/time right-aligned; paperclip icon after the date when the mail has attachments (tooltip [ML-07]). Sent/Drafts: [ML-02] 「宛先: <first recipient>」, with more recipients [ML-14] 「宛先: <first recipient> ほか N 人」 (A67), in the sender position.
  - Line 2: subject with ellipsis ([ML-13] 「(件名なし)」 when empty).
  - Unread = bold on both lines only (no dot, no colour). Selected row = fill `RowSelected` (#D6E4F7) + 6 px left bar #0B4FA8. Row text uses `AppFontSize`.
- Data: `ListViewModel.LoadFolder(folderKind)` queries `GetFolderView` (cache) sorted by INTERNALDATE DESC (A68: sort key and displayed time are both INTERNALDATE); virtualized `ListView` (`VirtualizingStackPanel`), incrementally loaded 200 rows at a time as the user scrolls (keyset paging). Reloads after sync completion and after local actions; selection kept when the message still exists; new mail arriving by sync keeps the scroll position and the current selection (A52).
- Outbox folder: rows come from the `outbox` table (sender position shows the first recipient; time = created time; failed items show a red pill marker [ML-03] 「送信できませんでした」); no checkboxes.
- Date format (Q44): today `HH:mm` [ML-04]; this year `M月d日 H:mm` [ML-05]; older `yyyy年M月d日 H:mm` [ML-06]; local time via `IClock`.
- Selection and read marking (Q69, Q74, A58): selecting a row raises `IAppState.SelectedMessageChanged`. When the USER selects a row (click, or Up/Down/Enter key on the list), the mail is marked read immediately on selection, only in Inbox and Spam (mail-display PLAN-1 performs `SetSeenAsync`). A selection made AUTOMATICALLY by the app (after delete / move to Spam / not spam / restore, after reload) is visual only and does NOT mark read; `SelectionSource { User, Auto }` is carried in the event.
- After delete / Move to Spam / Not spam / Restore the selection moves to the NEXT OLDER mail; the previous (newer) one if there is none; if the list is empty, nothing is selected and the preview shows the guide [PV-12] 「左の一覧からメールを選んでください」 (Q62). Selection source = Auto.
- Inbox button badge: red number badge with N = unread Inbox count when > 0 [TB-07] (top bar owned by app-shell); refreshed after sync and after read changes.
- Empty folder: centered [ML-01] 「メールはありません」; NOT shown while the account's first sync is running (only the loading banner is shown, Q62). List load failure: modal notice [BN-24].
- Keyboard: Up/Down changes selection; Delete key = Delete action only while the list has focus (folder-actions); Space toggles the check of the focused row; F5 refresh.

## 3. Multi-select (detailed-spec 22; A59-A65)

- State: `CheckedIds` (set of message ids / result keys) in `ListViewModel`, independent of the selected row. Clicking the row selects it (preview); clicking its checkbox only toggles the check; both can hold (A59). Checks do not mark mail read.
- List header row (48 DIP): 「すべて選ぶ」 checkbox [ML-09] shown in Inbox, Sent, Spam, Drafts and search results (hidden in Trash and Outbox). It checks the rows currently in the list, up to 200; unchecking also clears. While at least one row is checked the header also shows [ML-10] 「N通を選んでいます」 and the button [ML-11] 「選択をやめる」.
- Limit: more than 200 checked rows -> the delete is refused with modal notice [BN-22] (checked by folder-actions at delete time).
- Preview while checks exist: mail-display shows [PV-13] and header buttons other than Delete are disabled; when all checks are cleared the selected mail is shown again (A60).
- Checks are cleared on folder, account or search change and after a delete; they are kept across a sync reload (checked mails that disappeared are dropped) (A65).
- Exposes `ListViewModel.CheckedCount`, `GetCheckedItems()`, `ClearChecks()` to folder-actions and mail-display.

## 4. Functions

- `ListViewModel { Items, SelectedItem, CheckedIds, CheckedCount, LoadFolder(kind), Refresh(), LoadMore(), SelectNextAfterRemoval(removedIds), ClearChecks(), ToggleCheck(item), SelectAllVisible() }`, `MailListItem { GmMsgId?, OutboxId?, Sender, Subject, DateText, IsUnread, HasAttachment, FolderLabel?, IsChecked, ShowCheckbox }`.
- `SelectionChangedArgs { Item, SelectionSource Source }`.
- `DateFormatter.Format(DateTimeOffset utc, DateTimeOffset now)`.
- `RecipientSummary.Format(recipients)` -> 「宛先: first」 or 「宛先: first ほか N 人」.
- `UnreadCounter.GetInboxUnread()`.

## 5. Tests and done criteria

- Unit: DateFormatter for today/this year/older/midnight edge/year boundary; keyset paging; selection preserved on reload; unread count; RecipientSummary; next-older selection rule (older, else previous, else none) with source Auto not marking read and source User marking read in Inbox/Spam only; check state: select-all up to 200, cleared on folder change, kept on reload, hidden in Trash/Outbox; empty message not shown during first sync.
- Manual: scrolling through a large mailbox is smooth; two-line rows with ellipsis; bold unread; checkboxes and 「すべて選ぶ」 behave; Japanese subjects and senders display correctly; the 2 x 3 strip wraps 受信拒否リスト at the 20 pt cap.
