# Pending PLAN Edits

Edit list for developer-lite (written by designer-lite, 2026-10-08). PLAN files and `plans/README.md` were NOT edited by designer-lite. ALL parts are approved and are to be applied now (2026-10-08, Q58-Q78). Part A is editorial; part B was formerly "waiting for approval" and is now approved; part C lists the decided changes (user decisions Q58-Q78), including the two late changes (A58, A75).

## Index

A. Editorial fixes (apply now)
B. Approved edits (apply now)
C. Decided changes to write into the PLANs

## A. Editorial fixes (apply now)

### A1. Q-number clash

`plans/README.md` section 9 numbers the planning questions Q1-Q30, but `docs/QA.md` also has Q1-Q72 with different meanings. Rename the README ones to PQ1-PQ30 ("PQ" so that it does not clash with the informal "P2 defaults" wording), and fix the references in the PLAN headers and bodies listed here. References to QA.md numbers stay as they are.

| File | Current | Change to |
|---|---|---|
| `plans/README.md` section 9 | `Q1` ... `Q30` | `PQ1` ... `PQ30` (and the intro: "PQ = planning questions; Q = docs/QA.md") |
| `accounts-auth/PLAN-1.md` header and Save-flow line | `Q28` | `PQ28` |
| `app-lifecycle/PLAN.md` header and the SessionEnding line | `Q16` | `PQ16` (Q34 and Q43 stay: QA.md) |
| `app-shell/PLAN.md` header and the log line | `Q21, Q29` / `(Q29)` | `PQ21, PQ29` / `(PQ29)` |
| `block-list/PLAN.md` header | `Q24 (planning answer ...)` | `PQ24 (...)` |
| `compose/PLAN-1.md` header; section 3 title; From and body lines | `Q18, Q19` / `(Q11, A42)` / `(Q18)` / `(Q19)` | `PQ18, PQ19` / `(PQ11, A42)` / `(PQ18)` / `(PQ19)` |
| `filter-rules/PLAN.md` header | `Q24 (...)` | `PQ24 (...)` |
| `folder-actions/PLAN.md` header and Undo/Restore lines | `Q27` | `PQ27` (Q22 and Q40 stay: QA.md) |
| `mail-display/PLAN-1.md` header | `Q14` | `PQ14` (Q39 stays) |
| `mail-display/PLAN-2.md` header and the HTML-mode line | `Q9, Q10, Q17` / `(reference 12 pt, Q10)` | `PQ9, PQ10, PQ17` / `(... PQ10)` |
| `mail-list/PLAN.md` header and the date line | `Q17 (bold)` / `Date format (Q15)` | `PQ17` / `(PQ15)` |
| `outbox-send/PLAN.md` header; message-build line; failure-notice line | `Q22, Q25, Q43` / `(Q18)` / `(Q22, Q25, ...)` | `Q22, PQ25, Q43` / `(PQ18)` / `(Q22, PQ25, ...)` |
| `reply-forward/PLAN.md` header and the forward-attachments line | `Q26` | `PQ26` |
| `safety-warnings/PLAN-1.md` header and lines 26 and 31 | `Q13, Q14` / `(Q14)` | `PQ13, PQ14` / `(PQ14)` (Q23 and Q24 stay: QA.md) |
| `safety-warnings/PLAN-2.md` header | `Q22 (extended list, spec change)` | `PQ22 (extended list)` (Q25 and Q41 stay) |
| `search/PLAN.md` header | `Q20 (planning answer: LIKE)` | `PQ20 (LIKE)`; also add `Q47` |
| `settings/PLAN.md` header and the ZoomFactor line | `Q6, Q9` / `(Q10)` | `PQ6, PQ9` / `(PQ10)` |
| `sync-cache/PLAN-1.md` header and lines 35, 37 | `Q7, Q8` | `PQ7, PQ8` (Q42 stays) |
| `sync-cache/PLAN-2.md` header and the UIDVALIDITY line | `Q7, Q23` | `PQ7, PQ23` (the `Q17` on the new-mail body line stays: QA.md) |

### A2. Stale assumption citations in PLAN headers

| File | Current | Change |
|---|---|---|
| `mail-list/PLAN.md` header | `A45` | `Q44` (A45 superseded) |
| `search/PLAN.md` header | `A28` | `Q47` (A28 superseded) |
| `sync-cache/PLAN-2.md` header | `A10` | `Q51` (A10 superseded) |
| `mail-display/PLAN-2.md` header | `A33-A37, A41 (changed, see README section 10)` | `A33-A35; Q45, Q46, Q69` (A36 and A41 superseded, A37 confirmed) and the line "changed from A36/A37" -> "(Q46)" |
| `folder-actions/PLAN.md` header and Delete line | `A38` and `(A38)` 5 seconds | `Q64` (A38 superseded: 10 seconds) |
| `plans/README.md` section 1 table | rows citing A41, A45, A28, A38, A10 | the replacement references above |

### A3. `plans/README.md` other corrections

- Section 10 ("Spec changes needing designer-lite update") is stale: all 22 items are applied in spec.md / detailed-spec.md / QA.md (Q44-Q57). Replace the section body with "Applied 2026-10-08 (QA.md Q44-Q57); see QA.md." and keep the heading.
- Section 9, line `Q27` (becomes PQ27): "Restore -> Inbox always; Undo -> previous folder/label set" is wrong. Correct text: "Restore -> Inbox always; Undo -> the folder the mail was viewed in only (other Gmail labels are not restored; QA.md Q55)."
- Section 6 constants: `UndoNoticeSeconds` 5 -> 10 (Q64).
- Section 9 line "Priority-2 defaults": replace "P2" wording by "priority-2 defaults" to avoid confusion with the PQ numbers.

### A4. `folder-actions/PLAN.md` Undo storage

"Undo (Q27): stores the previous membership set (folder kinds and uids) of the message at delete time" must store ONLY the folder the mail was viewed in (and its uid): `UndoToken { GmMsgId, FromFolder, Uid, ExpiresUtc }` already says so; fix the sentence and the header ("Q27" -> "PQ27, Q55"). Also the Undo token becomes a list of such entries for multi-delete (part C).

### A5. `ui-strings.md` is the source for wording

In `plans/README.md` section 8 the sentence "the reviewed list and the source of truth for wording once the user approves it" stays, but add: "The whole file is approved (Q76)."

## B. Approved edits (apply now)

| File | Edit | Depends on |
|---|---|---|
| `app-shell/PLAN.md` | Top bar in two rows (row 1 account + checkbox + refresh, row 2 folders); chrome text cap constant `ChromeTextMaxPt` = 20 applied to top bar and bottom strip, strip as 2 x 3 grid with wrapping | A49, A50 |
| `app-shell/PLAN.md` section 3 | Banner host: up to 2 banners, priority wrong-password > offline > Outbox failure > initial sync > others, close button only on the Outbox failure banner; transient notice host 4 s default (`TransientNoticeSeconds`) | A53, A54 |
| `app-shell/PLAN.md` | `IDialogService.ShowNotice(text)` = modal with one `OK` button; classes of messages per detailed-spec 20.3 | A55 |
| `app-shell/PLAN.md` and every window PLAN | Window rules and sizes from `window-layouts.md` (modal, Enter/Esc, button order, tab order, initial focus, F5, Delete key only with list focus, no Send shortcut) | A56 |
| `folder-actions/PLAN.md`, `mail-display/PLAN-1.md` | Header Delete button placed at the far right, red outline | A51 |
| `mail-list/PLAN.md`, `folder-actions/PLAN.md` | Multi-select delete as in detailed-spec section 22 (header label 「選んだN通を削除」, preview guide, 「すべて選ぶ」, 「選択をやめる」, 200 cap, Trash/Outbox without checkboxes, search results, partial failure, Space key) | A59-A65, A76 |
| `mail-list/PLAN.md` | Sent/Drafts rows 「宛先: first ほか N 人」; sort key and displayed time both INTERNALDATE (the PLAN says date) | A67, A68 |
| `mail-display/PLAN-1.md` | CC line in the preview header when present | A66 |
| `mail-display/PLAN-2.md` | Effective preview size = min(pt x zoom, 60pt) in both plain text and HTML | A70 |
| `compose/PLAN-1.md` | Over-limit amount shown in raw-file terms (encoded overage / 1.37, rounded up to 0.1 MB) | A69 |
| `compose/PLAN-2.md` | Clearing everything after an auto-save deletes that auto-saved draft on the next tick or close; draft-upload failure = silent retry + status line; explicit save failure = modal | A71, A74 |
| `compose/PLAN-1.md`, `compose/PLAN-2.md` | Layout, tab order, initial focus, close dialog default 下書きに保存 | A56, A75, Q75 |
| `address-book/PLAN.md`, `block-list/PLAN.md`, `filter-rules/PLAN.md`, `settings/PLAN.md`, `accounts-auth/PLAN-1.md`, `accounts-auth/PLAN-2.md` | Layouts and tab order from `window-layouts.md`; these windows are MODAL (the address-book PLAN says "modal-less window": change it); filter-rule edit dialog shows an error line instead of a disabled 保存 | A56 |
| All PLANs with strings | Strings come from `ui-strings.md` (fully approved, Q76) | Q72 |

## C. Decided changes to write into the PLANs (user decisions, 2026-10-08)

| File | Edit | Decision |
|---|---|---|
| `app-shell/PLAN.md` | First launch: window maximized, list pane about 35 percent of the width; afterwards restore last size, state and splitter (PLAN says "loaded from settings" only) | Q58 |
| `app-shell/PLAN.md` section 3 | Delete undo notice 10 seconds (the PLAN says 5 s); failures/refusals = modal dialog with one 「OK」 button, successes = transient notice | Q64 |
| `mail-list/PLAN.md` | Row = 2 lines (line 1 sender + date/time + paperclip, line 2 subject with ellipsis), unread = bold only; checkbox at the left of every row (hidden in Trash and Outbox, A62) | Q61, Q71 |
| `mail-list/PLAN.md` | Empty folder 「メールはありません」 is NOT shown during the first sync (only the loading banner); no selection -> preview guide 「左の一覧からメールを選んでください」; new mail keeps the scroll position | Q62 |
| `mail-list/PLAN.md`, `folder-actions/PLAN.md` | After delete / Move to Spam / Not spam / Restore select the NEXT OLDER mail; previous if none; guide text if empty (the PLAN says "next row") | Q62 |
| `mail-list/PLAN.md` section 2 | "Selecting a message marks it read (A11) after the body is displayed": change to "immediately on selection, only in Inbox and Spam" | Q69 |
| `folder-actions/PLAN.md` | Drafts header gets the large 「編集」 button (header-button matrix: Drafts = Edit, Delete); 迷惑メールではない / 受信トレイに戻す / 今すぐ送る / 下書きに戻す remain right-click only; Move to Spam: no notice (already) | Q63 |
| `folder-actions/PLAN.md` | Delete notice 10 seconds; multi-delete notice 「N通をごみ箱に移動しました [元に戻す]」; `UndoToken` becomes a list; one latest undo | Q64, Q71 |
| `mail-display/PLAN-2.md` section 2 | Remove "Ctrl+0 = reset, Ctrl+plus/minus also step"; keep Ctrl+mouse wheel only | Q69 |
| `accounts-auth/PLAN-1.md` | First account-add screen closed without adding -> confirm 「このまま閉じるとアプリは終わります。よろしいですか?」 [終わる] [戻る(既定)] (AC-19, approved) | Q66 |
| `accounts-auth/PLAN-2.md` | Deleting an account with Outbox mail is stopped with a modal OK message (AC-20); remove the delete-dialog extra line 「送信待ちのメールも消えます」 (AC-15 superseded) | Q68 |
| `sync-cache/PLAN-3.md` | Idle backfill is included (remove "optional"); numbers 60 s / 20 per cycle / 5 MB stay as assumption A57 | Q67 |
| `reply-forward/PLAN.md`, `address-book/PLAN.md`, `search/PLAN.md` | Approved as written; no change (search PLAN: add `Q70` to the header) | Q70 |
| `search/PLAN.md` | Search results have row checkboxes and multi-select delete (follows part B once approved) | Q71 |
| `mail-list/PLAN.md`, `folder-actions/PLAN.md`, `mail-display/PLAN-1.md` | AUTO-selection after delete / Move to Spam / Not spam / Restore is visual only and does NOT mark the mail read; mark-read (Inbox/Spam only) happens only when the user explicitly selects a row (click or key on the row) | Q74 (A58) |
| `compose/PLAN-2.md`, `app-shell/PLAN.md` (dialog service) | Compose close dialog: default button = 「下書きに保存」 (Enter); labels unchanged; this is the only exception to "Enter = cancel-type button" | Q75 (A75) |
| `folder-actions/PLAN.md`, `ui-strings` users | Spam right-click menu item reads 「迷惑メールではない」 (no suffix) | Q76 |
| `accounts-auth/PLAN-2.md` | Account rename does NOT go through the Q31 warning dialog; the warning wraps only add account, delete account and password re-entry | Q79 |
