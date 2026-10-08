# search

Planning status: complete. Spec: detailed-spec 13; A28, A47; D20; Q11, Q29, Q30; Q20 (planning answer: LIKE).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Always-visible inline search box at the top of the list pane, local cache query, result list with folder names, Clear.

## 2. Behavior

- Box with a 「検索」 action (Enter or button) and 「クリア」 button. The bottom 検索 button focuses the box (A47). Results replace the list; Clear restores the current folder view.
- Scope: all messages in the displayed account's cache (all memberships including AllMail-only). Matching: case-insensitive and width-normalized (full/half width, via `NormalizeKey`: `string.Normalize(NormalizationForm.FormKC)` + lower invariant) substring over sender (name and address), subject, and downloaded `body_text` / `derived_text`. No hiragana/katakana folding. SQL: `LIKE '%' || @q || '%' ESCAPE '\'` on `search_key` (sender+subject normalized, maintained at insert) and on a `body_key` column holding the normalized body text (`body_key` is a `messages` column defined in sync-cache PLAN-1 and filled by `SetBody`).
- Multiple words separated by spaces are ANDed (each must match some field).
- Result rows = the normal list rows plus a folder label (「受信トレイ」 etc.; a message with several memberships shows its primary view folder by priority Inbox, Sent, Drafts, Spam, Trash, else 「すべてのメール」 for All-Mail-only).
- A fixed note under the box while results are shown: 「まだ保存されていない古いメールの本文は検索されません。」 (Q30). Works offline. Results capped at 500 rows with 「結果が多いため一部のみ表示しています」.
- Selecting a result opens it in the preview; header buttons use the actions available for its folder.
- Clearing/leaving: switching folder or account clears the search.
- Outbox items are not searched.

## 3. Functions

- `SearchService.SearchAsync(accountId, query, ct)` returns `IReadOnlyList<MailListItem>`.
- `TextNormalizer.NormalizeKey(string)`.
- `SearchViewModel { Query, IsActive, SearchCommand, ClearCommand, FocusRequested }`.

## 4. Tests and done criteria

- Unit: full/half-width and case equivalence, Japanese substring (2-character query), AND of words, All-Mail-only found, undownloaded body excluded, LIKE escaping of `%` and `_`, cap.
- Manual: search a Japanese word in subject and in a body offline.
