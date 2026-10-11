# compose PLAN-3: address-book picker for recipients

Planning status: complete (wording PENDING). Spec: detailed-spec 7 (address-book picker), 10, 21; A19-A21, A56; Q19, Q28, Q80-Q85. Layout: `window-layouts.md` section 10 and 10.1. Wording: `ui-strings.md` CP-18 to CP-22, WD-31 (PENDING until the user approves the Japanese text). Depends on compose PLAN-1 (merged) and address-book PLAN (merged); implemented in W4 in the compose lane AFTER compose PLAN-2 (both edit the compose window XAML and ViewModel; do not run them in parallel worktrees).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

A picker button beside each recipient field of the compose window and the picker dialog that lists the compose window's own account's address book. Autocomplete (PLAN-1) is unchanged.

## 2. Behavior

- Buttons: [CP-18] 「アドレス帳から選ぶ」 right of 宛先, CC and BCC (the BCC button appears with the BCC row after 「BCCを追加」). Height at least 56 DIP. Tab order: 宛先, its button, CC, its button, BCCを追加 (then BCC and its button when shown), 件名, 本文, attachment 削除 buttons, 添付, 送信. Enter in a single-line field still moves to the next FIELD's text box, not onto the button. No shortcut key. Esc stays unbound in compose.
- Account: the compose window's `AccountId` (A21), not the account displayed in the main window. Source: `IContactRepository.GetAll(accountId)` (address-book PLAN). Previously used recipients are not listed.
- Dialog [WD-31]: MODAL to the owning compose window only (other compose windows and the main window stay usable), centered on it, about 720 x 560 DIP at 18 pt, title 「アドレス帳から選ぶ - <account display name>」. Hint line [CP-19]. List sorted like the address book (ふりがな, kana-normalized; empty reading after, by name), ONE ROW PER EMAIL ADDRESS: checkbox, name, address; row height at least 56 DIP; Space or click toggles; multi-select; no search box. Buttons [ 入れる ] [ やめる ] right-aligned; Enter = 入れる, Esc = やめる; initial focus = the list; tab order: list, 入れる, やめる.
- Already entered: addresses already in THAT field (case-insensitive, parsed from its tokens) are shown checked and disabled with the tag [CP-21] 「入力ずみ」. The same address in 宛先 and CC is not blocked.
- 入れる with no newly checked row: error line [CP-20] 「入れたい人を選んでください。」, dialog stays open.
- Empty address book: the dialog is not opened; `IDialogService.ShowNotice` (title [WD-10] 「お知らせ」, text [CP-22], button 「OK」) is shown.
- Result: each newly checked address is appended to the end of the field as a token `Name <address>` (same format as autocomplete selection; name omitted if empty), in list order; existing text and tokens are untouched; duplicates against the field are impossible by construction. Focus returns to that field with the caret at the end.

## 3. Functions

- `AddressPickerViewModel { Rows (ContactAddressRow { Name, Address, IsChecked, AlreadyEntered }), HintText, ErrorText, ConfirmCommand }`; `AddressPickerDialog` (XAML), `IAddressPickerService.Pick(ownerWindow, accountId, existingAddresses)` returns the list of chosen `MailboxAddress`es or null on cancel (so ComposeViewModel can be unit tested with a fake).
- `ComposeViewModel` additions: `PickToCommand`, `PickCcCommand`, `PickBccCommand` (all call a shared `PickInto(field)`), plus `AppendRecipients(field, addresses)` reusing `RecipientParser`.

## 4. Tests and done criteria

- Unit: row building (one row per address, kana sort, empty reading last), AlreadyEntered detection (case-insensitive, `Name <addr>` tokens), 入れる with nothing newly checked sets the error and does not close, append keeps existing text and formats `Name <addr>`, empty address book path, account is the compose window's AccountId.
- Manual: with two accounts, open compose on one while the other is displayed and confirm the right address book; tab order with and without BCC; picker is modal only to its compose window; a person with two addresses; Japanese names; focus returns to the field.
