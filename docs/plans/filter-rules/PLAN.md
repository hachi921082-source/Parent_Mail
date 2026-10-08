# filter-rules

Planning status: complete. Spec: detailed-spec 12; A26, A27, A43, A56; D10-D12; Q65; PQ24 (planning answer: sender matches address and display name). Layout: `window-layouts.md` section 9. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Per-account rule set (settings.db `filter_rules`), list dialog with enable/disable and reorder, evaluator as the second `IIncomingRule`.

## 2. Behavior

- Rule = optional conditions sender-contains, subject-contains, body-contains (at least one required) combined with AND (A26) + one action: 迷惑メールへ移動, ごみ箱へ移動, 既読にする. Matching is case- and width-normalized substring. Sender condition matches the From address or display name.
- Evaluation: new incoming Inbox mail only (same definition as sync-cache PLAN-2 section 3); rules in `sort_order` ascending, enabled only; first match applies its action and stops (D10). Body condition uses `body_text`, else `derived_text`; a message whose body is not downloaded never matches a body condition; rules never trigger fetches (D12). Runs after block list and only for mail not already handled.
- Actions: MoveToSpam / MoveToTrash via IMAP (memberships updated, no Undo notice), MarkRead via `SetSeenAsync`.
- Window [WD-17] (MODAL, 900 x 600 DIP at 18 pt, centered on the main window, title [FR-01] 「振り分けルール - <account display name>」): columns [FR-03] 有効 (checkbox, toggled with Space or click) / 条件の説明 (e.g. [FR-04] 「差出人に『abc』を含む かつ 件名に『xyz』を含む → ごみ箱へ」) / 動作; empty text [FR-11] 「ルールはありません」. Buttons [FR-05] 追加 / 編集 / 削除 / 上へ / 下へ (A27) / 閉じる. Tab order: list, 追加, 編集, 削除, 上へ, 下へ, 閉じる; initial focus = list; Enter on a row = 編集; Esc = 閉じる. 削除 asks confirm dialog [WD-19] (title 「ルールの削除」, text [FR-06] 「このルールを削除しますか?」, buttons 削除 / やめる*, default やめる).
- Edit dialog [WD-18] (MODAL; titles [FR-08] 「ルールを追加」/「ルールを編集」; hint [FR-12] 「条件をすべて満たすメールに使います」; labels [FR-07] 差出人に含む文字 / 件名に含む文字 / 本文に含む文字 / 動作; action drop-down [FR-02] 迷惑メールへ移動 / ごみ箱へ移動 / 既読にする; buttons [FR-13] 保存* / やめる). Field order = tab order: 差出人に含む文字, 件名に含む文字, 本文に含む文字, 動作, 保存, やめる; initial focus = 差出人に含む文字; Enter = 保存; Esc = やめる. 保存 is NEVER disabled: with no condition the red error line shows [FR-09] 「条件を1つ以上入れてください。」, with no action [FR-10] 「動作を選んでください。」 (replaces the earlier disabled-button design).
- Not applied to existing mail or during initial sync (A43).

## 3. Functions

- `IFilterRepository { GetAll(accountId); Add; Update; Delete; Reorder(accountId, orderedIds) }`.
- `FilterEvaluator.Evaluate(rules, message) -> FilterRule?`.
- `FilterRuleHandler : IIncomingRule`.
- `RuleDescriber.Describe(rule)` (Japanese text).

## 4. Tests and done criteria

- Unit: AND logic, first match wins, disabled skipped, undownloaded body never matches, derived text for HTML-only, address vs display name, reorder persistence, describer output.
- Manual: rule "subject contains test -> mark read" on a new mail.
