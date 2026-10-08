# compose PLAN-1: window, fields, autocomplete, attachments, send entry

Planning status: complete. Spec: detailed-spec 7, 5; A15, A19-A21, A42, A56, A69, A74; Q65, Q73; PQ18, PQ19; D13-D17. Layout: `window-layouts.md` section 10. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Attachment size check
4. Functions
5. Tests and done criteria

## 1. Scope

Separate compose window, recipient fields with autocomplete, attachments (drag and drop, button, size and executable checks), Send action hand-off to `outbox-send`. Drafts, auto-save and close dialog are PLAN-2.

## 2. Behavior

- `ComposeWindow` (non-modal, multiple allowed), opened by the compose button, reply/forward (reply-forward), draft Edit (folder-actions), or `mailto:` (To prefilled). Remembers `AccountId` = account displayed when opened (A21); title [CP-01/WD-20] 「新しいメール - <account display name>」 (reply [WD-21] 「返信 - <name>」, forward [WD-22] 「転送 - <name>」). Text size follows the settings (not preview zoom).
- Fields [CP-02]: 宛先 (To), CC, BCC (hidden until the user clicks [CP-03] 「BCCを追加」, D14), 件名, 本文 (plain text `TextBox`, multi-line, AcceptsReturn, spell-check off), attachment list (label [CP-17] 「添付:」), buttons [CP-04] 添付 (bottom left) and 送信 (large, bottom right, the only primary action).
- Window rules (A56): size 960 x 720 DIP at 18 pt, non-modal, several may be open. Layout top to bottom: 宛先, CC, BCCを追加 (BCC row appears there after the click), 件名, 本文 (grows), attachment chips (name, size, 削除), error/over-limit line, bottom row 添付 ... 送信. Tab order: 宛先, CC, BCCを追加 (then BCC when shown), 件名, 本文, each attachment 削除, 添付, 送信; Tab inside 本文 leaves the field (not inserted as text). Initial focus = 宛先 for a new mail and for a forward; for a reply the cursor goes to 本文 above the quote. Enter in a single-line field moves to the next field (never sends); NO keyboard shortcut for Send; Esc is not bound (closing goes through the close dialog, compose PLAN-2). Drag and drop of files onto any part of the window adds attachments. File picker title [CP-16] 「ファイルを選ぶ」 [WD-28].
- Recipient entry: tokenized text box; separators `,` `;` `、` space, Enter; each token validated (`MailboxAddress.TryParse`); invalid tokens are marked red and block sending with the error line [CP-05] 「宛先のアドレスを確認してください」. At least one recipient in To/CC/BCC is required (error line [CP-06] 「宛先を入れてください。」).
- Autocomplete (D17, A19): after 1 typed character show up to 8 candidates from the account's address book (name, reading, emails; matched on name/reading/address, case+width-normalized) and previously used recipients (To/CC/BCC addresses of that account's Sent messages in cache, most recent first, distinct). Selecting inserts the address (with name as `Name <addr>` for contacts).
- Attachments: drag-and-drop onto the window plus 「添付」 button (`OpenFileDialog`, multiselect). For each file: (1) `ExecutableChecker.CheckFile` -> flagged = modal notice [SW-06] 「このファイルは危険な可能性があるため、添付できません。」 (title 「お知らせ」, button 「OK」) and not added; (2) size check (section 3) -> exceeded = modal notice [CP-07] 「ファイルが大きすぎます」 plus second line [CP-08] with the amount over (raw-file terms, see section 3), not added; (3) otherwise copied into the compose working folder and listed with a 「削除」 button [CP-04] (D16). Duplicate file paths are allowed.
- Send (D13): validate recipients; if subject empty show confirm dialog [WD-24] (title 「件名が空です」, text [CP-10] 「件名が空です。このまま送りますか?」, buttons [CP-11] このまま送る / 戻る* ; default 戻る, Enter = 戻る) (A15); then call `IOutboxService.EnqueueAndSendAsync(draftState)` and close the compose window immediately (no confirmation, no undo, Q21). The window never waits for SMTP.
- From = the account's address only, no display name (PQ18). Body is `text/plain; charset=utf-8` (PQ19), format=flowed off.

## 3. Attachment size check (PQ11, A42)

`EncodedSizeEstimator.Estimate(files, bodyBytes)` = sum over files of `ceil(n/3) * 4 * 78/76` + `bodyBytes` (UTF-8) + `HeaderOverheadBytes` (10,240). Allowed when `Estimate <= 25,000,000`. The check runs when adding a file (including the new file); body growth is not rechecked live. It is rechecked at Send time: if over, Send is refused with modal notice [CP-09] 「メールが大きすぎます。添付ファイルを減らしてください。」 plus the over-amount line [CP-08]. The amount is shown in raw-file terms (A69): `overRawMb = ceil(((Estimate - 25,000,000) / 1.37) / 100,000) / 10` (encoded overage divided by 1.37, rounded UP to 0.1 MB), text 「あと約X.XMB減らしてください」, e.g. 「あと約2.3MB減らしてください」.

## 4. Functions

- `ComposeViewModel { To, Cc, Bcc, Subject, Body, Attachments, ShowBcc, SendCommand, AttachCommand, Open(ComposeInit init) }`, `ComposeInit { AccountId, To, Cc, Subject, Body, Attachments, SourceDraftId?, ReplyHeaders? }`.
- `RecipientParser.Parse(string)`, `RecipientSuggester.Suggest(accountId, prefix)`.
- `EncodedSizeEstimator`.
- `IComposeWindowFactory.Open(ComposeInit)` (used by other features), `ComposeWindowRegistry` (tracks open windows for quit handling).

## 5. Tests and done criteria

- Unit: recipient parsing (Japanese display names, separators), suggester ranking and dedupe, estimator (boundary at 25,000,000; 18 MB raw file is under, ~19 MB over), executable block, empty-subject flow.
- Manual: drag and drop of a Japanese-named file; over-limit file refused with the amount; Send closes the window immediately and the item appears in Outbox.
