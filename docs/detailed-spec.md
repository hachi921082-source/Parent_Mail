# Detailed Specification

<!-- Elaboration of spec.md to a level usable for implementation plans (docs/plans/<feature-name>/PLAN.md). Created/updated by designer-lite as features are planned. -->

Sources: `docs/spec.md` (S#), `docs/QA.md` resolved items (Q#) and the 21 approved assumed defaults (D#, as listed in QA.md "Assumed defaults"). Statements not traceable to those are marked **Assumption (An)** and collected in section 19. Updated 2026-10-08 with the user decisions from PLAN creation (QA.md Q44-Q57) and from the reviewer-lite self-review round (Q58-Q72); superseded assumptions stay numbered and are marked in section 19. All items formerly marked APPROVED were approved by the user on 2026-10-08 (QA.md Q73-Q78); nothing is pending. No rationale here (see DECISIONS.md).

## Index

1. Window and layout
2. Accounts and authentication
3. Gmail folder mapping
4. Sync and local cache
5. Offline, Outbox and send retry
6. Mail display
7. Compose window
8. Reply, reply all, forward
9. Folder context menus
10. Address book
11. Block list
12. Filter rules
13. Search
14. Safety warnings
15. Settings
16. Notifications, startup and shutdown
17. Message tone
18. Non-functional requirements
19. Assumptions
20. Notices, banners and dialogs
21. Keyboard, focus and window rules
22. Multi-select delete

## 1. Window and layout

Source: S4, Q13, Q33, D8-D9.

- Main window, minimum size 1024x700 (S8). Window size and splitter position are persisted in local settings (S8). First launch (nothing persisted yet): the window opens maximized with the list pane about 35 percent of the width; afterwards the last size, state and splitter position are restored (Q58).
- Top bar, full width, left to right: account buttons, Inbox, Outbox, Sent, Trash, Drafts, Spam buttons (S4). Then the HTML/plain-text checkbox (S4; it applies to the currently displayed mail only, see section 6, Q39) and a manual refresh button (S8). Position of checkbox and refresh button within the bar: Assumption A1 (right side of the bar). APPROVED (A50): because one row cannot hold account button, 6 folder buttons, checkbox and refresh at 1024 wide, the bar has two rows: row 1 = account buttons (left), checkbox and refresh (right); row 2 = the six folder buttons. See `docs/plans/window-layouts.md` section 2.
- Account buttons (Issue #1, Q91; replaces the single toggle button of S3/Q57): one button per account, side by side, showing the account name only. The selected button is filled, bold, thick-bordered and starts with a check mark; the non-selected one is white. The non-selected button shows a small unread badge with the unread count as a number when that (non-displayed) account has unread mail (Q13; number display Assumption A44); the selected button shows none. With one account only the selected button is shown. Details: `docs/plans/window-layouts.md` section 2.
- Unread count is shown only on the Inbox button, besides the account-button badge (S4).
- Mail list pane: narrow, left, below the top bar. Each row has 2 lines (Q61): line 1 = sender (display name, else address), date/time and attachment icon (paperclip); line 2 = subject, ellipsis when long; unread rows are bold only. A checkbox is always shown at the left of every row (Q71, section 22; hidden in Trash and Outbox: APPROVED A62). Sent/Drafts rows show 「宛先: <first recipient>」, with 「ほか N 人」 appended when there are more (APPROVED A67). Dates/times (local time): today `HH:mm`; this year `M月d日 H:mm`; older `yyyy年M月d日 H:mm` (Q44, replaces A45). The list is sorted by date descending; APPROVED A68: the sort key and the displayed time are both INTERNALDATE (the same value the 12-month body window uses). Action buttons at the bottom of the pane (APPROVED A50: a 2-column x 3-row grid, labels may wrap), in order, row by row: 新しいメール (compose), [BLANK cell: the former search button was removed, Q88; the cell stays empty so the grid and the other five buttons keep their places], settings, address book, 受信拒否リスト (block list), 振り分けルール (filter rules) (S4, Q57). The blank cell is the top row, right column; it is not focusable and not drawn as a button.
- Preview pane: very wide, right, most of the window (S4). Above the preview, large buttons always visible: Reply, Reply all, Forward, Delete (Q33). Context menu remains available (Q33). Next to these buttons: preview zoom controls 拡大, 縮小 and 「100%に戻す」 (Q38, Q57; section 6; control form Assumption A34). In Drafts there is an extra large button 「編集」; the other folder-specific actions (迷惑メールではない, 元のフォルダに戻す, 今すぐ送る, 下書きに戻す) are right-click only (Q63, Q87). APPROVED (A51): 削除 is placed apart from the other buttons at the far right of the header, outlined in red, so it is not pressed instead of 返信/転送. Moving to Spam gets no undo notice.
- Splitter between list and preview is draggable (S4).
- Text-size cap for chrome (Q59; 20pt approved, A49): the text of the top bar and of the bottom button strip stops growing at a maximum `ChromeTextMaxPt` = 20pt (the setting still applies below it; button height still grows to at least 56 DIP). Mail list, preview, dialogs and mail text follow the setting up to 48pt. The user suggested 28pt; at the 1024-wide minimum window 28pt (and 24pt) cannot fit the six folder buttons (measurements in window-layouts.md section 1.3).
- Empty states (Q62): no mail selected = the preview shows 「左の一覧からメールを選んでください」. An empty folder shows 「メールはありません」, EXCEPT during the first sync of the account, when the empty list shows nothing and only the loading banner 「メールを読み込んでいます…」 is displayed. After delete / Move to Spam / Not spam / Restore the next OLDER mail in the list is selected automatically; if there is none, the previous (newer) one; if the list is empty, the guide text. New mail arriving by sync keeps the list scroll position and the current selection (Assumption A52).
- Compose, reply, forward open in a separate window (S4).
- The folder buttons use Japanese labels; exact wording: Assumption A2 (Inbox=受信トレイ, Outbox=送信トレイ, Sent=送信済み, Trash=ごみ箱, Drafts=下書き, Spam=迷惑メール).
- Header buttons act on the selected mail. When no mail is selected they are disabled: Assumption A3. In Trash, Drafts and Outbox, Delete/Reply/Reply all/Forward availability follows the context-menu tables in section 9 (items not listed there are disabled): Assumption A3. In Spam, the header buttons Not spam / Delete are the context-menu items and Reply/Forward are disabled; an Outbox row selection shows a read-only preview and Outbox items have no read/unread state (Q56).

## 2. Accounts and authentication

Source: S1, S3, S8, Q31, D4.

- Gmail only, auth by Gmail app password via IMAP/SMTP (MailKit). Maximum 2 accounts. "Add account" is unavailable once 2 exist (S3; unavailability form is Assumption A4).
- Each account has a renamable display name (S3). Rename is done in settings (section 15): Assumption A5.
- Account-add screen: fields = email address, app password, display name (Assumption A6); states that a Gmail app password is required. No in-app setup guide (S3). Before saving, the app tests IMAP and SMTP login with the entered credentials; on failure nothing is saved. An address that is already registered is rejected (Q53).
- App password stored in Windows Credential Manager (S8). Never in SQLite or plain files.
- Add account, delete account and password re-entry are behind a warning dialog: text says to ask a knowledgeable person; default button = Cancel; no PIN (Q31). Exception: the account-add screen shown on first launch when no account exists yet is not preceded by this dialog (Assumption A31).
- Deleting an account removes its credential, its cache and its per-account data (address book, block list, filter rules): Assumption A7. If the account still has mail in its Outbox, the deletion is STOPPED (nothing is removed) with a modal notice asking the user to send the mail or return it to drafts first (Q68; wording ui-strings AC-20, approved; replaces the earlier idea of an extra line in the delete dialog).
- First launch with no account: if the account-add screen is closed without adding an account (やめる, Esc or X), the confirmation 「このまま閉じるとアプリは終わります。よろしいですか?」 with 「終わる」 / 「戻る」 (default 戻る) is shown; 「終わる」 quits the app (Q66; wording kept after review, ui-strings AC-19).
- Wrong or revoked app password (S3): show a plain-language notice asking a knowledgeable person to re-enter it; open the password re-entry screen; cached mail stays viewable. Re-entry screen is not preceded by the Q31 warning dialog because it is opened by the app: Assumption A8.
- Account switch while a sync runs does not cancel that sync (Q56).
- Account switch: pressing the non-selected account button changes the displayed account; list, folders, counts, search and per-account data all follow the displayed account (S3, Q28, Q35).

## 3. Gmail folder mapping

Source: D2, Q11, S4, S8.

- Inbox = INBOX; Sent = [Gmail]/Sent Mail; Drafts = [Gmail]/Drafts; Trash = [Gmail]/Trash; Spam = [Gmail]/Spam, resolved by IMAP special-use flags (D2).
- Outbox is local only; Gmail has none (D3).
- Mail present only in All Mail (archived) or custom labels is shown in no folder but is found by search (Q11). To support this, [Gmail]/All Mail is synced too, under the same rules as the other folders (spec.md section 8, Q36). One message appearing in several Gmail folders is stored once, keyed by Gmail message id X-GM-MSGID, with its folder memberships.
- Folder view rule (Q42): a folder view shows the messages whose folder membership includes that folder. A message that also belongs to Trash or Spam is excluded from the Inbox/Sent/Drafts views and appears only in Trash/Spam. All-Mail-only messages appear in no view (Q11) but are searchable. The cache keeps a message table separate from a message-folder membership mapping (design detail left to PLAN.md).
- Sent mail is not appended manually; Gmail stores it in Sent after SMTP submission (D3).
- Delete = move to Gmail Trash only; no permanent delete, no "Empty trash" (S8).

## 4. Sync and local cache

Source: S8, Q12, Q14, D4.

- Storage: SQLite per account (separate cache and sync per account, D4), holding mail, plus settings (account names, filter rules, address book, block list, window/splitter state) (S8). Whether settings live in the same or a separate SQLite file: Assumption A9 (separate settings DB; per-account cache DB).
- First sync: headers for all mail; bodies for the most recent 12 months (S8).
- Older bodies and all attachments: downloaded on demand when opened, then cached (S8).
- Periodic sync: only new/changed mail via IMAP UID/UIDVALIDITY (S8). If UIDVALIDITY changes for a folder, only that folder's UID mappings are dropped and re-resolved via X-GM-MSGID; bodies and attachments are kept; re-listed mail is NOT new mail (no block list/filter processing, no unread-badge inflation) (Q51; supersedes A10). IMAP uses polling only (no IDLE); CONDSTORE is used when Gmail advertises it, otherwise UID comparison. IMAP/SMTP timeout 30 s (Q53).
- Initial sync: the banner 「メールを読み込んでいます…」 is shown; the list fills progressively; the UI is not blocked (Q52). While the list is still empty the 「メールはありません」 text is not shown (Q62). Periodic sync inserting new mail keeps the list scroll position and selection (A52). The 12-month body window is judged by INTERNALDATE.
- Background backfill of older bodies when idle is INCLUDED (Q67). Parameters carried from the PLAN, listed as assumptions (A57): idle = no user input for 60 s, 20 bodies per cycle, newest first, only while online and while no sync runs, stops on any user action or sync start; inline (cid) parts up to 5 MB are fetched with an on-demand body.
- Fetch timing: at app startup, then every 5 minutes, plus manual refresh button (S8). At most one sync runs per account at a time: a manual refresh pressed during a running sync is merged into it (the refresh button is shown as busy) and timer ticks during a running sync are skipped (Assumption A39). Both accounts are synced on the same startup/5-minute timer (spec.md section 8, Q36), which feeds the other-account badge.
- Mirror rules: moves to Gmail Trash are reflected locally; mail permanently gone from Gmail is deleted from the local copy (S8).
- Read state: unread shown bold (S5). Opening a mail marks it read immediately (on selection, not after the body is displayed) and the state is synced to Gmail (\Seen), ONLY in Inbox and Spam; opening mail in Sent, Drafts, Trash and Outbox changes no read state (Q69; replaces A11). A mail selected automatically after a delete (section 1) is NOT marked read; only a row the user selects explicitly (click or key on the row) is marked read (A58, Q74). Context menu offers Mark read/unread (D8). While offline, Mark read/unread is queued and applied after reconnect; moves (delete, Move to Spam, Not spam, Restore, Undo) are refused with the notice 「今はインターネットにつながっていないため操作できません。つながってからもう一度お試しください。」 (Q55).
- Drafts: synced with Gmail Drafts (Q12). Auto-saved drafts (section 7) are written to Gmail Drafts when online; when offline they are kept locally and uploaded after reconnect: Assumption A12.
- New-mail handling order for a new message: download header and body (bodies of new mail are always fetched, Q17), apply block list and filter rules (sections 11, 12), then update counts/badge.
- "New mail" = mail first seen in the Inbox after the initial sync of that account completes (mail first seen in Sent, Drafts, Trash, Spam or All Mail only is not new mail). Block list and filter rules are NOT applied during the initial sync or when an account is first added (Assumption A43).
- Offline, unfetched older body or attachment: show plain-language "not downloaded yet" message; fetch and show automatically once online; other cached mail stays readable (S8).

## 5. Offline, Outbox and send retry

Source: S8, Q21, Q22, D3.

- Offline / Gmail unreachable: show cached mail with a plain-language banner (S8).
- Send: pressing Send sends immediately, with no confirmation dialog and no undo window (Q21). The message is placed in Outbox and submitted via SMTP right away; on success it is removed from Outbox and appears in Sent via Gmail (D3). If offline, it stays in Outbox and is sent automatically after reconnect (S8).
- Failure: the message stays in Outbox and is retried at each sync cycle (5 min). After 3 failures a plain-language failure notice is shown once (Q22); retries then continue every cycle with no upper limit, the notice is not repeated and the failure marker stays (Q52). Outbox "Send now" failure notice: 「まだ送れませんでした。しばらくしてからもう一度お試しください。」 (Q57). Outbox context menu: Send now, Return to drafts (Q22, D9). The failure notice is shown as a banner and a marker on the Outbox row; exact form: Assumption A13. The failure is also visible on the Outbox top-bar button (marker or count of failed messages): Assumption A40.
- Retry counter is persisted per message across app restarts: Assumption A14.
- Empty-subject warning remains on Send; no other confirmation (D13). Behaviour of that warning (Send anyway / Cancel): Assumption A15.
- After Send, the compose window closes (D13).
- When a message is sent successfully, the corresponding Gmail Drafts draft (auto-saved, or edited from Drafts) is deleted; the same applies to a draft returned from Outbox and later sent. A failed send keeps the draft/Outbox item (Assumption A46).
- Quitting the app with unsent mail in Outbox shows a warning dialog (section 16, Q43).

## 6. Mail display

Source: S8, S5, S14 (section 14 below).

- Default mode: plain text. The top-bar checkbox switches to HTML (S8). HTML is rendered in WebView2 (S8).
- The checkbox applies to the currently displayed mail only (temporary). Opening another mail resets the checkbox to the default plain-text display; HTML-only mails keep the derived-text fallback below (Q39).
- If a mail has no text part (HTML only), show plain text derived from the HTML instead of an empty pane (S8).
- Remote images in HTML are not loaded by default; a "Show images" action loads them on demand. Embedded cid/inline images display normally (S8). "Show images" applies to the one open mail only: Assumption A16.
- Mail preview includes images (S5).
- Attachments: list shown with the mail; save and open supported (S5). Executable rules in section 14. Saving uses a standard Save dialog; opening uses the Windows default app: Assumption A17.
- Preview header shows 差出人, 宛先, 件名, 日時, and APPROVED (A66) a CC line (「CC」) only when the mail has CC recipients; plus the not-in-address-book notice when applicable (section 14). With no mail selected the preview shows the guide text (section 1); with several rows checked it shows the multi-select guide (section 22).
- Text size applies to the preview (section 15), as follows (Q42):
  - The app text-size setting (18pt default) applies to WPF controls (list, buttons, headers, dialogs) and to plain-text display.
  - In HTML display mode no app CSS font-size is injected into WebView2. Instead the WebView2 ZoomFactor is used, ZoomFactor = text-size setting (pt) / 12 x temporary preview zoom, clamped to 0.25-5.0 and rounded to 2 decimals (Q45, replaces the /18 wording; 1.5 at the default 18pt, so HTML text matches plain text). WebView2 built-in zoom shortcuts are disabled.
  - Plain-text display uses the real font size multiplied by the temporary preview zoom.
  - Font size is in true points: WPF FontSize = pt x 96/72 (Q50). The ZoomFactor formula is settled by Q45 (A41 superseded).
  - APPROVED (A70): both modes are clamped consistently: the effective preview text size = min(text-size pt x zoom, 60pt) (60pt = ZoomFactor 5.0 x 12). Plain text is clamped to 60pt too, so HTML and plain text stay the same size at every setting (otherwise 48pt x 300% would be 144pt in plain text but 60pt in HTML).
- Mail enlarged display (preview zoom) (Q38, S5):
  - Zoom is a temporary multiplier on top of the text-size setting and affects the preview pane only; it does not change the stored text-size setting; the compose window is not affected (Assumption A33).
  - Controls: large 拡大, 縮小 and 「100%に戻す」 buttons beside the preview header buttons, plus Ctrl+mouse wheel over the preview (Assumption A34). No other zoom shortcuts: Ctrl+0, Ctrl+plus and Ctrl+minus are NOT provided (Q69; removes the shortcuts the PLAN had added).
  - Range 100%-300%, step 25%, default 100% (Assumption A35).
  - Zoom resets to 100% whenever the displayed mail, folder or account changes (Q46; supersedes A36).
  - Zoom is never persisted; every launch starts at 100% (Q38; A37 confirmed by the user, Q46).
  - Reset button label: 「100%に戻す」 (Q57).
- Plain text mode shows URLs as text; links are clickable and go through the suspicious-link rules (section 14): Assumption A18. HTML navigation is always intercepted and opened externally in the default browser; `mailto:` links open the compose window (Q49).
- The not-in-address-book notice reads 「アドレス帳に登録されていません」 (Q57).

## 7. Compose window

Source: S4, S5, Q19, Q20, Q21, D14-D16.

- Separate window, opened by the compose button in the main window (S4).
- Single compose window (Q89, user decision 2026-10-14, Issue #5): at most ONE compose window exists at a time. If one is open and any of 新しいメール, 返信, 全員に返信, 転送, Drafts > 編集 (button, menu or double-click on the row body, Q90) or a `mailto:` link is used, the app only brings the existing compose window to the front and activates it (restoring it first if minimized, A77). No notice, no new window, and the new request's content (recipient, quote, draft) is NOT applied to the open window and is not queued (A79). After the window closes (Send, close dialog, quit) the next request opens a new window normally. The address-book picker (modal child of the compose window) is unaffected (A80).
- Fields: To, CC, BCC, subject, body, attachments (S5). The body is plain text only (text/plain, UTF-8); no HTML composing. The From header carries the account's address only, never the display name (Q47). Window title: 「新しいメール - <account display name>」 (Q57). BCC is hidden until requested (D14).
- Recipient autocomplete: candidates from the displayed account's address book and that account's previously used recipients (Q19, D17). "Previously used" = addresses in To/CC/BCC of mails in that account's Sent: Assumption A19.
- Address-book picker (Q80-Q85, user decision 2026-10-11). A button 「アドレス帳から選ぶ」 sits beside each of 宛先, CC and BCC (the BCC button appears with the BCC row). It opens a dialog MODAL TO THAT COMPOSE WINDOW ONLY (other windows stay usable), titled 「アドレス帳から選ぶ - <account display name>」, listing the address book of the compose window's own account (A21), not the account displayed in the main window; previously used recipients are not listed. Sorted by ふりがな as in the address book; ONE ROW PER EMAIL ADDRESS (name, address, large checkbox, row height at least 56 DIP); multi-select; NO search box. Buttons [ 入れる ] [ やめる ]; Enter = 入れる, Esc = やめる, initial focus = the list. 入れる with nothing checked: error line, dialog stays open. Empty address book: the picker is not opened; a notice with one 「OK」 button is shown instead. Picked addresses are appended to the end of that field as `Name <address>` tokens (existing text untouched) and focus returns to that field. Addresses already in THAT field (case-insensitive) are shown checked and disabled with the tag 「入力ずみ」. The same address in 宛先 and CC is not blocked. Tab order: each picker button directly follows its field. No new shortcut key. Wording: ui-strings CP-18 to CP-22, WD-31 (PENDING approval of the wording). Implemented by compose PLAN-3.
- Attachments: drag and drop onto the compose window, plus an "Attach" button (D16). Size limit: Gmail's 25 MB applies to the whole encoded message and base64 inflates by about 1.37x, so the check uses the estimated encoded message size (limit 25 MB, roughly 18 MB of raw files). When exceeded, warn in plain language 「ファイルが大きすぎます」 and refuse the attachment (D16, Q42). Threshold: estimated encoded size = sum over files of ceil(n/3) x 4 x 78/76, plus body bytes, plus 10,240 bytes header overhead, must be <= 25,000,000 bytes; the message shows how far over (Q50; settles A42). APPROVED (A69): the over amount is shown in raw-file terms, i.e. the encoded overage divided by 1.37, rounded up to 0.1 MB (e.g. 「あと約2.3MB減らしてください」 means 2.3 MB of files), because the parent only sees file sizes, not encoded sizes. Each attachment can be removed (D16). Executable files (section 14 list, archives one level deep) are warned about and blocked from being attached (Q41).
- Draft auto-save every 30 seconds while editing (D15, S5); drafts are stored in Gmail Drafts (Q12, section 4). APPROVED (A71): if all fields and attachments are cleared after an auto-save has happened, the next auto-save tick (or close) deletes that auto-saved draft instead of keeping an empty draft (consistent with A20). APPROVED (A74): if uploading an auto-saved draft fails, the app retries silently each cycle and shows a small status line in the compose window; a modal OK dialog appears only when the user explicitly chose 下書きに保存 and it failed.
- Closing the window with unsent text: always show a dialog with three large buttons: save draft / discard / cancel (Q20). Auto-save stays active.
  - Save draft: saves the current text to Gmail Drafts and closes.
  - Discard: discards only the edits made after the last auto-save. A draft already auto-saved to Gmail Drafts is KEPT in Drafts (Q37). If no auto-save has happened yet (first 30 s), Discard leaves nothing behind.
  - Cancel: returns to the compose window.
  - Middle button label: 「保存しないで閉じる(前に自動保存された下書きは残ります)」 (section 17); the label does not promise deletion because the earlier auto-saved draft remains.
- Closing with no content shows no dialog: Assumption A20.
- Send: see section 5.
- Sending account: the compose window sends from the account that was displayed when it was opened: Assumption A21.
- Editing a draft (Drafts > Edit) opens the compose window with its content (D9).

## 8. Reply, reply all, forward

Source: S5, D14.

- Open in a separate window, same as compose (S5). Subject to the single compose window rule (section 7, Q89): if a compose window is already open, reply, reply all and forward only activate it; nothing is built or fetched (A81).
- Reply quotes the original (D14). APPROVED (Q70), as in the reply-forward PLAN:
  - Reply: To = Reply-To header if present, else From. Subject = `Re: ` + original subject unless it already starts with Re:/RE:/返信:. Quote: first line `yyyy年M月d日 H:mm、<sender name> さんは書きました:`, then each original line prefixed `> `; blank line above the quote and the cursor above it. Source text = body text, else the text derived from HTML.
  - Reply all: To = Reply-To/From; CC = original To + CC minus the account's own address and duplicates (case-insensitive).
  - Forward: subject `Fwd: ` (not repeated if Fwd:/Fw:/転送: present); body = blank lines, `---------- 転送メッセージ ----------`, 差出人/日時/件名/宛先 lines, blank line, then the original text unquoted; To empty; the original attachments are carried over (below).
- Subject prefix and quote format: Assumption A22 ("Re: " / "Fwd: " prefixes).
- Reply and reply all pre-fill To/CC from the original: Assumption A22.
- Forward fetches the original's attachments (downloading them if needed) and applies the same executable and size checks as compose; violating attachments are dropped and a notice says so. If an attachment is not downloaded and the app is offline, a notice is shown and the forward continues without it (Q54).

## 9. Folder context menus

Source: S5, D8-D9, Q22.

| Folder | Items |
|---|---|
| Inbox | Reply, Reply all, Forward, Mark read/unread, Move to Spam, Delete, Add sender to address book, Block sender |
| Sent | Forward, Delete |
| Drafts | Edit, Delete |
| Spam | Not spam (move to Inbox), Delete |
| Trash | 「元のフォルダに戻す」 (Restore (Q87), to the folder the mail was deleted from; Inbox if unknown, Q86) only; no permanent delete |
| Outbox | Send now, Return to drafts |

- Delete = IMAP move to Gmail Trash (S8). Applies to both the Delete menu item and the Delete header button.
- Delete shows no confirmation dialog. Instead a non-modal notice 「ごみ箱に移動しました [元に戻す]」 is shown for 10 seconds; "元に戻す" (Undo) moves the mail back to the folder in which it was viewed when deleted; other Gmail labels are not restored (Q40, Q55). Undo is refused while offline. The notice stays for 10 seconds (Q64; replaces A38, 5 s). Moving a mail to Spam gets NO undo notice (Q63). Only the latest delete can be undone (a new delete replaces the notice). The Delete key deletes only while the mail list has keyboard focus (section 21). Deleting several checked mails: section 22.
- Restore from Trash (Q86): the app persists, per mail, the folder it was deleted from (an origin-folder record in the local cache, so it survives restarts; recorded when the app itself deletes, including multi-delete). Restore moves the mail back to that folder (Sent -> Sent). If no origin is recorded (trashed on another device or Gmail web, trashed before this feature existed, or an All-Mail-only mail) it goes to the Inbox. Other Gmail labels are not restored. The record is cleared once the mail leaves Trash. The menu label is 「元のフォルダに戻す」 (Q87).
- Moves in this table are refused while offline (section 4, Q55). The block menu item reads 「受信拒否に追加」 (Q57).
- "Add sender to address book" adds to the displayed account's address book (D19). "Block sender" adds to the displayed account's block list (Q35); its entry-unit choice follows section 11.
- Preview-header buttons (section 1) are enabled only where the same action exists in this table: Assumption A3. Exception (Q63): in Drafts the header also has the large 「編集」 button (= the Edit menu item).
- Right-click menu Delete on a row: if that row is checked it deletes all checked rows, otherwise only the clicked row (APPROVED A76).

## 10. Address book

Source: S5, Q27, Q28, D17-D19.

- One address book per account (Q28).
- Fields per person: name, ふりがな (reading, used for sorting), one or more email addresses, memo (Q27).
- Opened by the address book button at the bottom of the list pane (S4). UI form (list plus add/edit/delete dialog, sorted by reading): Assumption A23.
- Columns and edit fields: 名前 / ふりがな / メールアドレス / メモ (Q57; 「よみがな」 is not used).
- Used by: recipient autocomplete (D17), compose address-book picker (section 7, Q80-Q85), not-in-address-book notice (D18), "Add sender to address book" (D19).
- Stored in local settings (S8).

## 11. Block list

Source: S5, Q15, Q16, Q35.

- One block list per account (Q35).
- Entry = email address or whole domain, chosen at registration (Q16). A domain entry also blocks its subdomains; matching is case-insensitive (Q48). Window title and button: 「受信拒否リスト」 (Q57).
- Mail from a blocked sender is moved to Spam (Q15).
- Opened by the block list button (S4); entries can be viewed and removed: Assumption A24.
- Applies to new incoming mail only, evaluated before filter rules: Assumption A25 (existing mail is not moved when an entry is added).

## 12. Filter rules

Source: S5, Q17, Q18, Q35, D10-D12.

- One rule set per account (Q35).
- Conditions: sender contains, subject contains, body contains (Q17). "Sender contains" matches the address and the display name, case-insensitive (Q48). Window title and button: 「振り分けルール」 (Q57). Multiple conditions in one rule combine as AND: Assumption A26.
- Actions: move to Trash, move to Spam, mark as read (Q18). No custom folders.
- Applies to new incoming mail only; evaluated top to bottom; first match stops (D10).
- Edited in a simple list dialog with enable/disable per rule (D11); opened by the filter rules button (S4).
- Body condition: evaluated after the new mail's body is fetched; HTML-only mails matched on the derived plain text; mail with undownloaded body never matches body conditions; rules never trigger old-body fetches (D12).
- Rule order is changeable (move up/down): Assumption A27.

## 13. Search

Source: S5, Q11, Q29, Q30, D20.

- An inline search box is always visible at the top of the list pane; results replace the list; a "Clear" button restores it (D20). There is no search button at the bottom of the list pane (removed, Q88; Assumption A47 superseded). The box has its own 「検索」 action (Enter or button, ui-strings SR-01).
- Scope: all folders of the displayed account, including mail present only in All Mail (Q29, Q11). Results show the folder name (Q29).
- Matching: local cache only; fields = sender, subject, downloaded bodies. Unfetched older bodies are excluded and the UI says so (Q30). Match = SQLite `LIKE` substring scan normalizing case and full/half width only; no hiragana/katakana folding (Q47; supersedes A28). Several words separated by spaces are ANDed (each word must match some field); results are capped at 500 rows with the note 「結果が多いため一部のみ表示しています」 (Q70, approved as in the search PLAN). Search results also have row checkboxes and support multi-select delete (section 22).
- Works offline (Q30).

## 14. Safety warnings

Source: S5, Q23-Q26.

Suspicious link (Q23), a link is suspicious if any holds:
- the shown text differs from the real target;
- the URL host is an IP address;
- the URL is a known shortener: a code constant of about 20 hosts, matched on exact host or any subdomain (Q49; settles A29);
- the scheme is not https;
- the domain is look-alike: a host label starting with `xn--`, any non-ASCII host, or a label mixing scripts (Q49).

Banner and links (Q49): the red banner judges both the HTML part and the text part. A link that is not suspicious opens in the default browser with no dialog. HTML navigation is always intercepted and opened externally; `mailto:` opens compose.

Warning UI (Q24):
- a red banner at the top of the mail when it contains a suspicious link;
- clicking such a link opens a confirmation dialog showing the real destination; default button = Cancel.

Executable attachments (Q25): extensions exe, msi, bat, cmd, scr, js, vbs, ps1, jar, lnk, and macro-enabled Office files (docm, xlsm, pptm and the other types of A30); extended by Q51 with: hta, com, pif, reg, wsf, vbe, jse, msc, dll, cpl, iso, img, vhd, vhdx, appx, msix, wsh, wsc, ps1xml, psm1, scf, inf, gadget, application, msp, mst, sct, chm, hlp, ade, adp, mde. Matching: last extension only, case-insensitive, trailing spaces and dots stripped; a name containing a right-to-left override character (U+202E and similar) is flagged. Only zip archives are inspected, one level deep; a zip that cannot be read or is encrypted is treated as suspicious. The unreadable-zip message is listed in ui-strings.md. Strong warning; opening is forbidden; saving is allowed only after confirmation.

Attaching an executable file when composing (Q41): the same extension list as the receive-side rule applies, with archives checked one level deep. Attaching is warned about and BLOCKED (the file is not added). This applies to both drag and drop and the "Attach" button (section 7).

Sender not in the address book (Q26, D18): a notice in the preview header only (not in list rows). Wording: 「アドレス帳に登録されていません」 (Q57).

## 15. Settings

Source: S5, S8, Q31, Q32, D21.

- Opened by the settings button (S4). Contents: text size, accounts (rename, add, delete, password re-entry) (A5; Q31 protects add, delete and password re-entry, NOT rename: Q79). Anything else is not part of the first release (including signature, print, star, themes, S6).
- Text size: default 18pt equivalent, in true points: WPF FontSize = pt x 96/72 (S8, Q50). Changed by free numeric input or slider (Q32). Range 12-48pt, live sample preview in the settings screen, applies to list, preview, compose window and dialogs (D21).
- Account add, delete and password re-entry are behind the warning dialog (section 2, Q31).
- Preview zoom (section 6) is not a setting and is not stored; the text-size setting is unaffected by it (Q38, A33).

## 16. Notifications, startup and shutdown

Source: Q13, Q14, Q34, D5-D6.

- New-mail notification: in-app only (the Inbox unread count changes); no toast, no sound (Q14).
- Other-account indicator: unread badge on the non-selected account button (Q13, Q91).
- Startup: opens the last-used account's Inbox (D5); a sync starts (S8). First launch without an account opens the account-add screen: Assumption A31.
- Single instance: a second launch brings the existing window to the front instead of opening another one (Q47).
- No auto-start with Windows and no setting for it (D6).
- Closing the main window (X) quits the app (Q34). No tray, no background process. Because of this, fetching and the Outbox retry run only while the app is open.
- Quit with unsent mail in Outbox (X, and Windows shutdown/close where feasible) shows a warning dialog 「送信待ちのメールがあります。このまま閉じると送信されません」 with three large buttons (Q43):
  - 「送信してから閉じる」: try sending now, then quit; if sending fails, the app stays open and the failure notice is shown.
  - 「このまま閉じる」: quit; unsent mail stays in Outbox.
  - 「戻る」 (default): return to the app.
  - Windows shutdown/logoff is intercepted via `SessionEnding` and the same 3-button dialog is shown; 「このまま閉じる」 lets Windows continue (Q52; settles A48).
- Open compose window at quit: handled by the section 7 close dialog: Assumption A32. There is at most one compose window (Q89), so the quit sequence runs that one window's close dialog if it exists; 「戻る」 aborts the quit. The implementation may stay generic over a list of windows.

## 17. Message tone

Source: D1, S3.

- UI language: Japanese only (D1).
- Notices and errors: polite plain Japanese, no technical terms, one sentence on what to do next. Problems that the parent cannot fix say to ask a knowledgeable person (D1).
- Banners required by the spec: offline banner and wrong-password notice (S3, S8, D7). Which message is a banner, a transient notice or a modal OK dialog: section 20.
- Close-dialog buttons: 「下書きに保存」, 「保存しないで閉じる(前に自動保存された下書きは残ります)」, 「戻る」.

## 18. Non-functional requirements

Source: S1, S8.

- Windows desktop (development environment Windows 11), C# with WPF, MailKit, WebView2, SQLite, Windows Credential Manager (S1, S8).
- Minimum window size 1024x700 (S8).
- Privacy: mail content stays on the PC; no transmission to third parties; no LLM features (S8).
- No app-level encryption of local data (S8).
- Local log file (Q53; reverses the earlier "no error log"): `%LOCALAPPDATA%\Parent_Mail\logs\parentmail.log`, rotated at 1 MB keeping 3 files. Allowed: timestamps, component names, exception type/message/stack, counts, durations, IMAP/SMTP error categories. Forbidden: mail content, email addresses, subjects, attachment names, passwords, URLs. Unhandled exceptions are logged and show a plain-language notice.
- Stack (Q53): net10.0-windows; solution = App (WPF) + Core (incl. Data) + Tests (xUnit); CommunityToolkit.Mvvm and Microsoft.Extensions.DependencyInjection; Microsoft.Data.Sqlite (WAL, `PRAGMA user_version` migrations); HtmlAgilityPack; IMAP/SMTP timeout 30 s.
- Distribution (Q53): self-contained single-file exe named `Parent_Mail_YYYYMMDD-HHMMSS.exe`; no installer.
- All paths and file names (attachments, save dialogs, cache paths) must work with Japanese and other non-ASCII characters (global CLAUDE.md).

## 19. Assumptions

Added by designer-lite because the source documents do not state them; each needs user veto or later confirmation.

- A1 Checkbox and refresh button sit on the right side of the top bar.
- A2 Japanese labels: 受信トレイ, 送信トレイ, 送信済み, ごみ箱, 下書き, 迷惑メール; not-in-address-book notice wording in Japanese.
- A3 Preview-header buttons are disabled with no selection and when the action is absent from the section 9 table for that folder. EXTENDED by Q63: Drafts also has the large 「編集」 button.
- A4 "Add account" is disabled or hidden once 2 accounts exist.
- A5 Account rename lives in the settings screen.
- A6 Account-add fields: email address, app password, display name.
- A7 Deleting an account removes its credential, cache, address book, block list and filter rules. EXTENDED by Q68: blocked while the account has Outbox mail.
- A8 Password re-entry screen opened by the app is not preceded by the Q31 warning dialog.
- A9 Per-account cache DB plus a separate settings DB.
- A10 SUPERSEDED by Q51: UIDVALIDITY change drops only UID mappings; bodies/attachments kept; not new mail.
- A11 SUPERSEDED by Q69: opening a mail marks it read immediately and syncs \Seen, only in Inbox and Spam.
- A12 Offline auto-saved drafts are kept locally and uploaded to Gmail Drafts after reconnect.
- A13 Failure notice = banner plus marker on the Outbox row.
- A14 Retry counter persists across restarts.
- A15 Empty-subject warning offers Send anyway / Cancel.
- A16 "Show images" applies to the open mail only.
- A17 Attachment save via standard Save dialog; open via Windows default app.
- A18 In plain-text mode URLs are clickable and subject to the suspicious-link rules.
- A19 "Previously used recipients" = To/CC/BCC addresses of the account's Sent mail.
- A20 No close dialog when the compose window has no content.
- A21 A compose window sends from the account displayed when it was opened.
- A22 Reply/forward details: Re:/Fwd: prefixes, To/CC pre-fill, forward quotes text and carries original attachments.
- A23 Address book UI: sorted list by reading plus add/edit/delete dialog.
- A24 Block list entries can be viewed and removed.
- A25 Block list applies to new incoming mail only and is evaluated before filter rules.
- A26 Multiple conditions in one filter rule combine as AND.
- A27 Filter rule order can be changed (up/down).
- A28 SUPERSEDED by Q47: `LIKE` substring with case and width normalization.
- A29 SETTLED by Q49: shortener list = code constant; look-alike = xn--, non-ASCII host, mixed script.
- A30 Macro-enabled Office extensions: docm, xlsm, pptm, dotm, xltm, potm, xlam, ppam, ppsm.
- A31 First launch without any account opens the account-add screen, which is not preceded by the Q31 warning dialog.
- A32 Quitting with an open compose window goes through the section 7 close dialog (at most one such window since Q89; the original wording "windows" is kept generic in code).
- A33 Preview zoom is a temporary multiplier on the text-size setting, applies to the preview pane only, does not change the stored text-size setting, and does not affect the compose window.
- A34 Zoom controls: large 拡大, 縮小, 「100%に戻す」 buttons beside the preview header buttons, plus Ctrl+mouse wheel. NARROWED by Q69: no Ctrl+0 / Ctrl+plus / Ctrl+minus.
- A35 Zoom range 100%-300%, step 25%, default 100%.
- A36 SUPERSEDED by Q46: zoom resets to 100% on mail/folder/account change.
- A37 CONFIRMED by the user (Q46): zoom is never persisted; every launch starts at 100%.
- A38 SUPERSEDED by Q64: the Delete undo notice stays for 10 seconds.
- A39 Sync mutual exclusion: at most one sync per account at a time; a manual refresh during a running sync is merged into it (button shown busy); timer ticks during a running sync are skipped.
- A40 A send failure is also shown on the Outbox top-bar button (marker or count of failed messages), extending A13.
- A41 SUPERSEDED by Q45: ZoomFactor = pt / 12 x zoom, clamp 0.25-5.0, 2 decimals.
- A42 SETTLED by Q50: formula in section 7.
- A43 Block list and filter rules are not applied during the initial sync or when an account is first added; "new mail" = mail first seen in the Inbox after the initial sync completes (revised 2026-10-08 after PLAN self-review: rules must not touch the user's own sent mail or mail moved to Trash/Spam on the Gmail web).
- A44 The other-account badge on the non-selected account button shows the unread count as a number (Q91).
- A45 SUPERSEDED by Q44: today HH:mm; this year M月d日 H:mm; older yyyy年M月d日 H:mm.
- A46 On successful send, the matching Gmail Drafts draft is deleted (also for drafts returned from Outbox and later sent); a failed send keeps the draft/Outbox item.
- A47 SUPERSEDED (Q88): formerly "the bottom search button moves input focus to the always-visible inline search box"; the bottom button no longer exists.
- A48 SETTLED by Q52: SessionEnding is intercepted with the 3-button dialog.
- A2 (extension): wording for compose, block list, filter rules, zoom reset, Trash restore, address-book notice and ふりがな is fixed in ui-strings.md section 17 (Q57).
- A49 CONFIRMED (Q59): chrome text cap `ChromeTextMaxPt` = 20pt for the top bar and the bottom button strip (the user suggested 28pt; it does not fit at 1024 wide, see window-layouts.md 1.3).
- A50 CONFIRMED: top bar in two rows (row 1 account button + checkbox + refresh; row 2 six folder buttons); bottom strip a 2 x 3 grid with wrapping labels.
- A51 CONFIRMED (Q63): 削除 header button placed apart at the far right, red-outlined.
- A52 CONFIRMED (listed at the user's request, Q62): new mail arriving by sync keeps the list scroll position and the current selection.
- A53 CONFIRMED (Q64): banner region above the top bar; priority wrong-password > offline > Outbox failure > initial sync > others; maximum 2 shown (lower ones wait); 「×」 only on the Outbox failure banner (it stays hidden afterwards; the marker and button count stay).
- A54 CONFIRMED: transient success notices stay 4 seconds (delete undo: 10 seconds).
- A55 CONFIRMED: the message-to-class table in section 20 (modal OK / banner / transient / in-place).
- A56 CONFIRMED (Q65): window layouts and common window rules in `docs/plans/window-layouts.md` (modal dialogs, Enter/Esc, button order, sizes, tab and field order, initial focus).
- A57 CONFIRMED (Q67): idle backfill parameters carried from the PLAN: idle 60 s, 20 bodies per cycle, newest first, inline parts <= 5 MB.
- A58 CHANGED and CONFIRMED (Q74): a mail selected AUTOMATICALLY after a delete / move is NOT marked read; auto-selection is visual only. Read marking happens only when the user explicitly selects a row (click) or presses a key on it.
- A59 CONFIRMED (Q71): in the mail list, clicking a row selects it for the preview and clicking its checkbox toggles the check; the two are independent. Header Delete: if at least one row is checked it deletes the checked rows (label 「選んだN通を削除」), otherwise the selected row.
- A60 CONFIRMED (Q71): while at least one row is checked the preview shows the multi-select guide and only the delete button is enabled.
- A61 CONFIRMED (Q71): 「すべて選ぶ」 header checkbox and 「選択をやめる」 clear button; at most 200 mails per delete action.
- A62 CONFIRMED (Q71): multi-select in Inbox, Sent, Spam, Drafts and search results; checkbox column and 「すべて選ぶ」 hidden in Trash and Outbox.
- A63 CONFIRMED (Q71): in search results, checked mails already in Trash are skipped; Undo restores each mail to the folder shown on its result row, an All-Mail-only mail returns to All Mail only.
- A64 CONFIRMED (Q71): if some moves of a multi-delete fail, the moved ones stay moved, the others are restored locally, a modal OK dialog reports it, and Undo covers the moved ones.
- A65 CONFIRMED (Q71): Space toggles the check of the focused row; checks are cleared on folder, account or search change and kept across a sync reload (mails that disappeared are dropped).
- A66 CONFIRMED (Q72): the preview header shows a CC line only when the mail has CC recipients.
- A67 CONFIRMED (Q72): Sent/Drafts rows show 「宛先: <first recipient> ほか N 人」 when there are more recipients.
- A68 CONFIRMED (Q72): list sort key and displayed time are both INTERNALDATE.
- A69 CONFIRMED (Q72): attachment over-limit amount shown in raw-file terms (encoded overage / 1.37).
- A70 CONFIRMED (Q72): preview text size clamped consistently in plain text and HTML: effective size = min(pt x zoom, 60pt).
- A71 CONFIRMED (Q72): clearing everything in a compose window after an auto-save deletes that auto-saved draft (reviewer item 41).
- A72 CONFIRMED (Q68): wording of the account-delete-blocked message (ui-strings AC-20).
- A73 CONFIRMED (Q66): wording of the first-launch close confirmation (ui-strings AC-19; alternative given).
- A74 CONFIRMED: draft upload failure during auto-save is silent (retry each cycle plus a status line in the compose window); modal only when the user explicitly saved the draft.
- A75 CHANGED and CONFIRMED (Q75): the compose close dialog default button is 「下書きに保存」 (not 「戻る」); the three labels stay as in Q20.
- A76 CONFIRMED (Q71): right-click Delete on a checked row deletes all checked rows; on an unchecked row only that row.
- A77 designer-lite assumption (Q89, user may veto): if the open compose window is minimized, the repeat request restores it (normal state) and activates it.
- A78 designer-lite assumption (Q89, user may veto): opening a draft from the Drafts list (Edit button, menu, double-click; the double-click is confirmed by Q90: Drafts folder only, the double-clicked row only regardless of checked rows, not on the checkbox or buttons) while a compose window is open also only activates the open window; the draft is not opened and the open window's content is not touched.
- A79 designer-lite assumption (Q89, user may veto): the repeat request is simply dropped. Its content (reply quote, forward attachments, mailto address, draft) is not merged into or queued for the open window, and a request for a different account than the open window's account is dropped the same way (the open window keeps sending from its own account, A21).
- A80 designer-lite assumption (Q89, user may veto): the address-book picker and other modal child windows of the compose window (close dialog, empty-subject confirm, warnings) are unaffected; if one is open when the request arrives, the compose window is activated and the child stays in front of it as usual.
- A81 designer-lite assumption (Q89, user may veto): the check happens before any work (no forward attachment download, no draft fetch), and a second app launch still brings the main window to front (Q47) and does not touch the compose window.

## 20. Notices, banners and dialogs

Source: Q60, Q64. Palette, mockup and the class of each message were approved on 2026-10-08.

### 20.1 Four kinds of message

| Kind | Used for | Form |
|---|---|---|
| Banner | A state that lasts while a condition holds | Full-width bar above the top bar, bold text, at least 56 DIP high |
| Modal notice | A failure or refusal the user must read (Q64, decided) | Modal dialog with ONE big 「OK」 button (Enter and Esc close it) |
| Transient notice | A success (Q64, decided) | Bar at the bottom of the preview pane, 4 seconds (A54); delete undo 10 seconds with 「元に戻す」 |
| In-place text | Hints and field errors | Text inside the pane or dialog (header notice, red error line under a field, search note) |

Rule: modal notices are only for actions the user just performed. Problems found automatically (a body that cannot be fetched while moving down the list, sync errors) use a banner or in-place text so that several dialogs never pile up.

### 20.2 Banners (Q64 order APPROVED)

- Maximum 2 banners at a time. Priority, highest first: 1 wrong password (with 「入れ直す」), 2 offline, 3 Outbox failure, 4 initial sync (「メールを読み込んでいます…」), 5 others (missing Gmail folder, generic sync error). When more than 2 conditions hold, the two highest are shown; a lower one appears when a higher one ends.
- Close button 「×」 only on the Outbox failure banner. After closing it does not come back for the same failure (the failure marker on the Outbox row and the count on the Outbox button stay, A13, A40). The other banners end when their condition ends.
- The red suspicious-link banner is inside the preview pane at the top of the mail and is not part of this stack.

### 20.3 Message classes (APPROVED)

| Message (ui-strings ID) | Kind |
|---|---|
| Wrong/revoked password (AC-16) | Banner, red |
| Offline (BN-01) | Banner, yellow |
| Outbox failure after 3 tries (OB-01) | Banner, red, with 「×」 |
| Initial sync (BN-02) | Banner, light blue |
| Missing Gmail folder (BN-03), generic sync error (BN-19) | Banner, red |
| Suspicious link (SW-01) | In-preview red banner |
| Move refused offline (BN-05), move failed (BN-06), multi-delete partial failure (BN-21), account delete blocked (AC-20), unhandled error (BN-04), attachment blocked/too large (SW-06, CP-07, CP-09), forward dropped files (RF-05, RF-06), attachment not downloaded (BN-09), attachment download/save/open failure (BN-14 to BN-16), Send-now failed (OB-05), draft save failed after explicit save (BN-17), link refused (SW-05), WebView2 missing (PV-06) | Modal notice with 「OK」 |
| Delete done (BN-07, BN-11), block added (FA-11) | Transient notice |
| Body not downloaded (BN-08), body loading (BN-10), body fetch failure (BN-13), draft upload pending (BN-18), not in address book (PV-02), images hidden (PV-05), search note (SR-05), field errors | In-place text |

### 20.4 Colours (APPROVED; contrast ratio per WCAG 2.x, target at least 7:1 for text)

| Use | Background | Text | Ratio |
|---|---|---|---|
| Page, list, preview | #FFFFFF | #000000 | 21.0 |
| Offline banner | #FFD54F yellow | #000000 bold | 14.9 |
| Error and suspicious-link banner | #A80000 red | #FFFFFF bold | 7.9 |
| Loading / initial sync banner | #CFE8FF light blue | #000000 bold | 16.6 |
| Unread badge, Outbox failure pill | #A80000 | #FFFFFF bold | 7.9 |
| Success / undo notice | #1B5E20 dark green | #FFFFFF bold | 7.9 |
| Primary button, active folder | #0B4FA8 | #FFFFFF | 7.8 |
| Normal button | #E8E8E8, border #767676 | #000000 | 17.1 |
| Selected row | #D6E4F7 + 6 px bar #0B4FA8 | #000000 | 16.3 |
| List date text | #FFFFFF | #333333 | 12.6 |
| Hint text | #FFFFFF | #595959 | 7.0 |

The Outbox failure marker in a row is a red pill with white text (not red text), because red text on the selected-row colour is only 6.1. The Inbox count badge on the (blue) active folder button is a white pill with red text and a red border. Colour is never the only signal (text, number or bold always accompanies it). Mockup: `docs/plans/mockups/main-window.html`.

## 21. Keyboard, focus and window rules (APPROVED, A56)

Full detail, sizes and per-window sketches: `docs/plans/window-layouts.md`. Summary:
- Dialogs and windows opened from the main window are modal; the compose window (at most one at a time, Q89) is independent. Centered on the owner.
- Enter = the safe default button (confirm dialogs: the cancel-type one; add/edit forms: 保存). Exception (Q75): the compose close dialog (下書きに保存 / 保存しないで閉じる(...) / 戻る) has 「下書きに保存」 as the default, because saving loses nothing. Esc = cancel. Buttons right-aligned, the safe choice last.
- Delete key deletes only while the mail list has keyboard focus. F5 = refresh. No keyboard shortcut for Send. Ctrl+mouse wheel zooms the preview; there are no other zoom shortcuts (Q69).
- Initial focus: main window = mail list; compose = 宛先; dialogs = default button (forms: first field).
- Sizes: button height at least 56 DIP, any clickable target at least 48 DIP, gaps 16 DIP, dialog margin 24 DIP, thick visible focus rectangle.
- Tab order follows the field order listed per window in window-layouts.md. Compose: each address-book picker button directly follows its recipient field (Q84).

## 22. Multi-select delete (Q71 approved as a feature; details approved 2026-10-08)

Decided by the user (Q71): a checkbox is always shown at the left of every row of the mail list; the checked mails are deleted together with the Delete button (moved to Gmail Trash); the notice reads 「N通をごみ箱に移動しました [元に戻す]」 for 10 seconds and Undo restores all of them. The remaining details were approved on 2026-10-08 (Q77).

- Selecting vs checking (A59): clicking a row selects it (preview, single mail); clicking the row's checkbox only toggles the check. A mail can be both. Checks do not mark mail read.
- Header Delete button: with no row checked it deletes the selected mail (single delete, notice 「ごみ箱に移動しました [元に戻す]」). With one or more rows checked it deletes exactly the checked rows and its label becomes 「選んだN通を削除」; the selected row is ignored unless it is also checked. Right-click Delete: see A76. Delete key (list focused): same rule as the button.
- Preview (A60): while one or more rows are checked the preview shows 「N通のメールを選んでいます。「削除」を押すと、まとめてごみ箱に移します。」; 返信, 全員に返信, 転送, 編集 and the zoom buttons are disabled. When all checks are cleared the selected mail is shown again.
- Clearing and select-all (A61): header row of the list has a 「すべて選ぶ」 checkbox (selects the rows currently in the list, up to 200) and, while something is checked, the text 「N通を選んでいます」 with a 「選択をやめる」 button. Unchecking 「すべて選ぶ」 also clears. More than 200 checked rows: the delete is refused with 「一度に選べるのは200通までです」 (modal OK).
- Folders (A62): Inbox, Sent, Spam, Drafts and search results have the checkboxes. In Trash and Outbox the checkbox column and 「すべて選ぶ」 are hidden because the app has no permanent delete (Trash) and Outbox items are not Gmail mail.
- Search results (A63): same checkboxes and Delete. Checked mails already in Trash are skipped. Undo returns each mail to the folder shown on its result row; a mail that was only in All Mail returns to All Mail only.
- Offline: refused with the standard offline refusal modal (BN-05); nothing changes. Undo is also refused offline.
- Partial failure (A64): moved mails stay moved; the ones that failed are restored in the list; modal 「N通のうちM通を移せませんでした。もう一度お試しください。」; Undo (if still shown) covers the moved ones.
- After the delete: the checks are cleared; the next older mail after the oldest deleted position is selected (fallback: the previous one; none: guide text) as in section 1. The auto-selected mail is not marked read (A58, Q74).
- Undo: one notice, the latest delete only; a new delete replaces it. Per mail the folder it was viewed in is restored; other Gmail labels are not (Q55).
- Keyboard (A65): Space toggles the focused row's check; checks are cleared on folder, account or search change and kept when a sync reloads the list.
