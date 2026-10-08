# Questions for the User

<!-- Unresolved questions requiring the user's own judgment, appended by designer-lite. -->

## Resolved

- Q1 Purpose: email client for the user's own parents to use Gmail on a PC. Reflected in spec.md section 2.
- Q2 Target users: family (elderly parents, low IT literacy). Reflected in spec.md section 2.
- Q3 Platform: Windows desktop app. Reflected in spec.md section 1.
- Q7 Meaning of "Parent": the user's own parents. Reflected in spec.md section 2.
- Gmail connection: IMAP/SMTP with app password. Reflected in spec.md section 1.
- Multiple accounts: max 2, renamable, switch button on top bar. Reflected in spec.md section 3.
- Q9 Action buttons at bottom of mail list; draggable splitter; unread count on Inbox button only. Reflected in spec.md section 4.
- Account switch button: obvious labeled button showing current account name. Reflected in spec.md section 3.
- New-mail compose opens in a separate window. Reflected in spec.md section 4.
- Q10 App password guidance: no in-app guide, only a notice on the account-add screen. Reflected in spec.md section 3.
- Q6 (part) Tech stack: C# + WPF, MailKit, WebView2, Windows Credential Manager, no LLM. Reflected in spec.md section 8.
- Q6 (part) Data storage: mail also stored locally (local cache). Reflected in spec.md section 8. Replaces the proposed "offline cache" in Q8.
- Q6 (part) Delete: move to Gmail Trash only; no permanent delete, no "Empty trash". Reflected in spec.md section 8.
- Q6 (part) Privacy: mail stays on the PC, no third-party transmission. Reflected in spec.md section 8.
- Q6 (part) Fetch timing: on startup and at a regular interval (value open, see Q6d). Reflected in spec.md section 8.
- Q4 First-release scope: all features in one release, no phasing, local cache included. Reflected in spec.md section 5.
- Q9 HTML/text checkbox: in the top bar. Reflected in spec.md section 4.
- Q9 Mail list rows: sender, subject, date/time, attachment icon, unread in bold (bold not explicitly confirmed). Reflected in spec.md section 4.
- Q8 Adopted: reply/reply all/forward (separate window), attachment save/open, text-size setting (large default), safety warnings, draft auto-save, read/unread display (not explicitly confirmed). Reflected in spec.md section 5.
- Q8 On hold: signature, print, star, extra contrast themes. Reflected in spec.md section 6.
- Q5 Non-goals: other mail services, 3+ accounts, smartphone version, sharing, calendar integration, AI features. Reflected in spec.md section 7.
- Q6a Local cache format: SQLite. Reflected in spec.md section 8.
- Q6c Local data protection: no app-level encryption; BitLocker and encryption setup are not needed. Reflected in spec.md section 8.

- Q6b Cache scope and sync: lazy sync (headers all, bodies 12 months eager, older bodies/attachments on demand, UID/UIDVALIDITY delta sync; idle backfill optional); local copy mirrors Gmail incl. deletion of permanently removed mail. Reflected in spec.md section 8.
- Q6d Fetch interval: startup + every 5 minutes + manual refresh. Reflected in spec.md section 8.
- Q6e Offline: cached mail + banner; outgoing mail held in Outbox and auto-sent after reconnect. Reflected in spec.md section 8.
- HTML display default: plain text, checkbox for HTML. Reflected in spec.md section 8.
- Remote images in HTML mail: not loaded by default, "Show images" loads on demand, inline images display normally. Reflected in spec.md section 8.
- HTML/text default: text (final), with HTML-only fallback to plain text derived from the HTML. Reflected in spec.md section 8.
- Offline unfetched body/attachment: plain-language "not downloaded yet" message, auto-fetch when online. Reflected in spec.md section 8.
- Default text size 18pt equivalent (changeable). Reflected in spec.md section 8.
- Minimum window size 1024x700. Reflected in spec.md section 8.

- Wrong/expired app password: plain-language notice asking a knowledgeable person to re-enter it, re-entry screen opens, cached mail stays viewable. Reflected in spec.md section 3. Background note (from memory, unverified; check Google's help page): app passwords have no time-based expiry but become invalid if revoked/deleted, if 2-step verification is turned off, or in some cases when the Google account password is changed or the account is flagged; the phone Gmail app uses OAuth, not an app password, so it is unaffected.

- Q11 Mail only in Gmail "All Mail" (archived) / custom labels: shown in no folder, found by search. To be carried in detailed-spec.md.
- Q12 Drafts: synced with Gmail Drafts. To be carried in detailed-spec.md.
- Q13 Non-displayed account with new mail: small unread badge on the account switch button. Reflected in spec.md section 4 (badge) and to be detailed in detailed-spec.md.
- Q14 New-mail notification for displayed account: in-app only (Inbox unread count changes); no toast, no sound. To be carried in detailed-spec.md.
- Q15 Block list: mail from a blocked sender is moved to Spam. To be carried in detailed-spec.md.
- Q16 Block list entry: email address or whole domain, chosen at registration. To be carried in detailed-spec.md.
- Q17 Filter conditions: sender contains / subject contains / body contains (user chose the broader option over the recommended sender+subject only). Implications (assumed defaults, veto if wrong): rules run after the new mail's body is fetched (new-mail bodies are always fetched); HTML-only mails are matched against the plain text derived from the HTML; for mail whose body is not downloaded (older than 12 months), body conditions do not match; rules never trigger a fetch of old bodies.
- Q18 Filter actions: move to Trash / move to Spam / mark as read (no custom folders). To be carried in detailed-spec.md.
- Q19 Recipient autocomplete (To/CC/BCC): address book plus previously used recipients. To be carried in detailed-spec.md.
- Q20 Closing compose window with unsent text: always confirm with large buttons "Save draft / Discard / Cancel" (user chose this over the recommended silent save). Draft auto-save (every 30 s) is kept; the dialog is the explicit choice. To be carried in detailed-spec.md.
- Q21 Send button: sends immediately, no confirmation dialog, no undo window (user chose this over the recommended confirmation). Assumed defaults (veto if wrong): the existing empty-subject warning stays; no other confirmation is added; after sending, the compose window closes and the mail goes through Outbox to Sent.
- Q22 Send failure: stays in Outbox, retried at each sync (5 min); after 3 failures a plain-language failure notice is shown; right-click "Send now" / "Return to drafts". To be carried in detailed-spec.md.
- Q23 Suspicious link = shown text differs from real target; IP-address URL; URL shortener; non-https; lookalike/punycode domain. To be carried in detailed-spec.md.
- Q24 Suspicious link UI: red banner at top of the mail plus confirmation dialog on clicking such a link (shows real destination, default button = Cancel). To be carried in detailed-spec.md.
- Q25 Executable attachments (exe, msi, bat, cmd, scr, js, vbs, ps1, jar, lnk, macro-enabled Office files; archives inspected one level deep): strong warning, opening forbidden, save allowed only after confirmation. To be carried in detailed-spec.md.
- Q26 Sender not in address book: notice shown in the preview header only (not in list rows). To be carried in detailed-spec.md.
- Q27 Address book fields: name, reading (yomigana, for sort), email address(es) (multiple allowed), memo. To be carried in detailed-spec.md.
- Q28 Address book is separate per account (user chose this over the recommended shared book). Consequences (assumed defaults, veto if wrong): recipient autocomplete (Q19) uses the displayed account's address book and that account's previously used recipients; the "not in address book" notice (Q26) checks the displayed account's address book; "Add sender to address book" adds to the displayed account. Block list / filter rule scope: see Q35 (open).
- Q29 Search scope: all folders of the displayed account; results show the folder name. To be carried in detailed-spec.md.
- Q30 Search matching: local cache only (sender, subject, downloaded bodies); unfetched older bodies excluded and the UI says so. To be carried in detailed-spec.md.
- Q31 Account settings (add/delete, password re-entry): protected by a warning dialog ("ask a knowledgeable person", default button = Cancel); no PIN. To be carried in detailed-spec.md.
- Q32 Text-size change: free numeric input or slider (user chose this over the recommended 4 big buttons); default stays 18pt. Assumed defaults (veto if wrong): range 12-48pt; live sample preview shown in the settings screen; applies to list, preview, compose and dialogs.
- Q33 Big buttons always shown at the top of the preview pane (Reply, Reply all, Forward, Delete) plus context menu. Reflected in spec.md section 4.
- Q34 Window close (X) quits the app. To be carried in detailed-spec.md.
- Q35 Block list and filter rules are per account, same as the address book. Reflected in spec.md section 5.

- Q36 Sync: [Gmail]/All Mail is synced too under the same rules; one message stored once (X-GM-MSGID); both accounts sync on the same timer. Reflected in spec.md section 8.
- Q37 Compose close dialog "Discard": an auto-saved draft in Gmail Drafts is KEPT (user chose this over deleting it); Discard only drops edits made after the last auto-save; if no auto-save has happened yet, nothing remains. The middle-button label must not promise deletion; exact Japanese wording is undecided (options in detailed-spec.md section 17).

## Open

Purpose: decisions needed before docs/detailed-spec.md can be written. Themes are ordered by dependency (A first). "Rec." = recommended option.

All themes answered. Remaining step: user vetoes assumed defaults below, then detailed-spec.md is written.

### Assumed defaults (veto if wrong)

- UI language: Japanese only. Error/notice tone: polite plain Japanese, no technical terms, one sentence on what to do next; settings problems say "ask a knowledgeable person".
- Outbox is local-only (Gmail has none). Sent mail is not APPENDed manually; Gmail saves it to Sent after SMTP.
- Folder mapping: INBOX, [Gmail]/Sent Mail, [Gmail]/Drafts, [Gmail]/Trash, [Gmail]/Spam via special-use flags.
- Separate cache (SQLite file/tables) and sync per account; startup opens last-used account's Inbox.
- Context menus: Inbox: Reply, Reply all, Forward, Mark read/unread, Move to Spam, Delete, Add sender to address book, Block sender. Sent: Forward, Delete. Drafts: Edit, Delete. Spam: Not spam (to Inbox), Delete. Trash: Restore (to Inbox) only (no permanent delete). Outbox: Send now, Return to drafts.
- Filter rules: apply to new incoming mail only, top-to-bottom, first match stops; edited in a simple list dialog with enable/disable.
- Search: inline box at top of the list pane, results replace the list, "Clear" button returns.
- Attachments: drag-and-drop onto compose window plus an "Attach" button; total limit 25 MB (Gmail), warn when exceeded and refuse; each attachment removable.
- Compose: empty-subject warning; reply quotes original; To/CC/BCC fields, BCC hidden until requested.
- Draft auto-save every 30 s while editing.
- Auto-start with Windows: off, no setting.
- Wrong-password and offline banners per spec sections 3 and 8.
- Filter body conditions: rules run after the new mail's body is fetched; HTML-only mails matched on derived plain text; undownloaded old bodies never match; rules never trigger old-body fetches.
- Send: empty-subject warning stays; no other confirmation; compose window closes after Send.
- Per-account scope (Q28/Q35): autocomplete, "not in address book" notice and "Add sender to address book" use the displayed account's address book; block list and filter rules per account.
- Text size: range 12-48pt, live sample in settings, applies everywhere.
