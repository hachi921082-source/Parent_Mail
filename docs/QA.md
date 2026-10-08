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

- Q38 New feature "mail enlarged display" (preview zoom), adopted by the user. Rule: zoom returns to the default when the application exits. Interpretation by designer-lite (to be confirmed): the zoom level is not persisted and each launch starts at the default (100% = the text-size setting value). Reflected in spec.md sections 4 and 5; details and assumptions A33-A37 in detailed-spec.md (controls, range 100%-300% in 25% steps, preview-only, session-wide while switching mails).

- Q39 HTML/plain-text checkbox applies to the currently displayed mail only; opening another mail resets it to plain text (HTML-only mails keep the derived-text fallback). Reflected in spec.md sections 4 and 8.
- Q40 Delete (button or menu): no confirmation dialog; non-modal notice "ごみ箱に移動しました [元に戻す]" for a few seconds (5 s, assumption A38); Undo returns the mail to its previous folder. Reflected in spec.md section 5.
- Q41 Attaching an executable file (same list as receive side, archives one level deep) is warned about and blocked. Reflected in spec.md section 5.
- Q13 re-confirmed: other-account indicator stays a small unread badge (no change).
- Also recorded: Outbox is a local-only concept and delete is an IMAP move to Gmail Trash (spec.md section 8); at most one sync per account at a time (A39); send failure also visible on the Outbox top-bar button (A40). Both are assumptions in detailed-spec.md.

- Q42 Review follow-ups (second external review). (1) Text size vs zoom: no app CSS font-size injected into WebView2; HTML mode uses ZoomFactor = text-size setting / 18pt x temporary zoom; plain text uses real font size x temporary zoom (formula = assumption A41). (2) Attachment limit: check uses estimated encoded message size (25 MB, about 18 MB raw), plain-language 「ファイルが大きすぎます」, attachment refused (threshold computation = assumption A42). (3) Folder view rule: a view shows messages whose membership includes that folder; messages also in Trash/Spam appear only there; All-Mail-only messages appear in no view but are searchable; message table separate from folder-membership mapping. Reflected in spec.md section 8 (note 1) and detailed-spec.md sections 3, 6, 7.

- Q43 Self-review follow-ups. User decision: quitting with unsent Outbox mail shows the warning 「送信待ちのメールがあります。このまま閉じると送信されません」 with 「送信してから閉じる」/「このまま閉じる」/「戻る」(default); failed send-then-close keeps the app open with the failure notice. Reflected in spec.md section 8 and detailed-spec.md sections 5, 16. Assumptions added (user can veto): A43 no block/filter on initial sync; A44 other-account badge shows unread count; A45 Japanese date format; A46 successful send deletes the Gmail Drafts draft; A47 search button focuses the always-visible inline box; A48 shutdown interception where feasible; A31 tightened (first-launch account-add has no Q31 warning).

- Q44 (2026-10-08, PLAN creation) Date format in list rows: today `HH:mm`; this year `M月d日 H:mm`; older `yyyy年M月d日 H:mm`; local time. Supersedes A45. Reflected in spec.md section 4 and detailed-spec.md sections 1, 19.
- Q45 (2026-10-08) HTML ZoomFactor = text-size setting (pt) / 12 x preview zoom, clamp 0.25-5.0, round to 2 decimals (1.5 at the default 18pt so HTML text matches plain text); WebView2 built-in zoom shortcuts disabled. Replaces the "/ 18pt" wording of Q42 (1) and assumption A41. Reflected in spec.md section 8, detailed-spec.md section 6.
- Q46 (2026-10-08) Preview zoom resets to 100% whenever the displayed mail, folder or account changes; never persisted. User confirmed A37 and replaced A36 ("persists while switching") and the "stays within the session" part of Q38. Reflected in spec.md section 5, detailed-spec.md section 6.
- Q47 (2026-10-08) Compose and search: compose body is plain text only (text/plain UTF-8); From header = address only (display name never sent); search = SQLite `LIKE` substring normalizing case and full/half width only, no hiragana/katakana folding (supersedes A28); single instance, a second launch brings the existing window to front (new).
- Q48 (2026-10-08) Matching: a block-list domain entry also covers its subdomains; filter "sender contains" matches address and display name; both case-insensitive. Extends Q16/Q17.
- Q49 (2026-10-08) Links: shortener list is a code constant (about 20 hosts, exact host or subdomain); look-alike = `xn--` label, any non-ASCII host, or mixed-script label (settles A29); the red banner judges both HTML and text parts; non-suspicious links open in the default browser with no dialog; HTML navigation always intercepted and opened externally; `mailto:` opens compose. Extends Q23/Q24.
- Q50 (2026-10-08) Sizes: font size is in true points, WPF FontSize = pt x 96/72; attachment limit: estimated encoded size = sum(ceil(n/3) x 4 x 78/76) + body bytes + 10,240 bytes header <= 25,000,000 bytes, and the message shows how far over (settles A42).
- Q51 (2026-10-08) Executable list extended: hta, com, pif, reg, wsf, vbe, jse, msc, dll, cpl, iso, img, vhd, vhdx, appx, msix, wsh, wsc, ps1xml, psm1, scf, inf, gadget, application, msp, mst, sct, chm, hlp, ade, adp, mde (plus Q25 and A30 lists); only zip is inspected, one level; an unreadable or encrypted zip is treated as suspicious; match on last extension, case-insensitive, trailing spaces/dots stripped; right-to-left override names flagged. Extends Q25/Q41. UIDVALIDITY change: only the folder's UID mappings are dropped and re-resolved via X-GM-MSGID; bodies and attachments kept; re-listed mail is not new mail. Supersedes A10.
- Q52 (2026-10-08) Sync and shutdown: initial sync shows the banner 「メールを読み込んでいます…」 and fills the list progressively without blocking the UI (new); after 3 send failures retries continue every cycle with no upper limit, the notice is shown once and the marker stays (clarifies Q22); Windows shutdown/logoff is intercepted (SessionEnding) with the Q43 3-button dialog (settles A48).
- Q53 (2026-10-08) Accounts and build: adding an account tests IMAP and SMTP login first and rejects a duplicate address; a local log file is adopted (`%LOCALAPPDATA%\Parent_Mail\logs\parentmail.log`, no mail content, addresses, subjects, attachment names, passwords or URLs; 1 MB x 3 files), reversing the earlier "no error log" idea; distribution is a self-contained single-file exe `Parent_Mail_YYYYMMDD-HHMMSS.exe`, no installer; stack net10.0-windows, App + Core (incl. Data) + Tests, CommunityToolkit.Mvvm + Microsoft DI, Microsoft.Data.Sqlite, HtmlAgilityPack, IMAP/SMTP timeout 30 s.
- Q54 (2026-10-08) Forward: original attachments are fetched and the same executable and size checks apply; violators are dropped with a notice; if not downloaded while offline, a notice is shown and the forward continues without them. Settles A22 for attachments.
- Q55 (2026-10-08) Offline behavior: moves (delete, Move to Spam, Not spam, Restore, Undo) are refused with 「今はインターネットにつながっていないため操作できません。つながってからもう一度お試しください。」; Mark read/unread is queued. Undo (Q40) restores only the folder the mail was viewed in; other Gmail labels are not restored (user confirmed; narrows the earlier "previous folder" wording). Restore from Trash always goes to the Inbox.
- Q56 (2026-10-08) Planning findings, confirmed: Spam header buttons are the Not spam / Delete menu items and Reply/Forward are disabled there; an Outbox row shows a read-only preview and has no read/unread state; draft autosave replaces the Gmail draft (APPEND new, then delete old); switching accounts during a sync does not cancel it.
- Q57 (2026-10-08) Wording approved (ui-strings.md section 17): 新しいメール (compose button and window title), 受信拒否リスト (block list), 受信拒否に追加 (menu), 振り分けルール (filter rules), 100%に戻す (zoom reset; zoom buttons 拡大 / 縮小), 受信トレイに戻す (Trash restore), アドレス帳に登録されていません (notice), ふりがな (address-book reading field), account switch button shows the name only, Outbox send-now failure 「まだ送れませんでした。しばらくしてからもう一度お試しください。」. Resolves the A2 wording gaps.

## Open

None. All questions are answered. The 21 assumed defaults below (D1-D21) were approved by the user as-is and are incorporated in detailed-spec.md (referenced there as D#). Other assumptions (A1-A48) are listed in detailed-spec.md section 19 and remain open to user veto; A10, A28, A36, A41, A45 are superseded and A29, A37, A42, A48 settled by Q44-Q57 (2026-10-08).

### Approved assumed defaults (D1-D21)

1. UI language: Japanese only. Error/notice tone: polite plain Japanese, no technical terms, one sentence on what to do next; settings problems say "ask a knowledgeable person".
2. Folder mapping: INBOX, [Gmail]/Sent Mail, [Gmail]/Drafts, [Gmail]/Trash, [Gmail]/Spam via special-use flags.
3. Outbox is local-only (Gmail has none). Sent mail is not APPENDed manually; Gmail saves it to Sent after SMTP.
4. Separate cache (SQLite) and sync per account.
5. Startup opens the last-used account's Inbox.
6. Auto-start with Windows: off, no setting.
7. Wrong-password and offline banners per spec sections 3 and 8.
8. Inbox context menu: Reply, Reply all, Forward, Mark read/unread, Move to Spam, Delete, Add sender to address book, Block sender.
9. Other context menus: Sent: Forward, Delete. Drafts: Edit, Delete. Spam: Not spam (to Inbox), Delete. Trash: Restore (to Inbox) only (no permanent delete). Outbox: Send now, Return to drafts.
10. Filter rules: apply to new incoming mail only, top-to-bottom, first match stops.
11. Filter rules are edited in a simple list dialog with enable/disable.
12. Filter body conditions: rules run after the new mail's body is fetched; HTML-only mails matched on derived plain text; undownloaded old bodies never match; rules never trigger old-body fetches.
13. Send: empty-subject warning stays; no other confirmation; compose window closes after Send.
14. Reply quotes the original; BCC hidden until requested.
15. Draft auto-save every 30 s while editing.
16. Attachments: drag-and-drop onto compose window plus an "Attach" button; Gmail's 25 MB limit (check refined by Q42); warn when exceeded and refuse; each attachment removable.
17. Recipient autocomplete: displayed account's address book plus that account's previously used recipients.
18. "Not in address book" notice uses the displayed account's address book.
19. "Add sender to address book" adds to the displayed account's address book.
20. Search: inline box at top of the list pane, results replace the list, "Clear" button returns.
21. Text size: range 12-48pt, live sample in settings, applies everywhere.
