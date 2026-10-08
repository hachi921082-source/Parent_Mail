# Detailed Specification

<!-- Elaboration of spec.md to a level usable for implementation plans (docs/plans/<feature-name>/PLAN.md). Created/updated by designer-lite as features are planned. -->

Sources: `docs/spec.md` (S#), `docs/QA.md` resolved items (Q#) and the 21 approved assumed defaults (D#, as listed in QA.md "Assumed defaults"). Statements not traceable to those are marked **Assumption (An)** and collected in section 19. No rationale here (see DECISIONS.md).

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

## 1. Window and layout

Source: S4, Q13, Q33, D8-D9.

- Main window, minimum size 1024x700 (S8). Window size and splitter position are persisted in local settings (S8).
- Top bar, full width, left to right: account switch button, Inbox, Outbox, Sent, Trash, Drafts, Spam buttons (S4). Then the HTML/plain-text checkbox (S4; it applies to the currently displayed mail only, see section 6, Q39) and a manual refresh button (S8). Position of checkbox and refresh button within the bar: Assumption A1 (right side of the bar).
- Account switch button: labeled, shows current account name (S3). Shows a small unread badge, showing the unread count as a number, when the other (non-displayed) account has unread mail (Q13; number display Assumption A44).
- Unread count is shown only on the Inbox button, besides the account-button badge (S4).
- Mail list pane: narrow, left, below the top bar. Row = sender, subject, date/time, attachment icon; unread rows in bold (S4). Dates/times use Japanese format, e.g. 2026年10月8日 14:30; same-day mails may show time only (exact format Assumption A45). Action buttons at the bottom of the pane, in order: compose, search, settings, address book, block list, filter rules (S4).
- Preview pane: very wide, right, most of the window (S4). Above the preview, large buttons always visible: Reply, Reply all, Forward, Delete (Q33). Context menu remains available (Q33). Next to these buttons: preview zoom controls "+", "-" and "reset" (Q38; section 6; control form Assumption A34).
- Splitter between list and preview is draggable (S4).
- Compose, reply, forward open in a separate window (S4).
- The folder buttons use Japanese labels; exact wording: Assumption A2 (Inbox=受信トレイ, Outbox=送信トレイ, Sent=送信済み, Trash=ごみ箱, Drafts=下書き, Spam=迷惑メール).
- Header buttons act on the selected mail. When no mail is selected they are disabled: Assumption A3. In Trash, Drafts and Outbox, Delete/Reply/Reply all/Forward availability follows the context-menu tables in section 9 (items not listed there are disabled): Assumption A3.

## 2. Accounts and authentication

Source: S1, S3, S8, Q31, D4.

- Gmail only, auth by Gmail app password via IMAP/SMTP (MailKit). Maximum 2 accounts. "Add account" is unavailable once 2 exist (S3; unavailability form is Assumption A4).
- Each account has a renamable display name (S3). Rename is done in settings (section 15): Assumption A5.
- Account-add screen: fields = email address, app password, display name (Assumption A6); states that a Gmail app password is required. No in-app setup guide (S3).
- App password stored in Windows Credential Manager (S8). Never in SQLite or plain files.
- Add account, delete account and password re-entry are behind a warning dialog: text says to ask a knowledgeable person; default button = Cancel; no PIN (Q31). Exception: the account-add screen shown on first launch when no account exists yet is not preceded by this dialog (Assumption A31).
- Deleting an account removes its credential, its cache and its per-account data (address book, block list, filter rules): Assumption A7.
- Wrong or revoked app password (S3): show a plain-language notice asking a knowledgeable person to re-enter it; open the password re-entry screen; cached mail stays viewable. Re-entry screen is not preceded by the Q31 warning dialog because it is opened by the app: Assumption A8.
- Account switch: clicking the account switch button changes the displayed account; list, folders, counts, search and per-account data all follow the displayed account (S3, Q28, Q35).

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
- Periodic sync: only new/changed mail via IMAP UID/UIDVALIDITY (S8). If UIDVALIDITY changes for a folder, that folder's cache is rebuilt: Assumption A10.
- Optional (not required): background backfill of older bodies when idle (S8).
- Fetch timing: at app startup, then every 5 minutes, plus manual refresh button (S8). At most one sync runs per account at a time: a manual refresh pressed during a running sync is merged into it (the refresh button is shown as busy) and timer ticks during a running sync are skipped (Assumption A39). Both accounts are synced on the same startup/5-minute timer (spec.md section 8, Q36), which feeds the other-account badge.
- Mirror rules: moves to Gmail Trash are reflected locally; mail permanently gone from Gmail is deleted from the local copy (S8).
- Read state: unread shown bold (S5). Opening a mail marks it read and the state is synced to Gmail (\Seen): Assumption A11. Context menu offers Mark read/unread (D8).
- Drafts: synced with Gmail Drafts (Q12). Auto-saved drafts (section 7) are written to Gmail Drafts when online; when offline they are kept locally and uploaded after reconnect: Assumption A12.
- New-mail handling order for a new message: download header and body (bodies of new mail are always fetched, Q17), apply block list and filter rules (sections 11, 12), then update counts/badge.
- "New mail" = mail first seen after the initial sync of that account completes. Block list and filter rules are NOT applied during the initial sync or when an account is first added (Assumption A43).
- Offline, unfetched older body or attachment: show plain-language "not downloaded yet" message; fetch and show automatically once online; other cached mail stays readable (S8).

## 5. Offline, Outbox and send retry

Source: S8, Q21, Q22, D3.

- Offline / Gmail unreachable: show cached mail with a plain-language banner (S8).
- Send: pressing Send sends immediately, with no confirmation dialog and no undo window (Q21). The message is placed in Outbox and submitted via SMTP right away; on success it is removed from Outbox and appears in Sent via Gmail (D3). If offline, it stays in Outbox and is sent automatically after reconnect (S8).
- Failure: the message stays in Outbox and is retried at each sync cycle (5 min). After 3 failures a plain-language failure notice is shown (Q22). Outbox context menu: Send now, Return to drafts (Q22, D9). The failure notice is shown as a banner and a marker on the Outbox row; exact form: Assumption A13. The failure is also visible on the Outbox top-bar button (marker or count of failed messages): Assumption A40.
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
- Preview header shows the not-in-address-book notice when applicable (section 14).
- Text size applies to the preview (section 15), as follows (Q42):
  - The app text-size setting (18pt default) applies to WPF controls (list, buttons, headers, dialogs) and to plain-text display.
  - In HTML display mode no app CSS font-size is injected into WebView2. Instead the WebView2 ZoomFactor is used, derived from the text-size setting (setting / 18pt reference, i.e. 100% at the default) multiplied by the temporary preview zoom.
  - Plain-text display uses the real font size multiplied by the temporary preview zoom.
  - The exact ZoomFactor formula and rounding are settled in PLAN.md: Assumption A41.
- Mail enlarged display (preview zoom) (Q38, S5):
  - Zoom is a temporary multiplier on top of the text-size setting and affects the preview pane only; it does not change the stored text-size setting; the compose window is not affected (Assumption A33).
  - Controls: large "+", "-" and "reset" buttons beside the preview header buttons, plus Ctrl+mouse wheel over the preview (Assumption A34).
  - Range 100%-300%, step 25%, default 100% (Assumption A35).
  - Zoom stays while switching mails, folders and accounts within the session (Assumption A36).
  - Zoom is not persisted: it returns to the default (100%) when the app exits, so every launch starts at 100% (Q38; interpretation in A37).
- Plain text mode shows URLs as text; links are clickable and go through the suspicious-link rules (section 14): Assumption A18.

## 7. Compose window

Source: S4, S5, Q19, Q20, Q21, D14-D16.

- Separate window, opened by the compose button in the main window (S4).
- Fields: To, CC, BCC, subject, body, attachments (S5). BCC is hidden until requested (D14).
- Recipient autocomplete: candidates from the displayed account's address book and that account's previously used recipients (Q19, D17). "Previously used" = addresses in To/CC/BCC of mails in that account's Sent: Assumption A19.
- Attachments: drag and drop onto the compose window, plus an "Attach" button (D16). Size limit: Gmail's 25 MB applies to the whole encoded message and base64 inflates by about 1.37x, so the check uses the estimated encoded message size (limit 25 MB, roughly 18 MB of raw files). When exceeded, warn in plain language 「ファイルが大きすぎます」 and refuse the attachment (D16, Q42). The exact threshold computation (body and header overhead, rounding) is Assumption A42. Each attachment can be removed (D16). Executable files (section 14 list, archives one level deep) are warned about and blocked from being attached (Q41).
- Draft auto-save every 30 seconds while editing (D15, S5); drafts are stored in Gmail Drafts (Q12, section 4).
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

- Open in a separate window, same as compose (S5).
- Reply quotes the original (D14). Forward quoting/attachments carry-over: Assumption A22 (forward includes the original text quoted and the original attachments).
- Subject prefix and quote format: Assumption A22 ("Re: " / "Fwd: " prefixes).
- Reply and reply all pre-fill To/CC from the original: Assumption A22.

## 9. Folder context menus

Source: S5, D8-D9, Q22.

| Folder | Items |
|---|---|
| Inbox | Reply, Reply all, Forward, Mark read/unread, Move to Spam, Delete, Add sender to address book, Block sender |
| Sent | Forward, Delete |
| Drafts | Edit, Delete |
| Spam | Not spam (move to Inbox), Delete |
| Trash | Restore (to Inbox) only; no permanent delete |
| Outbox | Send now, Return to drafts |

- Delete = IMAP move to Gmail Trash (S8). Applies to both the Delete menu item and the Delete header button.
- Delete shows no confirmation dialog. Instead a non-modal notice 「ごみ箱に移動しました [元に戻す]」 is shown for a few seconds; "元に戻す" (Undo) moves the mail back to its previous folder (Q40). Notice duration 5 seconds: Assumption A38.
- "Add sender to address book" adds to the displayed account's address book (D19). "Block sender" adds to the displayed account's block list (Q35); its entry-unit choice follows section 11.
- Preview-header buttons (section 1) are enabled only where the same action exists in this table: Assumption A3.

## 10. Address book

Source: S5, Q27, Q28, D17-D19.

- One address book per account (Q28).
- Fields per person: name, reading (yomigana, used for sorting), one or more email addresses, memo (Q27).
- Opened by the address book button at the bottom of the list pane (S4). UI form (list plus add/edit/delete dialog, sorted by reading): Assumption A23.
- Used by: recipient autocomplete (D17), not-in-address-book notice (D18), "Add sender to address book" (D19).
- Stored in local settings (S8).

## 11. Block list

Source: S5, Q15, Q16, Q35.

- One block list per account (Q35).
- Entry = email address or whole domain, chosen at registration (Q16).
- Mail from a blocked sender is moved to Spam (Q15).
- Opened by the block list button (S4); entries can be viewed and removed: Assumption A24.
- Applies to new incoming mail only, evaluated before filter rules: Assumption A25 (existing mail is not moved when an entry is added).

## 12. Filter rules

Source: S5, Q17, Q18, Q35, D10-D12.

- One rule set per account (Q35).
- Conditions: sender contains, subject contains, body contains (Q17). Multiple conditions in one rule combine as AND: Assumption A26.
- Actions: move to Trash, move to Spam, mark as read (Q18). No custom folders.
- Applies to new incoming mail only; evaluated top to bottom; first match stops (D10).
- Edited in a simple list dialog with enable/disable per rule (D11); opened by the filter rules button (S4).
- Body condition: evaluated after the new mail's body is fetched; HTML-only mails matched on the derived plain text; mail with undownloaded body never matches body conditions; rules never trigger old-body fetches (D12).
- Rule order is changeable (move up/down): Assumption A27.

## 13. Search

Source: S5, Q11, Q29, Q30, D20.

- An inline search box is always visible at the top of the list pane; results replace the list; a "Clear" button restores it (D20). Pressing the search button at the bottom of the list pane (S4) moves the input focus to this box (Assumption A47).
- Scope: all folders of the displayed account, including mail present only in All Mail (Q29, Q11). Results show the folder name (Q29).
- Matching: local cache only; fields = sender, subject, downloaded bodies. Unfetched older bodies are excluded and the UI says so (Q30). Case-insensitive substring match: Assumption A28.
- Works offline (Q30).

## 14. Safety warnings

Source: S5, Q23-Q26.

Suspicious link (Q23), a link is suspicious if any holds:
- the shown text differs from the real target;
- the URL host is an IP address;
- the URL is a known shortener (list maintained in code): Assumption A29;
- the scheme is not https;
- the domain is look-alike (punycode `xn--` or similar): detection method Assumption A29.

Warning UI (Q24):
- a red banner at the top of the mail when it contains a suspicious link;
- clicking such a link opens a confirmation dialog showing the real destination; default button = Cancel.

Executable attachments (Q25): extensions exe, msi, bat, cmd, scr, js, vbs, ps1, jar, lnk, and macro-enabled Office files (extensions docm, xlsm, pptm and the other macro-enabled Office types: Assumption A30). Archives are inspected one level deep for these. Strong warning; opening is forbidden; saving is allowed only after confirmation.

Attaching an executable file when composing (Q41): the same extension list as the receive-side rule applies, with archives checked one level deep. Attaching is warned about and BLOCKED (the file is not added). This applies to both drag and drop and the "Attach" button (section 7).

Sender not in the address book (Q26, D18): a notice in the preview header only (not in list rows). Wording "Not in your address book" in Japanese: Assumption A2.

## 15. Settings

Source: S5, S8, Q31, Q32, D21.

- Opened by the settings button (S4). Contents: text size, accounts (rename, add, delete, password re-entry) (A5; Q31 for protection). Anything else is not part of the first release (including signature, print, star, themes, S6).
- Text size: default 18pt equivalent (S8). Changed by free numeric input or slider (Q32). Range 12-48pt, live sample preview in the settings screen, applies to list, preview, compose window and dialogs (D21).
- Account add, delete and password re-entry are behind the warning dialog (section 2, Q31).
- Preview zoom (section 6) is not a setting and is not stored; the text-size setting is unaffected by it (Q38, A33).

## 16. Notifications, startup and shutdown

Source: Q13, Q14, Q34, D5-D6.

- New-mail notification: in-app only (the Inbox unread count changes); no toast, no sound (Q14).
- Other-account indicator: unread badge on the account switch button (Q13).
- Startup: opens the last-used account's Inbox (D5); a sync starts (S8). First launch without an account opens the account-add screen: Assumption A31.
- No auto-start with Windows and no setting for it (D6).
- Closing the main window (X) quits the app (Q34). No tray, no background process. Because of this, fetching and the Outbox retry run only while the app is open.
- Quit with unsent mail in Outbox (X, and Windows shutdown/close where feasible) shows a warning dialog 「送信待ちのメールがあります。このまま閉じると送信されません」 with three large buttons (Q43):
  - 「送信してから閉じる」: try sending now, then quit; if sending fails, the app stays open and the failure notice is shown.
  - 「このまま閉じる」: quit; unsent mail stays in Outbox.
  - 「戻る」 (default): return to the app.
  - Whether Windows shutdown can be intercepted is an implementation matter left to PLAN.md: Assumption A48.
- Open compose windows at quit: handled by the section 7 close dialog: Assumption A32.

## 17. Message tone

Source: D1, S3.

- UI language: Japanese only (D1).
- Notices and errors: polite plain Japanese, no technical terms, one sentence on what to do next. Problems that the parent cannot fix say to ask a knowledgeable person (D1).
- Banners required by the spec: offline banner and wrong-password notice (S3, S8, D7).
- Close-dialog buttons: 「下書きに保存」, 「保存しないで閉じる(前に自動保存された下書きは残ります)」, 「戻る」.

## 18. Non-functional requirements

Source: S1, S8.

- Windows desktop (development environment Windows 11), C# with WPF, MailKit, WebView2, SQLite, Windows Credential Manager (S1, S8).
- Minimum window size 1024x700 (S8).
- Privacy: mail content stays on the PC; no transmission to third parties; no LLM features (S8).
- No app-level encryption of local data (S8).
- All paths and file names (attachments, save dialogs, cache paths) must work with Japanese and other non-ASCII characters (global CLAUDE.md).

## 19. Assumptions

Added by designer-lite because the source documents do not state them; each needs user veto or later confirmation.

- A1 Checkbox and refresh button sit on the right side of the top bar.
- A2 Japanese labels: 受信トレイ, 送信トレイ, 送信済み, ごみ箱, 下書き, 迷惑メール; not-in-address-book notice wording in Japanese.
- A3 Preview-header buttons are disabled with no selection and when the action is absent from the section 9 table for that folder.
- A4 "Add account" is disabled or hidden once 2 accounts exist.
- A5 Account rename lives in the settings screen.
- A6 Account-add fields: email address, app password, display name.
- A7 Deleting an account removes its credential, cache, address book, block list and filter rules.
- A8 Password re-entry screen opened by the app is not preceded by the Q31 warning dialog.
- A9 Per-account cache DB plus a separate settings DB.
- A10 UIDVALIDITY change rebuilds that folder's cache.
- A11 Opening a mail marks it read and syncs \Seen to Gmail.
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
- A28 Search is a case-insensitive substring match.
- A29 Shortener list and look-alike-domain detection method are implementation choices to be fixed in PLAN.md.
- A30 Macro-enabled Office extensions: docm, xlsm, pptm, dotm, xltm, potm, xlam, ppam, ppsm.
- A31 First launch without any account opens the account-add screen, which is not preceded by the Q31 warning dialog.
- A32 Quitting with open compose windows goes through the section 7 close dialog.
- A33 Preview zoom is a temporary multiplier on the text-size setting, applies to the preview pane only, does not change the stored text-size setting, and does not affect the compose window.
- A34 Zoom controls: large "+", "-", "reset" buttons beside the preview header buttons, plus Ctrl+mouse wheel.
- A35 Zoom range 100%-300%, step 25%, default 100%.
- A36 Zoom persists while switching mails, folders and accounts within a session.
- A37 "Return to default at exit" is interpreted as: zoom is not persisted and every launch starts at 100% (to be confirmed by the user).
- A38 The Delete undo notice stays for 5 seconds.
- A39 Sync mutual exclusion: at most one sync per account at a time; a manual refresh during a running sync is merged into it (button shown busy); timer ticks during a running sync are skipped.
- A40 A send failure is also shown on the Outbox top-bar button (marker or count of failed messages), extending A13.
- A41 Exact WebView2 ZoomFactor formula (text-size setting / 18pt x temporary zoom) and rounding are settled in PLAN.md.
- A42 Exact attachment threshold computation (estimated encoded size incl. body/header overhead, about 18 MB of raw files) is settled in PLAN.md.
- A43 Block list and filter rules are not applied during the initial sync or when an account is first added; "new mail" = mail first seen after the initial sync completes.
- A44 The other-account badge on the account switch button shows the unread count as a number.
- A45 Dates/times use Japanese format (e.g. 2026年10月8日 14:30); same-day mails may show time only; exact format settled in PLAN.md.
- A46 On successful send, the matching Gmail Drafts draft is deleted (also for drafts returned from Outbox and later sent); a failed send keeps the draft/Outbox item.
- A47 The search button moves input focus to the always-visible inline search box.
- A48 Intercepting Windows shutdown for the Outbox quit warning is done where feasible; settled in PLAN.md.
