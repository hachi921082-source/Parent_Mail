# address-book

Planning status: complete. Spec: detailed-spec 10; A23; Q27, Q28; D17-D19.

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Per-account address book: repository (settings.db tables `contacts`, `contact_emails`), list window, add/edit/delete dialog, lookup APIs for autocomplete and the not-in-book notice.

## 2. Behavior

- Opened by the list-pane アドレス帳 button: modal-less window titled with the account display name; list sorted by reading (hiragana-normalized ordinal compare: katakana converted to hiragana, width-normalized; entries with empty reading sort by name after those with reading). Columns: name, reading, first email, memo (truncated).
- Buttons: 追加, 編集, 削除 (delete asks 「この人を削除しますか?」 with 「削除」/「やめる」, default やめる). Edit dialog fields: name (required), reading (hiragana; optional but recommended hint 「並べ替えに使います」), emails (one or more, add/remove rows, validated), memo.
- Duplicate rule (P2): the same email address (case-insensitive) cannot belong to two contacts in one account; saving shows 「このアドレスはすでに登録されています」.
- Account-scoped: switching account reloads the list; data is deleted with the account (settings.db FK cascade).
- Lookup: `IsKnown(accountId, address)` (case-insensitive) for the preview notice; `Search(accountId, prefix)` for autocomplete; `AddFromSender(accountId, name, address)` for folder-actions (opens the dialog prefilled).

## 3. Functions

- `IContactRepository { GetAll(accountId); Add; Update; Delete; FindByEmail(accountId, email); Search(accountId, prefix) }`.
- `KanaNormalizer.ToHiraganaKey(string)`, `ContactSorter`.
- `AddressBookViewModel`, `ContactEditViewModel` (validation).

## 4. Tests and done criteria

- Unit: sorting with mixed kana/kanji/empty reading, duplicate rejection, search by name/reading/email with width normalization, cascade delete with the account.
- Manual: add Japanese names; autocomplete in compose uses them; notice disappears for known senders.
