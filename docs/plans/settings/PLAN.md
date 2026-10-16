# settings

Planning status: complete. Spec: detailed-spec 15, 1 (persistence), 21; A5, A9, A56, A70; Q58, Q65, Q73; PQ6, PQ9. Layout: `window-layouts.md` section 4. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. settings.db schema
3. Behavior
4. Functions
5. Tests and done criteria

## 1. Scope

`settings.db` creation and migrations, `ISettingsStore` (app settings), text-size service (`ITextScale`), settings window (text size part; the accounts section is filled by accounts-auth PLAN-2).

## 2. settings.db schema (`PRAGMA user_version` migrations, WAL, foreign keys on)

```
accounts(id TEXT PK, email TEXT UNIQUE COLLATE NOCASE, display_name TEXT, sort_order INTEGER, created_utc INTEGER)
app_settings(key TEXT PK, value TEXT)           -- text_size_pt, window_*, splitter_width, last_account_id
contacts(id INTEGER PK, account_id TEXT FK CASCADE, name TEXT, reading TEXT, memo TEXT)
contact_emails(id INTEGER PK, contact_id INTEGER FK CASCADE, email TEXT COLLATE NOCASE)
block_entries(id INTEGER PK, account_id TEXT FK CASCADE, kind INTEGER, value TEXT COLLATE NOCASE)
filter_rules(id INTEGER PK, account_id TEXT FK CASCADE, sort_order INTEGER, enabled INTEGER,
             sender_contains TEXT NULL, subject_contains TEXT NULL, body_contains TEXT NULL, action INTEGER)
```
Tables for address-book/block-list/filter-rules are created by this migration (v1) so all share one migration chain; their repositories are implemented in their own features.

## 3. Behavior

- Text size: default 18 pt, range 12-48, free numeric input plus slider (synchronized), live sample text inside the settings window. Invalid input reverts to the last valid value. Applied immediately app-wide (list, buttons, headers, dialogs, compose, plain-text display) via a `TextScale` resource: `FontSizeDip = pt * 96 / 72` (PQ9), published as dynamic resource `AppFontSize` plus derived sizes (header/button = x1.0, small = x0.85).
- HTML display does not use CSS size; ZoomFactor is computed by `ITextScale.GetWebViewZoom(zoom)` = `Round(Clamp(min(textSizePt * zoom, 60) / 12, 0.25, 5.0), 2)` (PQ10, A70: effective preview size capped at 60 pt). Plain text uses `FontSizeDip * zoom`.
- Settings window [WD-02] (title [ST-01] 「設定」), MODAL, 720 DIP wide, centered on the main window; opened by the list-pane 設定 button. Layout top to bottom: [ST-02] 「文字の大きさ」 with number box + [ST-03] 「pt」 + slider (accessible name [ST-08]) and the range hint 12 - 48; the live sample box [ST-04]; [ST-05] 「アカウント」 heading with one row per account (up to 5 two-line rows, Issue #8: the rows area is a vertical scroll area showing 2 to 3 rows, Tab onto a hidden button scrolls it into view, [AC-12] and [ST-07] stay outside it, detailed-spec A87; display name + address, buttons [AC-09] 「名前を変える」, [AC-10] 「パスワードを入れ直す」, [AC-11] 「削除」; implemented in accounts-auth PLAN-2); [AC-12] 「アカウントを追加」 (disabled with [AC-04] 「アカウントは5つまでです」 at 5 accounts); the close button [ST-07] 「閉じる」 at the bottom right.
- Tab order = field order: number box, slider, then per account 名前を変える, パスワードを入れ直す, 削除; then アカウントを追加; then 閉じる. Initial focus = the number box. Enter = 閉じる; Esc = 閉じる (changes apply live, so closing keeps them). Invalid number: error line [ST-06] 「12から48の数字を入れてください。」 under the box, last valid value kept. パスワードを入れ直す, 削除 and アカウントを追加 go through the warning dialog first; 名前を変える opens the rename window directly (accounts-auth PLAN-2). Chrome text cap does not apply inside dialogs (they follow the setting).
- Window/splitter state API consumed by app-shell; zoom is NOT stored anywhere.

## 4. Functions

- `SettingsDb.Open(path)`, `Migrate()`; `ISettingsStore.Get()` returns `AppSettings`, `Save(AppSettings)` (wave-0 contract; typed record instead of key/value).
- `ITextScale { double TextSizePt; double FontSizeDip; double ChromeFontSizeDip (min(TextSizePt, 20) * 96/72); event Changed; double GetEffectivePreviewPt(double zoom); double GetWebViewZoom(double zoom); double GetPlainTextSizeDip(double zoom); }`.
- `SettingsViewModel { TextSizePt (clamped), SampleText }`.

## 5. Tests and done criteria

- Unit: migration from empty DB; `GetWebViewZoom(1.0)` at 18 pt = 1.50; at 48 pt x 3.0 = 5.00 (effective size capped at 60 pt); at 12 pt x 1.0 = 1.00; rounding to 2 decimals; pt->DIP 18 -> 24.
- Manual: change size, all windows follow immediately; relaunch keeps the value; Japanese path for settings.db.
