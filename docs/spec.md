# Specification

<!-- Confirmed specification. Built up collaboratively with designer-lite; records only "what is currently decided." -->

## Index

1. Basic Information
2. Purpose and Target Users
3. Accounts
4. UI Layout
5. Confirmed Features (first release)
6. On-Hold Features
7. Non-Goals
8. Technical Requirements

## 1. Basic Information

- App name: Parent_Mail
- Platform: Windows desktop app (development environment: Windows 11)
- Mail service: Gmail only
- Connection: IMAP/SMTP with Gmail app password

## 2. Purpose and Target Users

- Purpose: email client that lets the user's own parents send and receive Gmail on a PC.
- Target users: the user's family (primarily elderly parents unfamiliar with PCs and smartphones, with poor eyesight).
- UI policy: simple UI, large text, minimal operations.

## 3. Accounts

- Gmail accounts only; authentication by Gmail app password.
- Maximum 2 accounts.
- Each account has a renamable display name.
- Switched by the account switch button at the left end of the top bar. It is an obvious, labeled button showing the current account name.
- Account-add screen states that a Gmail app password is required. Adding an account first tests IMAP and SMTP login with the entered address and app password; a duplicate address is rejected. No detailed in-app setup guide (a knowledgeable person sets up the accounts).
- If the first account-add screen (no account exists yet) is closed without adding an account, a confirmation 「このまま閉じるとアプリは終わります。よろしいですか?」 with 「終わる」 / 「戻る」 (default 戻る) is shown (wording approved).
- An account that still has mail in its Outbox cannot be deleted: the deletion is stopped with a message asking the user to send the mail or return it to drafts first.
- If the app password is wrong or no longer valid: show a plain-language notice asking a knowledgeable person to re-enter it, and open the password re-entry screen; cached mail stays viewable.

## 4. UI Layout

Main window has 3 areas (see sketch: `docs/images/layout-sketch.png`).

- Top bar (full window width), left to right: account switch button, Inbox, Outbox, Sent, Trash, Drafts, Spam. Folders are not in a left pane.
- Mail list: narrow pane at the left below the top bar.
  - Each row has 2 lines: line 1 = sender, date/time and an attachment icon (paperclip); line 2 = subject, shortened with an ellipsis when long. Unread mails are bold (bold only). A checkbox is always shown at the left of every row (multi-select delete, section 5).
  - Action buttons at the bottom of the mail list pane (Japanese labels), a 2 x 3 grid filled row by row: 新しいメール (compose), [blank cell, kept free; no search button because the search box above the list is always visible], settings, address book, 受信拒否リスト (block list), 振り分けルール (filter rules).
  - Dates in rows: today `HH:mm`; this year `M月d日 H:mm`; older `yyyy年M月d日 H:mm` (local time).
- Mail preview (text): very wide pane at the right, taking most of the window.
  - Large buttons always shown at the top of the preview pane: Reply, Reply all, Forward, Delete (the context menu remains available as well). In Drafts there is also a large 「編集」 button. Other folder-specific actions (迷惑メールではない, 元のフォルダに戻す, 今すぐ送る, 下書きに戻す) are right-click only.
  - No mail selected: the preview shows the guide text 「左の一覧からメールを選んでください」.
  - Preview zoom ("mail enlarged display") controls near these buttons (see section 5).
- Composing a new mail, reply, and forward open in a separate window, not inline in the main window. Only one such window exists at a time (section 5).
- The boundary between mail list and preview is draggable (resizable splitter). First launch: the window opens maximized with the list pane about 35 percent of the width; afterwards the last size and splitter position are restored.
- The text of the top bar and of the bottom button strip of the list pane is capped (it stops growing above a maximum size) so that it always fits; mail list, preview and mail text follow the text-size setting up to 48pt. The cap is 20pt; the top bar has two rows and the bottom strip is a 2 x 3 grid (detailed-spec section 1).
- Unread count is shown only on the Inbox folder button. Exception: the account switch button shows a small unread badge showing the unread count when the other (non-displayed) account has unread mail.
- HTML/plain-text switch: a checkbox in the top bar. It applies to the currently displayed mail only; opening another mail resets it to the default plain-text display.

## 5. Confirmed Features (first release)

All features below are in the first release (no phasing), including the local mail cache (section 8).

- Standard folders: Inbox, Outbox, Sent, Trash, Drafts, Spam, each with the context (right-click) menu items needed for it.
- Address book (separate per account). Fields: 名前, ふりがな, メールアドレス, メモ. A sender not in it gets the preview-header notice 「アドレス帳に登録されていません」.
- Filter/sorting rules (per account). Window and button name: 「振り分けルール」. Condition "sender contains" matches the address and the display name, case-insensitive.
- Block list (per account). A domain entry also covers its subdomains. Window and button name: 「受信拒否リスト」.
- Attachment by drag and drop.
- HTML mail display plus plain-text mode.
- Mail content preview, including images.
- Search: local cache `LIKE` substring match, normalizing case and full/half width only (no hiragana/katakana folding).
- Compose with To, CC, BCC. Besides autocomplete, a button beside each of To, CC and BCC opens an address-book picker (modal to that compose window, multi-select, the compose window's own account's address book; Q80-Q85). Compose body is plain text only (text/plain, UTF-8). The From header carries the address only (the account display name is never sent). Only one app instance runs; a second launch brings the existing window to front.
- Reply / reply all / forward (open in a separate window, same as compose).
- Only ONE compose window can be open at a time (Q89, Issue #5). While it is open, pressing 新しいメール, 返信, 全員に返信, 転送, editing a draft or opening a `mailto:` link only brings the existing compose window to the front (restoring it if minimized); no notice is shown and no second window is created.
- Attachment save / open.
- Text-size setting (large by default, changeable by free numeric input or slider).
- Mail enlarged display (preview zoom): the preview can be enlarged temporarily; the zoom is never saved. It resets to 100% whenever the displayed mail, folder or account changes (and therefore on every launch). Buttons: 拡大 / 縮小 / 100%に戻す, plus Ctrl+mouse wheel over the preview; there are no other zoom keyboard shortcuts.
- Read/unread display (unread shown in bold in the mail list). Opening a mail marks it read immediately, only in Inbox and Spam.
- Safety warnings for suspicious links and executable attachments. Attaching an executable file to an outgoing mail is warned about and blocked. Executable list: exe, msi, bat, cmd, scr, js, vbs, ps1, jar, lnk, macro-enabled Office files, plus hta, com, pif, reg, wsf, vbe, jse, msc, dll, cpl, iso, img and similar script/installer/disk-image types (full list in detailed-spec section 14). Only zip is inspected, one level deep; an unreadable or encrypted zip is treated as suspicious. Extension match: last extension, case-insensitive, trailing spaces/dots stripped; file names with right-to-left override characters are flagged. Link banner judges both HTML and text parts; non-suspicious links open in the default browser with no dialog; `mailto:` opens compose.
- Delete (button, menu item or the Delete key while the mail list has focus) shows no confirmation dialog; a non-modal notice "ごみ箱に移動しました [元に戻す]" is shown for 10 seconds and Undo moves the mail back to the folder it was viewed in (other Gmail labels are not restored). Moving a mail to Spam shows no undo notice. After a delete, move to Spam or Restore, the next older mail in the list is selected automatically (the previous one if there is none; the guide text if the list is empty). The automatically selected mail is NOT marked read; only a mail the user selects explicitly is.
- Multi-select delete: checked mails (checkbox at the left of each row) are deleted together with the Delete button (moved to Gmail Trash); the notice reads 「N通をごみ箱に移動しました [元に戻す]」 for 10 seconds and Undo restores all of them. Details and open proposals: detailed-spec section 22.
- Notices: a failure or refusal the user must read is a modal dialog with one big 「OK」 button; a success is a transient banner or notice. Several banners can be shown at once (maximum 2, in a fixed priority order; only the Outbox failure banner has a close button 「×」). Details: detailed-spec section 20. The app remembers, per mail, the folder it was deleted from (kept in the local cache, so it survives app restarts); in Trash the menu item 「元のフォルダに戻す」 moves the mail back to that folder (a mail deleted from Sent returns to Sent). If the origin is unknown (trashed on another device or Gmail web, trashed before this was recorded, or only in All Mail) it goes to the Inbox. Other Gmail labels are not restored. While offline, moves (delete, spam, restore, undo) are refused with a plain notice; mark read/unread is queued.
- Draft auto-save.

## 6. On-Hold Features

Not in the first release; may be reconsidered later.

- Signature
- Print
- Star marks
- Additional contrast themes (beyond text size)

## 7. Non-Goals

Out of scope:

- Mail services other than Gmail
- 3 or more accounts
- Smartphone version
- Sharing with others
- Calendar integration
- AI features

## 8. Technical Requirements

- Language/framework: C# with WPF, net10.0-windows. Solution = App (WPF) + Core (incl. Data) + Tests (xUnit). CommunityToolkit.Mvvm and Microsoft.Extensions.DependencyInjection.
- Mail library: MailKit (IMAP/SMTP); connect/read/send timeout 30 s; polling only (no IDLE).
- HTML display: WebView2. No app CSS font-size is injected into HTML mail; text size and preview zoom are applied through the WebView2 ZoomFactor = text-size setting (pt) / 12 x preview zoom, clamped to 0.25-5.0 and rounded to 2 decimals (1.5 at the default 18pt, so HTML matches plain text). WebView2 built-in zoom shortcuts are disabled. Font size is in true points (WPF size = pt x 96/72). The text-size setting applies to WPF controls and plain-text display directly.
- App password storage: Windows Credential Manager.
- LLM features: none.
- Local storage: mail is also stored locally on the PC (local mail cache) in SQLite (Microsoft.Data.Sqlite; settings.db plus one cache.db per account; HtmlAgilityPack for HTML parsing), in addition to settings (account names, filter rules, address book, block list, window/splitter state).
- Local data protection: no app-level encryption. Data is protected only by the Windows user account; BitLocker and any other encryption setup are not needed.
- Privacy: mail content stays on the PC; no transmission to third parties.
- Local log file `%LOCALAPPDATA%\Parent_Mail\logs\parentmail.log`: component names, timestamps, exception type/message/stack, counts and error categories only; never mail content, addresses, subjects, attachment names, passwords or URLs. Rotated at 1 MB, 3 files.
- Distribution: self-contained single-file exe named `Parent_Mail_YYYYMMDD-HHMMSS.exe`; no installer.
- Outbox is a local-only concept (not a Gmail folder).
- Delete: moves mail to Gmail Trash only (IMAP move). Permanent deletion and "Empty trash" are not provided in the app.
- Local copy mirrors Gmail state: moves to Gmail Trash are reflected locally; a mail permanently gone from Gmail is also deleted from the local copy.
- Sync:
  - While the first sync runs, the banner 「メールを読み込んでいます…」 is shown and the list fills progressively without blocking the UI.
  - If a folder's UIDVALIDITY changes, only that folder's UID mappings are rebuilt (re-resolved via X-GM-MSGID); bodies and attachments are kept and re-listed mail is not treated as new mail.
  - First sync downloads headers for all mail and bodies for the most recent 12 months.
  - Older bodies and all attachments are downloaded on demand when opened, then cached locally (SQLite).
  - Periodic sync checks only new/changed mail (IMAP UID/UIDVALIDITY), not the whole mailbox.
  - Background backfill of older bodies while the app is idle is included (parameters in detailed-spec section 4).
  - During the first sync with an empty list the list shows no 「メールはありません」, only the loading banner.
  - Gmail "All Mail" is also synced, under the same rules as the other folders; a message appearing in several Gmail folders is stored once (keyed by X-GM-MSGID).
  - Both accounts are synced on the same timer (startup, then every 5 minutes).
- Fetch timing: on app startup, then automatically every 5 minutes, plus a manual refresh button.
- Send failure (online or offline): the mail stays in Outbox, is retried every sync cycle (5 minutes) with no upper limit, and a plain-language failure notice is shown once after 3 failures (the failure marker stays); Outbox menu: "Send now" / "Return to drafts".
- Attachment limit: size is checked on the estimated encoded message size against Gmail's 25 MB (about 18 MB of raw files): sum over files of ceil(n/3) x 4 x 78/76, plus body bytes, plus 10 KB header overhead, must be <= 25,000,000 bytes; over the limit, a plain-language warning is shown and the attachment is refused.
- Search: local cache only (sender, subject, downloaded bodies) across all folders of the displayed account; not-downloaded older bodies are excluded and the UI says so; no IMAP server search.
- Sync exclusion: one sync at a time per account; a manual refresh during a sync is merged into it (button shown busy); timer ticks during a sync are skipped.
- Quitting the app with unsent mail in Outbox shows a warning ("送信待ちのメールがあります。このまま閉じると送信されません") with three large buttons: send then close / close anyway / back (default). If the send-then-close attempt fails, the app stays open and shows the failure notice. The same dialog is shown when Windows shutdown/logoff is intercepted (SessionEnding); "close anyway" lets Windows continue.
- Forward fetches the original's attachments and applies the same executable and size checks; violating attachments are dropped with a notice. Attachments not yet downloaded while offline: notice, forward continues without them.
- Offline / Gmail unreachable: show cached mail with a plain-language banner; outgoing mail is held in Outbox and sent automatically after reconnect.
- HTML display default: plain-text mode; the top-bar checkbox switches to HTML for the currently displayed mail only (reset on opening another mail). If a mail has no text part (HTML only), the plain text derived from the HTML is shown instead of an empty pane.
- Remote images in HTML mail: not loaded by default; a "Show images" action loads them on demand. Images embedded in the mail (cid/inline) display normally.
- Offline with unfetched older body/attachment: shows a plain-language "not downloaded yet" message; fetched and shown automatically once online; other cached mail stays readable.
- Default text size: 18pt equivalent, changeable in settings.
- Minimum window size: 1024x700.
