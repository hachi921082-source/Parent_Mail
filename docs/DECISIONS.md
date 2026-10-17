# Decisions

<!-- Background, comparative analysis, and rejected alternatives for spec.md decisions. -->

## Index

1. Top-bar folder layout (spec.md section 4)
2. Action buttons at the bottom of the mail list (spec.md section 4)
3. No in-app app-password guide (spec.md section 3)
4. Technical stack and local mail storage (spec.md section 8)
5. Gmail app password (spec.md sections 1 and 3)
6. Local data protection (spec.md section 8)
7. Default display mode = plain text (spec.md section 8)
8. HTML ZoomFactor divides by 12, not 18 (spec.md section 8; 2026-10-08)
9. Preview zoom resets per mail (spec.md section 5; 2026-10-08)
10. Local log file adopted (spec.md section 8; 2026-10-08)
11. Extended executable list and zip handling (spec.md section 5; 2026-10-08)
12. Delete Undo restores only the viewed folder (spec.md section 5; 2026-10-08)
13. Multi-select delete with an always-visible row checkbox (spec.md section 5; 2026-10-08)
14. Chrome text cap (spec.md section 4; 2026-10-08)
15. Modal OK for failures, transient notices for successes (spec.md section 5; 2026-10-08)
16. Idle backfill included; mark-read only in Inbox and Spam (spec.md sections 5, 8; 2026-10-08)
17. Address-book picker in compose: one button per recipient field, no search box (detailed-spec section 7; 2026-10-11)
18. Data folder next to the exe in the Release build (spec.md section 8; 2026-10-17)

## Top-bar folder layout (spec.md section 4)

- Decision: folders and account switch sit in a top bar; below it a narrow mail list and a very wide preview pane.
- Rationale: the users (parents) have poor eyesight. A standard 3-pane layout (folders / list / preview) would make the preview pane too small to read comfortably, so folders were moved to the top to maximize preview width.

## Action buttons at the bottom of the mail list (spec.md section 4)

- Decision: compose, search, settings, address book, block list, filter rules sit at the bottom of the mail list pane.
- Rationale: parents receive few mails, so the list stays short and the bottom buttons remain easy to reach.

## No in-app app-password guide (spec.md section 3)

- Decision: only state on the account-add screen that an app password is required.
- Rationale: a knowledgeable person sets up the accounts for the parents.

## Technical stack and local mail storage (spec.md section 8)

- Decision: C# + WPF, MailKit, WebView2, Windows Credential Manager, no LLM; mail also stored locally; delete = move to Gmail Trash only; no third-party transmission.
- Rationale: user's choice. Designer's earlier proposal was "fetch each time, store settings only"; the user chose to keep mail locally as well instead. No further reason was stated.
- Consequence: the proposed "offline cache" feature is replaced by the local mail cache.

## Gmail app password (spec.md sections 1 and 3)

- Decision: IMAP/SMTP with Gmail app password; Gmail only; maximum 5 accounts, each renamable (raised from 2 on 2026-10-16, Issue #8; QA.md Q92).
- Rationale: user's choice. (Note: app passwords require Google 2-step verification to be enabled on the account.)

## Local data protection (spec.md section 8)

- Decision: no app-level encryption; BitLocker not needed.
- Known consequence: the local mail database (SQLite) is readable by anyone with access to the Windows account or the disk. Accepted trade-off; no further rationale given.

## Default display mode = plain text (spec.md section 8)

- Decision: mails display as plain text by default; HTML is opt-in via the top-bar checkbox.
- Rationale: user raised safety as the criterion ("is text the safer default?") and chose text. Supporting points (from the assistant, not the user): real link targets are visible in text; no hidden tracking; text is larger and easier to read.

## HTML ZoomFactor divides by 12, not 18 (spec.md section 8; 2026-10-08)

- Decision: ZoomFactor = text-size pt / 12 x preview zoom (clamp 0.25-5.0, 2 decimals).
- Rationale (assistant summary): WebView2 renders HTML at 100% with roughly 12pt-equivalent body text, so dividing by 18 would make HTML mail look smaller than the plain-text view at the same setting. Dividing by 12 gives 1.5 at the default 18pt and matches plain text. Replaces the earlier "/ 18pt" wording (Q42, A41).

## Preview zoom resets per mail (spec.md section 5; 2026-10-08)

- Decision: zoom returns to 100% when the displayed mail, folder or account changes; never persisted.
- Rationale: user decision; reason below is the assistant's summary. A zoom left on from one mail would silently affect the next one, and the parents may not know how to undo it. Replaces A36 (zoom persists within a session); confirms A37 (not persisted).

## Local log file adopted (spec.md section 8; 2026-10-08)

- Decision: a small local log file is kept (no mail content, addresses, subjects, attachment names, passwords or URLs; 1 MB x 3 files).
- Rationale: user decision during planning; reason below is the assistant's summary. Without any log, problems the parents cannot describe cannot be diagnosed by the knowledgeable helper. The content rules keep the privacy policy (no mail data outside the cache DB). Reverses the earlier "error log not adopted" note in HANDOFF.

## Extended executable list and zip handling (spec.md section 5; 2026-10-08)

- Decision: list extended with hta, com, pif, reg, wsf, vbe, jse, msc, dll, cpl, iso, img and other script/installer/disk-image types; only zip is inspected (one level); unreadable or encrypted zip is suspicious; right-to-left-override names are flagged.
- Rationale (assistant summary): the original list missed common Windows script and disk-image launch vectors; an encrypted zip cannot be inspected, so treating it as suspicious is the safe choice for these users. Other archive formats are not opened by the app, so they are not inspected.

## Delete Undo restores only the viewed folder (spec.md section 5; 2026-10-08)

- Decision: Undo moves the mail back to the folder it was viewed in; other Gmail labels are not restored.
- Rationale (assistant summary): user confirmed. The app shows folders, not labels; restoring the full label set would need extra label bookkeeping with no visible benefit. Narrows the Q27 planning idea of "previous folder/label set". Moves are refused offline, so Undo never needs queuing.

## Multi-select delete with an always-visible row checkbox (spec.md section 5; 2026-10-08)

- Decision: every list row has a checkbox at all times; the Delete button deletes all checked mails; one undo notice restores them all.
- Rationale: user decision. Reason below is the assistant's summary: parents who get bulk advertising mail would otherwise delete one mail at a time; an always-visible checkbox needs no "selection mode" to learn. Alternatives not chosen (assistant summary): Ctrl/Shift click multi-select (invisible to users unfamiliar with PCs) and a separate "selection mode" toggle (an extra step and state to explain).
- Details (header Delete button, preview, select-all) were approved as proposed on 2026-10-08 (detailed-spec section 22).

## Chrome text cap (spec.md section 4; 2026-10-08)

- Decision: the text of the top bar and of the list-pane bottom button strip is capped; list, preview and mail text still follow the 48pt maximum. User suggested 28pt.
- Rationale (assistant measurement, not a user statement): at the 1024-wide minimum window the six folder buttons need about 1,140 DIP at 28pt and 900 DIP at 20pt, against about 960 available, so 28pt would overflow. 20pt is proposed. Capping was preferred over wrapping folder labels because wrapping would take list height at the 700-high minimum. The user approved 20pt and the two-row top bar on 2026-10-08, noting that the parents' Full HD displays use display scaling (effective width about 1280 DIP).

## Modal OK for failures, transient notices for successes (spec.md section 5; 2026-10-08)

- Decision: failures/refusals the user must read use a modal dialog with one big 「OK」 button; successes use a transient notice. Delete undo notice lasts 10 seconds. At most 2 banners at a time; only the Outbox failure banner can be closed.
- Rationale: user decision. Assistant summary of why 10 seconds: parents react slowly and a 5-second window is easy to miss. The priority order (wrong-password > offline > Outbox failure > initial sync > others) and the rule that modal dialogs are only for actions the user just performed (automatic problems use banners) are designer-lite proposals.

## Idle backfill included; mark-read only in Inbox and Spam (spec.md sections 5, 8; 2026-10-08)

- Decision: background backfill of older bodies while idle is included (was optional). Opening a mail marks it read immediately only in Inbox and Spam; Ctrl+0 / Ctrl+plus / Ctrl+minus zoom shortcuts are removed (Ctrl+mouse wheel stays).
- Rationale: user decision, no further reason stated. Assistant note: because mail selected automatically after a delete counts as opened, the next mail can be marked read without the parent reading it; the user changed this on 2026-10-08: auto-selection is visual only and does not mark the mail read (A58, Q74).

## Compose close dialog default = save draft (detailed-spec section 21; 2026-10-08)

- Decision: in the compose close dialog (下書きに保存 / 保存しないで閉じる(...) / 戻る) the default button is 下書きに保存.
- Rationale: user decision; reason is the assistant's summary: saving loses nothing and the parent most likely wants the text kept, whereas 戻る would force another step. Exception to the general rule that Enter hits the cancel-type button.

## Address-book picker in compose: one button per recipient field, no search box (detailed-spec section 7; 2026-10-11)

- Decision (Q80-Q85): a button 「アドレス帳から選ぶ」 beside each of 宛先 / CC / BCC opens a modal multi-select picker (one row per email address, checkboxes, no search box) of the compose window's own account's address book. Picked addresses are appended as `Name <address>`; addresses already in the field are shown checked and disabled with 「入力ずみ」. Implemented by compose PLAN-3 after PLAN-2.
- Why a button beside each field: the result is predictable (the button next to a field fills that field); nothing depends on which field happens to have focus, and there is no extra "which field?" question for the parent. Rejected: one button adding to the focused field (hidden state, easy to fill the wrong field); one button that asks 宛先 / CC / BCC (extra step and dialog).
- Why no search box: the parents' address books are small, a sorted list with big checkboxes is enough, and fewer controls is simpler. Revisit if books grow.
- Why the compose window's account (not the displayed one): consistent with A21 (the compose window sends from the account displayed when it opened) and with the per-account address book (Q28).
- Why a separate PLAN-3 after PLAN-2 in the same lane: both edit the compose window XAML and ViewModel, so parallel worktrees would conflict; folding it into PLAN-2 would mix drafts with recipient entry.

## Data folder next to the exe in the Release build (spec.md section 8; 2026-10-17)

- Decision (Q93, Issue #9): Release exe keeps all data in a `Data` folder next to the exe, created on first launch; Debug build keeps the old rule (`Data` next to the exe if present, else `%LOCALAPPDATA%\Parent_Mail`); `PARENTMAIL_DATA_ROOT` wins in both. `Data` holds everything except the app password, which stays in Windows Credential Manager.
- Why: the parents' PC may have the exe on a non-OS drive. If Windows crashes or is reinstalled, `%LOCALAPPDATA%` is lost with it, but a `Data` folder on the other drive survives, so the mail cache, settings, address book and rules stay. No manual folder creation is needed on the parents' PC.
- Why the password stays in Credential Manager (user decision): it is already implemented, and a password file in `Data` would be readable by anyone who can open the folder and would travel with every copy of it. After an OS reinstall the existing password re-entry flow is used per account; the mail data stays.
- Why by build kind: on the developer PC, Debug runs and checks keep using the current data without any copying.
- Assistant-decided details (A92-A98, user may veto): silent fallback to `%LOCALAPPDATA%\Parent_Mail` with a log line if `Data` is not writable (a dialog would only confuse the parents, and the app stays usable); no automatic migration (a silent copy of mail data is risky and the developer PC is the only place with old data; manual copy is documented); folder name exactly `Data`.
- Rejected: putting data directly in the exe folder without a subfolder (mixes data with exe files); automatic migration from `%LOCALAPPDATA%`; a startup dialog on fallback.
