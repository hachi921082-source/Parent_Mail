# filter-rules

Planning status: complete. Spec: detailed-spec 12; A26, A27, A43; D10-D12; Q24 (planning answer: sender matches address and display name).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Per-account rule set (settings.db `filter_rules`), list dialog with enable/disable and reorder, evaluator as the second `IIncomingRule`.

## 2. Behavior

- Rule = optional conditions sender-contains, subject-contains, body-contains (at least one required) combined with AND (A26) + one action: 迷惑メールへ移動, ごみ箱へ移動, 既読にする. Matching is case- and width-normalized substring. Sender condition matches the From address or display name.
- Evaluation: new incoming mail only; rules in `sort_order` ascending, enabled only; first match applies its action and stops (D10). Body condition uses `body_text`, else `derived_text`; a message whose body is not downloaded never matches a body condition; rules never trigger fetches (D12). Runs after block list and only for mail not already handled.
- Actions: MoveToSpam / MoveToTrash via IMAP (memberships updated, no Undo notice), MarkRead via `SetSeenAsync`.
- Dialog (list): columns 有効 (checkbox), 条件の説明 (e.g. 「差出人に『abc』を含む かつ 件名に『xyz』を含む → ごみ箱へ」), 動作. Buttons 追加, 編集, 削除 (confirm, default やめる), 上へ, 下へ (A27). Edit dialog: three optional text boxes and an action drop-down; save is disabled until a condition and an action exist.
- Not applied to existing mail or during initial sync (A43).

## 3. Functions

- `IFilterRepository { GetAll(accountId); Add; Update; Delete; Reorder(accountId, orderedIds) }`.
- `FilterEvaluator.Evaluate(rules, message) -> FilterRule?`.
- `FilterRuleHandler : IIncomingRule`.
- `RuleDescriber.Describe(rule)` (Japanese text).

## 4. Tests and done criteria

- Unit: AND logic, first match wins, disabled skipped, undownloaded body never matches, derived text for HTML-only, address vs display name, reorder persistence, describer output.
- Manual: rule "subject contains test -> mark read" on a new mail.
