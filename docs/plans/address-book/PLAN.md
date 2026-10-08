# address-book

Planning status: complete. Spec: detailed-spec 10; A23, A56; Q27, Q28, Q65, Q70; D17-D19. Layout: `window-layouts.md` section 7. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Per-account address book: repository (settings.db tables `contacts`, `contact_emails`), list window, add/edit/delete dialog, lookup APIs for autocomplete and the not-in-book notice.

## 2. Behavior

- Opened by the list-pane アドレス帳 button: MODAL window [WD-11] (900 x 600 DIP at 18 pt, centered on the main window) titled [AB-01] 「アドレス帳 - <account display name>」; list sorted by reading (hiragana-normalized ordinal compare: katakana converted to hiragana, width-normalized; entries with empty reading sort by name after those with reading). Columns (labels) [AB-02]: 名前, ふりがな, メールアドレス, メモ. Empty list text [AB-13] 「まだ登録されていません」. Window buttons [AB-03] 追加 / 編集 / 削除 / 閉じる. Tab order: list, 追加, 編集, 削除, 閉じる; initial focus = the list; Enter on a row = 編集; Esc = 閉じる.
- Buttons: 追加, 編集, 削除 (delete asks confirm dialog [WD-13] (title 「人の削除」) with text [AB-04] 「この人を削除しますか?」 and buttons 削除 / やめる*, default やめる). Edit dialog [WD-12] (MODAL over the book; titles [AB-08] 「人を追加」/「人を編集」; buttons [AB-12] 保存* / やめる; Enter (not in メモ) = 保存, Esc = やめる; initial focus = 名前). Field order = tab order: 名前, ふりがな, メールアドレス rows, アドレスを増やす, メモ (multi-line), 保存, やめる; address-row buttons [AB-11] 「アドレスを増やす」 / 「この行をはずす」. Error lines (in place): empty name [AB-07]; bad address [AB-09]; no address [AB-10]. Fields: name (required), ふりがな (reading; hiragana; optional but recommended hint [AB-05] 「並べ替えに使います」), emails (one or more, add/remove rows, validated), memo.
- Duplicate rule (priority-2 default): the same email address (case-insensitive) cannot belong to two contacts in one account; saving shows the error line [AB-06] 「このアドレスはすでに登録されています」.
- Account-scoped: switching account reloads the list; data is deleted with the account (settings.db FK cascade).
- Lookup: `IsKnown(accountId, address)` (case-insensitive) for the preview notice; `Search(accountId, prefix)` for autocomplete; `AddFromSender(accountId, name, address)` for folder-actions (opens the dialog prefilled).

## 3. Functions

- `IContactRepository { GetAll(accountId); Add; Update; Delete; FindByEmail(accountId, email); Search(accountId, prefix) }`.
- `KanaNormalizer.ToHiraganaKey(string)`, `ContactSorter`.
- `AddressBookViewModel`, `ContactEditViewModel` (validation).

## 4. Tests and done criteria

- Unit: sorting with mixed kana/kanji/empty reading, duplicate rejection, search by name/reading/email with width normalization, cascade delete with the account.
- Manual: add Japanese names; autocomplete in compose uses them; notice disappears for known senders.
