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

## Open

None.
