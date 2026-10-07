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
  - Each row shows: sender, subject, date/time, attachment icon; unread mails in bold.
- Composing a new mail, reply, and forward open in a separate window, not inline in the main window.
- The boundary between mail list and preview is draggable (resizable splitter).
- Unread count is shown only on the Inbox folder button.
- HTML/plain-text switch: a checkbox in the top bar.

## 5. Confirmed Features (first release)

All features below are in the first release (no phasing), including the local mail cache (section 8).

- Standard folders: Inbox, Outbox, Sent, Trash, Drafts, Spam, each with the context (right-click) menu items needed for it.
- Address book.
- Filter/sorting rules.
- Block list.
- Attachment by drag and drop.
- HTML mail display plus plain-text mode.
- Mail content preview, including images.
- Search.
- Compose with To, CC, BCC.
- Reply / reply all / forward (open in a separate window, same as compose).
- Attachment save / open.
- Text-size setting (large by default, changeable).
- Read/unread display (unread shown in bold in the mail list).
- Safety warnings for suspicious links and executable attachments.
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
- HTML display: WebView2.
- App password storage: Windows Credential Manager.
- LLM features: none.
- Local storage: mail is also stored locally on the PC (local mail cache) in SQLite, in addition to settings (account names, filter rules, address book, block list, window/splitter state).
- Local data protection: no app-level encryption. Data is protected only by the Windows user account; BitLocker and any other encryption setup are not needed.
- Privacy: mail content stays on the PC; no transmission to third parties.
- Delete: moves mail to Gmail Trash only. Permanent deletion and "Empty trash" are not provided in the app.
- Local copy mirrors Gmail state: moves to Gmail Trash are reflected locally; a mail permanently gone from Gmail is also deleted from the local copy.
- Sync:
  - First sync downloads headers for all mail and bodies for the most recent 12 months.
  - Older bodies and all attachments are downloaded on demand when opened, then cached locally (SQLite).
  - Periodic sync checks only new/changed mail (IMAP UID/UIDVALIDITY), not the whole mailbox.
  - Optional: background backfill of older bodies when idle.
- Fetch timing: on app startup, then automatically every 5 minutes, plus a manual refresh button.
- Offline / Gmail unreachable: show cached mail with a plain-language banner; outgoing mail is held in Outbox and sent automatically after reconnect.
- HTML display default: plain-text mode; the top-bar checkbox switches to HTML. If a mail has no text part (HTML only), the plain text derived from the HTML is shown instead of an empty pane.
- Remote images in HTML mail: not loaded by default; a "Show images" action loads them on demand. Images embedded in the mail (cid/inline) display normally.
- Offline with unfetched older body/attachment: shows a plain-language "not downloaded yet" message; fetched and shown automatically once online; other cached mail stays readable.
- Default text size: 18pt equivalent, changeable in settings.
- Minimum window size: 1024x700.
