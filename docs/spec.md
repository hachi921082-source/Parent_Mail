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
- Account-add screen states that a Gmail app password is required. No detailed in-app setup guide (a knowledgeable person sets up the accounts).
- If the app password is wrong or no longer valid: show a plain-language notice asking a knowledgeable person to re-enter it, and open the password re-entry screen; cached mail stays viewable.

## 4. UI Layout

Main window has 3 areas (see sketch: `docs/images/layout-sketch.png`).

- Top bar (full window width), left to right: account switch button, Inbox, Outbox, Sent, Trash, Drafts, Spam. Folders are not in a left pane.
- Mail list: narrow pane at the left below the top bar.
  - Action buttons at the bottom of the mail list pane: compose, search, settings, address book, block list, filter rules.
- Mail preview (text): very wide pane at the right, taking most of the window.
  - Large buttons always shown at the top of the preview pane: Reply, Reply all, Forward, Delete (the context menu remains available as well).
  - Preview zoom ("mail enlarged display") controls near these buttons (see section 5).
  - Each row shows: sender, subject, date/time, attachment icon; unread mails in bold.
- Composing a new mail, reply, and forward open in a separate window, not inline in the main window.
- The boundary between mail list and preview is draggable (resizable splitter).
- Unread count is shown only on the Inbox folder button. Exception: the account switch button shows a small unread badge showing the unread count when the other (non-displayed) account has unread mail.
- HTML/plain-text switch: a checkbox in the top bar. It applies to the currently displayed mail only; opening another mail resets it to the default plain-text display.

## 5. Confirmed Features (first release)

All features below are in the first release (no phasing), including the local mail cache (section 8).

- Standard folders: Inbox, Outbox, Sent, Trash, Drafts, Spam, each with the context (right-click) menu items needed for it.
- Address book (separate per account).
- Filter/sorting rules (per account).
- Block list (per account).
- Attachment by drag and drop.
- HTML mail display plus plain-text mode.
- Mail content preview, including images.
- Search.
- Compose with To, CC, BCC.
- Reply / reply all / forward (open in a separate window, same as compose).
- Attachment save / open.
- Text-size setting (large by default, changeable by free numeric input or slider).
- Mail enlarged display (preview zoom): the preview can be enlarged temporarily; the zoom is not saved and returns to the default when the app exits.
- Read/unread display (unread shown in bold in the mail list).
- Safety warnings for suspicious links and executable attachments. Attaching an executable file to an outgoing mail is warned about and blocked.
- Delete (button or menu item) shows no confirmation dialog; a non-modal notice "ごみ箱に移動しました [元に戻す]" is shown for a few seconds and Undo moves the mail back to its previous folder.
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

- Language/framework: C# with WPF.
- Mail library: MailKit (IMAP/SMTP).
- HTML display: WebView2. No app CSS font-size is injected into HTML mail; text size and preview zoom are applied through the WebView2 ZoomFactor (setting / 18pt x temporary zoom). The text-size setting applies to WPF controls and plain-text display directly.
- App password storage: Windows Credential Manager.
- LLM features: none.
- Local storage: mail is also stored locally on the PC (local mail cache) in SQLite, in addition to settings (account names, filter rules, address book, block list, window/splitter state).
- Local data protection: no app-level encryption. Data is protected only by the Windows user account; BitLocker and any other encryption setup are not needed.
- Privacy: mail content stays on the PC; no transmission to third parties.
- Outbox is a local-only concept (not a Gmail folder).
- Delete: moves mail to Gmail Trash only (IMAP move). Permanent deletion and "Empty trash" are not provided in the app.
- Local copy mirrors Gmail state: moves to Gmail Trash are reflected locally; a mail permanently gone from Gmail is also deleted from the local copy.
- Sync:
  - First sync downloads headers for all mail and bodies for the most recent 12 months.
  - Older bodies and all attachments are downloaded on demand when opened, then cached locally (SQLite).
  - Periodic sync checks only new/changed mail (IMAP UID/UIDVALIDITY), not the whole mailbox.
  - Optional: background backfill of older bodies when idle.
  - Gmail "All Mail" is also synced, under the same rules as the other folders; a message appearing in several Gmail folders is stored once (keyed by X-GM-MSGID).
  - Both accounts are synced on the same timer (startup, then every 5 minutes).
- Fetch timing: on app startup, then automatically every 5 minutes, plus a manual refresh button.
- Send failure (online or offline): the mail stays in Outbox, is retried every sync cycle (5 minutes), and a plain-language failure notice is shown after 3 failures; Outbox menu: "Send now" / "Return to drafts".
- Attachment limit: size is checked on the estimated encoded message size against Gmail's 25 MB (about 18 MB of raw files); over the limit, a plain-language warning is shown and the attachment is refused.
- Search: local cache only (sender, subject, downloaded bodies) across all folders of the displayed account; not-downloaded older bodies are excluded and the UI says so; no IMAP server search.
- Sync exclusion: one sync at a time per account; a manual refresh during a sync is merged into it (button shown busy); timer ticks during a sync are skipped.
- Quitting the app with unsent mail in Outbox shows a warning ("送信待ちのメールがあります。このまま閉じると送信されません") with three large buttons: send then close / close anyway / back (default). If the send-then-close attempt fails, the app stays open and shows the failure notice.
- Offline / Gmail unreachable: show cached mail with a plain-language banner; outgoing mail is held in Outbox and sent automatically after reconnect.
- HTML display default: plain-text mode; the top-bar checkbox switches to HTML for the currently displayed mail only (reset on opening another mail). If a mail has no text part (HTML only), the plain text derived from the HTML is shown instead of an empty pane.
- Remote images in HTML mail: not loaded by default; a "Show images" action loads them on demand. Images embedded in the mail (cid/inline) display normally.
- Offline with unfetched older body/attachment: shows a plain-language "not downloaded yet" message; fetched and shown automatically once online; other cached mail stays readable.
- Default text size: 18pt equivalent, changeable in settings.
- Minimum window size: 1024x700.
