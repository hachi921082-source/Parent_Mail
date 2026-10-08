# compose PLAN-1: window, fields, autocomplete, attachments, send entry

Planning status: complete. Spec: detailed-spec 7, 5; A15, A19-A21, A42; Q18, Q19; D13-D17.

## Index

1. Scope
2. Behavior
3. Attachment size check
4. Functions
5. Tests and done criteria

## 1. Scope

Separate compose window, recipient fields with autocomplete, attachments (drag and drop, button, size and executable checks), Send action hand-off to `outbox-send`. Drafts, auto-save and close dialog are PLAN-2.

## 2. Behavior

- `ComposeWindow` (non-modal, multiple allowed), opened by the compose button, reply/forward (reply-forward), draft Edit (folder-actions), or `mailto:` (To prefilled). Remembers `AccountId` = account displayed when opened (A21); title shows 「新しいメール - <account display name>」. Text size follows the settings (not preview zoom).
- Fields: 宛先 (To), CC, BCC (hidden until the user clicks 「BCCを追加」, D14), 件名, 本文 (plain text `TextBox`, multi-line, AcceptsReturn, spell-check off), attachment list, buttons 送信 (large) and 添付 (Attach).
- Recipient entry: tokenized text box; separators `,` `;` `、` space, Enter; each token validated (`MailboxAddress.TryParse`); invalid tokens are marked red and block sending with 「宛先のアドレスを確認してください」. At least one recipient in To/CC/BCC is required.
- Autocomplete (D17, A19): after 1 typed character show up to 8 candidates from the account's address book (name, reading, emails; matched on name/reading/address, case+width-normalized) and previously used recipients (To/CC/BCC addresses of that account's Sent messages in cache, most recent first, distinct). Selecting inserts the address (with name as `Name <addr>` for contacts).
- Attachments: drag-and-drop onto the window plus 「添付」 button (`OpenFileDialog`, multiselect). For each file: (1) `ExecutableChecker.CheckFile` -> flagged = dialog 「このファイルは危険な可能性があるため、添付できません。」 and not added; (2) size check (section 3) -> exceeded = 「ファイルが大きすぎます」 plus the amount over, not added; (3) otherwise copied into the compose working folder and listed with a 「削除」 button (D16). Duplicate file paths are allowed.
- Send (D13): validate recipients; if subject empty show dialog 「件名が空です。このまま送りますか?」 buttons 「このまま送る」 / 「戻る」 (A15, default 戻る); then call `IOutboxService.EnqueueAndSendAsync(draftState)` and close the compose window immediately (no confirmation, no undo, Q21). The window never waits for SMTP.
- From = the account's address only, no display name (Q18). Body is `text/plain; charset=utf-8` (Q19), format=flowed off.

## 3. Attachment size check (Q11, A42)

`EncodedSizeEstimator.Estimate(files, bodyBytes, subjectBytes)` = sum over files of `ceil(n/3) * 4 * 78/76` + `bodyBytes` (UTF-8) + `HeaderOverheadBytes` (10,240). Allowed when `Estimate <= 25,000,000`. The check runs when adding a file (including the new file); body growth is not rechecked live. It is rechecked at Send time: if over, Send is refused with 「メールが大きすぎます。添付ファイルを減らしてください。」 plus the over-amount. The message shows the amount over in MB (one decimal), e.g. 「あと約2.3MB減らしてください」.

## 4. Functions

- `ComposeViewModel { To, Cc, Bcc, Subject, Body, Attachments, ShowBcc, SendCommand, AttachCommand, Open(ComposeInit init) }`, `ComposeInit { AccountId, To, Cc, Subject, Body, Attachments, SourceDraftId?, ReplyHeaders? }`.
- `RecipientParser.Parse(string)`, `RecipientSuggester.Suggest(accountId, prefix)`.
- `EncodedSizeEstimator`.
- `IComposeWindowFactory.Open(ComposeInit)` (used by other features), `ComposeWindowRegistry` (tracks open windows for quit handling).

## 5. Tests and done criteria

- Unit: recipient parsing (Japanese display names, separators), suggester ranking and dedupe, estimator (boundary at 25,000,000; 18 MB raw file is under, ~19 MB over), executable block, empty-subject flow.
- Manual: drag and drop of a Japanese-named file; over-limit file refused with the amount; Send closes the window immediately and the item appears in Outbox.
