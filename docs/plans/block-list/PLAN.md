# block-list

Planning status: complete. Spec: detailed-spec 11; A24, A25, A43; Q24 (planning answer: domain includes subdomains).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Per-account block entries (settings.db `block_entries`), management window, evaluation as the first `IIncomingRule` of the incoming-mail pipeline (sync-cache PLAN-2).

## 2. Behavior

- Entry kinds: `Address` (exact, case-insensitive) or `Domain` (matches the domain and any subdomain: `example.com` matches `a@example.com` and `a@mail.example.com`, not `a@notexample.com`). Domain input accepts `example.com` or `@example.com` (normalized to lowercase without `@`).
- Window opened by the 受信拒否リスト button: list of entries (種類, 値), buttons 追加 (kind radio + text), 削除 (confirm 「この受信拒否を解除しますか?」, default やめる). Duplicate entries are rejected quietly with 「すでに登録されています」.
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
