# settings

Planning status: complete. Spec: detailed-spec 15, 1 (persistence); A5, A9; Q6, Q9.

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

- Text size: default 18 pt, range 12-48, free numeric input plus slider (synchronized), live sample text inside the settings window. Invalid input reverts to the last valid value. Applied immediately app-wide (list, buttons, headers, dialogs, compose, plain-text display) via a `TextScale` resource: `FontSizeDip = pt * 96 / 72` (Q9), published as dynamic resource `AppFontSize` plus derived sizes (header/button = x1.0, small = x0.85).
- HTML display does not use CSS size; ZoomFactor is computed by `ITextScale.GetWebViewZoom(zoom)` = `Round(Clamp(textSizePt / 12 * zoom, 0.25, 5.0), 2)` (Q10). Plain text uses `FontSizeDip * zoom`.
- Settings window: opened by the list-pane settings button; sections = text size, accounts (rename/add/delete/password; implemented in accounts-auth PLAN-2). Close applies; no Save button needed (changes are live and persisted on change).
- Window/splitter state API consumed by app-shell; zoom is NOT stored anywhere.

## 4. Functions

- `SettingsDb.Open(path)`, `Migrate()`; `ISettingsStore.Get<T>(key, default)`, `Set(key, value)`.
- `ITextScale { double TextSizePt; double FontSizeDip; event Changed; double GetWebViewZoom(double zoom); double GetPlainTextSizeDip(double zoom); }`.
- `SettingsViewModel { TextSizePt (clamped), SampleText }`.

## 5. Tests and done criteria

- Unit: migration from empty DB; `GetWebViewZoom(1.0)` at 18 pt = 1.50; at 48 pt x 3.0 = clamp 5.0; at 12 pt x 1.0 = 1.00; rounding to 2 decimals; pt->DIP 18 -> 24.
- Manual: change size, all windows follow immediately; relaunch keeps the value; Japanese path for settings.db.
