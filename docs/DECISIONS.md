# Decisions

<!-- Background, comparative analysis, and rejected alternatives for spec.md decisions. -->

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
