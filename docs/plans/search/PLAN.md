# search

Planning status: complete. Spec: detailed-spec 13; A47, A62, A63; D20; Q11, Q29, Q30, Q47, Q70, Q71; PQ20 (LIKE). Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Always-visible inline search box at the top of the list pane, local cache query, result list with folder names, Clear.

## 2. Behavior

- Box (placeholder [SR-03] 「差出人・件名・本文から探す」) with a [SR-01] 「検索」 action (Enter or button) and [SR-02] 「クリア」 button. The bottom 検索 button focuses the box (A47). Results replace the list; Clear restores the current folder view.
- Scope: all messages in the displayed account's cache (all memberships including AllMail-only). Matching: case-insensitive and width-normalized (full/half width, via `NormalizeKey`: `string.Normalize(NormalizationForm.FormKC)` + lower invariant) substring over sender (name and address), subject, and downloaded `body_text` / `derived_text`. No hiragana/katakana folding. SQL: `LIKE '%' || @q || '%' ESCAPE '\'` on `search_key` (sender+subject normalized, maintained at insert) and on a `body_key` column holding the normalized body text (`body_key` is a `messages` column defined in sync-cache PLAN-1 and filled by `SetBody`).
- Multiple words separated by spaces are ANDed (each must match some field).
- Result rows = the normal list rows plus a folder label (「受信トレイ」 etc.; a message with several memberships shows its primary view folder by priority Trash, Spam, Inbox, Sent, Drafts (same as the view rule Q42: a mail also in Trash is shown as Trash), else [SR-04] 「すべてのメール」 for All-Mail-only).
- A fixed in-place note under the box while results are shown: [SR-05] 「まだ保存されていない古いメールの本文は検索されません。」 (Q30). No results: [SR-07] 「見つかりませんでした」. Works offline. Results capped at 500 rows with [SR-06] 「結果が多いため一部のみ表示しています」.
- Result rows have the same row checkboxes, 「すべて選ぶ」 header and multi-select delete as the folder lists (Q71, A62, A63; mail-list PLAN 3 and folder-actions PLAN 4: mails already in Trash are skipped; Undo returns each mail to the folder shown on its result row, an All-Mail-only mail to All Mail only). Checks are cleared when the search or folder changes.
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
