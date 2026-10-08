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

- Decision: IMAP/SMTP with Gmail app password; Gmail only; maximum 2 accounts, each renamable.
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
