# block-list

Planning status: complete. Spec: detailed-spec 11; A24, A25, A43, A56; Q48, Q65; PQ24 (planning answer: domain includes subdomains). Layout: `window-layouts.md` section 8. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Per-account block entries (settings.db `block_entries`), management window, evaluation as the first `IIncomingRule` of the incoming-mail pipeline (sync-cache PLAN-2).

## 2. Behavior

- Entry kinds: `Address` (exact, case-insensitive) or `Domain` (matches the domain and any subdomain: `example.com` matches `a@example.com` and `a@mail.example.com`, not `a@notexample.com`). Domain input accepts `example.com` or `@example.com` (normalized to lowercase without `@`).
- Window [WD-14] opened by the 受信拒否リスト button: MODAL, 720 x 560 DIP at 18 pt, centered on the main window, title [BL-01] 「受信拒否リスト - <account display name>」. List of entries (columns [BL-02] 種類 / 値; kinds アドレス, ドメイン; empty text [BL-08] 「受信拒否はありません」). Add form under the list: kind radio [BL-06] 「このアドレスだけ」/「このドメイン全体」 (default address), value box (label 「アドレスまたはドメイン」), 追加, hint [BL-09] 「ドメインを入れると、その下のアドレスもすべて受信拒否になります」, red error line [BL-07] 「アドレスまたはドメインを正しく入れてください。」. Buttons [BL-03] 追加 / 削除 / 閉じる. Tab order: list, kind radio (2), value box, 追加, 削除, 閉じる; initial focus = the value box; Enter in the value box = 追加; Esc = 閉じる. 削除 asks confirm dialog [WD-15] (title 「受信拒否の解除」, text [BL-04] 「この受信拒否を解除しますか?」, buttons 解除する / やめる*, default やめる). Duplicate entries are rejected with error line [BL-05] 「すでに登録されています」.
- Evaluation: for a new incoming message (not initial sync, not UIDVALIDITY re-list) whose From address matches an entry of that account, move to Spam via IMAP and update memberships; the message is not processed by filter rules. Evaluated before filter rules (A25). Existing mail is not moved when an entry is added.
- "Block sender" from folder-actions calls `AddFromSender(accountId, address, kind)`.

## 3. Functions

- `IBlockRepository { GetAll(accountId); Add; Remove; }`.
- `BlockMatcher.Matches(entries, fromAddress)`.
- `BlockRule : IIncomingRule`.
- `BlockListViewModel`.

## 4. Tests and done criteria

- Unit: address/domain/subdomain matching and the `notexample.com` negative, case-insensitivity, duplicates, rule short-circuits filters, no action during initial sync.
- Manual: block the test account's second address and send a mail; it lands in Spam.
