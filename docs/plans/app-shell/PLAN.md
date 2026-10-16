# app-shell

Planning status: complete (updated 2026-10-08 for approved layouts, palette, Q58-Q78). Spec: detailed-spec 1, 17, 18, 20, 21; A1-A3, A49-A50, A53-A56; Q58, Q59, Q60, Q64, Q65, Q73, Q75; PQ21, PQ29 (see `docs/plans/README.md` section 9). Layout source: `docs/plans/window-layouts.md`; palette mockup: `docs/plans/mockups/main-window.html`; wording: `docs/plans/ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Files and structure
3. Behavior
4. Functions
5. Tests and done criteria

## 1. Scope

Solution bootstrap, DI composition root, main window skeleton (banner area, two-row top bar, list pane, preview pane, splitter), banner/notice host, dialog service, Japanese string catalog, palette, single-instance guard, global error handling, local log file, common window rules (modality, Enter/Esc, button order, tab order, focus, shortcuts), DPI.

## 2. Files and structure

- `Parent_Mail.sln`, `src/ParentMail.App`, `src/ParentMail.Core`, `tests/ParentMail.Tests`; `.gitignore` (bin, obj, `tests/local.secrets.json`, publish output).
- `App/App.xaml.cs`: single-instance mutex `Global\Parent_Mail_SingleInstance` plus a named event; a second launch signals the first (window restored and activated) and exits. Builds the DI container (`ServiceCollection`), registers all interfaces of README section 7, then shows `MainWindow`.
- `App/Features/Shell/MainWindow.xaml(.cs)`, `MainViewModel`: grid rows = banner area, top bar (2 rows), content. Content columns = list pane, `GridSplitter`, preview pane. MinWidth 1024, MinHeight 700. Window title [WD-01] 「Parent_Mail」.
- `Resources/Strings.ja.cs`: static `Strings` generated from `ui-strings.md` (every ID; Japanese text must be identical; section 18 gives titles and buttons of every window and dialog). Folder labels [TB-01..TB-06] 受信トレイ, 送信トレイ, 送信済み, ごみ箱, 下書き, 迷惑メール.
- `Resources/Palette.xaml`: brushes (below).
- `Core/Platform/AppPaths.cs`, `AppLog.cs`.
- `App/Services/NotifierService` (`INotifier`), `DialogService` (`IDialogService`), `WindowRules` (attached behaviors for Enter/Esc/focus).

## 3. Behavior

### 3.1 Palette (ui hex values from `window-layouts.md` / detailed-spec 20.4 / mockup)

| Use | Background | Text | Brush key |
|---|---|---|---|
| Page, list, preview | #FFFFFF | #000000 | `Page` |
| Offline banner | #FFD54F | #000000 bold | `BannerOffline` |
| Error banner, suspicious-link banner | #A80000 | #FFFFFF bold | `BannerError` |
| Loading / initial-sync banner | #CFE8FF | #000000 bold | `BannerLoading` |
| Unread badge, Outbox failure pill | #A80000 | #FFFFFF bold | `Badge` |
| Success / undo notice | #1B5E20 | #FFFFFF bold | `NoticeOk` |
| Primary button, active folder button | #0B4FA8 | #FFFFFF | `Primary` |
| Normal button | #E8E8E8, 2 px border #767676 | #000000 | `Button` |
| Selected row | #D6E4F7 + 6 px left bar #0B4FA8 | #000000 | `RowSelected` |
| List date text | #FFFFFF | #333333 | `DateText` |
| Hint text | #FFFFFF | #595959 | `Hint` |
| Header Delete button | #FFFFFF, border and text #A80000 bold | | `DeleteOutline` |
| Focus rectangle | 3 px #0B4FA8 (white on dark buttons) | | `Focus` |
| Bars (top bar, strip) | #F3F3F3, 2 px border #999999 | | `Bar` |

Rules: the Inbox count badge on the active (blue) folder button is a white pill with red text and a red border; the Outbox failure marker in a row is a red pill with white text (not red text). Colour is never the only signal (text, number or bold accompanies it).

### 3.2 Main window layout (window-layouts section 2)

- Order top to bottom: banner area (0-2 banners), top bar row 1, top bar row 2, content.
- Top bar row 1: one side-by-side account button per account (1 to 5) in a horizontally scrolling strip with ◀ ▶ arrow buttons at the left end (Issue #1, Q91; overflow Issue #8, Q92, A82-A87, details in accounts-auth PLAN-1 "Overflow"; details in accounts-auth PLAN-1 and window-layouts 2.1): each shows the account name only [TB-09]; selected = `Primary` fill + thick border + bold + leading check mark, non-selected = white + thin border and carries the red unread badge [TB-10] (non-selected only); one account = only the selected button; max about 300 DIP each with 1-2 accounts and 220 DIP with 3-5 (min 120), ellipsis + tooltip for long names; the strip takes all the width left of the HTML checkbox; at the right the HTML checkbox [TB-11] and the refresh button [TB-12] (busy text [TB-13]). Row 2: the six folder buttons in spec order; red badge for Inbox unread [TB-07] and for Outbox failed count [TB-08]; the active folder button is drawn pressed (thick border + `Primary` fill).
- Text cap (A49, Q59): constant `ChromeTextMaxPt = 20`; the top bar and the list-pane bottom strip use `min(setting pt, 20 pt)` for text; heights still grow to the minimum 56 DIP. All other text follows the text-size setting (12-48 pt).
- List pane (mail-list PLAN) bottom strip: 2 columns x 3 rows grid, each cell >= 56 DIP high, labels wrap to two lines instead of overflowing; order row by row: 新しいメール (primary button) / 検索 / 設定 / アドレス帳 / 受信拒否リスト / 振り分けルール [TB-14..TB-19].
- Sizes (window-layouts 1.2, DIP at 18 pt): button height >= 56, width >= 120, padding 24; any clickable target >= 48 x 48; text box/drop-down height >= 56; gap 16 (label to field 8); dialog margin 24; dialog width 520 (notice/confirm), 720 (forms); dialogs size to content up to 90 percent of the work area, then scroll; context-menu rows >= 48 DIP high.
- First launch (Q58): window maximized, list pane about 35 percent of the width. Afterwards the last window size/position/state and splitter width are restored (loaded from `ISettingsStore`, saved on closing and on splitter drag end; clamped to the screen if off-screen).

### 3.3 Banners, notices, dialogs (detailed-spec 20; A53-A55)

- Banner host above the top bar, full width, up to 2 banners at a time, each >= 56 DIP high, bold text, optional action button at the right. Priority (highest first): 1 wrong password [AC-16, action AC-17], 2 offline [BN-01], 3 Outbox failure [OB-01], 4 initial sync [BN-02], 5 others (missing Gmail folder [BN-03], generic sync error [BN-19]). With more than 2 conditions the two highest are shown; a lower one appears when a higher one ends. Colors per palette (wrong password/error red, offline yellow, loading light blue).
- Close button 「×」 (48 x 48, accessible name [TB-25] 閉じる) ONLY on the Outbox failure banner; once closed it does not return for the same failure (the marker on the Outbox row and the count on the Outbox button stay). Other banners end when their condition ends.
- The suspicious-link banner is NOT part of this stack (inside the preview, safety-warnings PLAN-1).
- Transient notice host (bottom of the preview pane): text plus optional action button; default duration `TransientNoticeSeconds = 4`; the delete undo notices [BN-07, BN-11] use `UndoNoticeSeconds = 10`. Colour `NoticeOk`.
- Modal notice: `IDialogService.ShowNotice(title, message)` (title is always 「お知らせ」) = modal dialog titled 「お知らせ」 [WD-10] with one big 「OK」 button [BN-12] (default; Enter and Esc close). Used for failures and refusals that follow an action the user just performed; problems found automatically (body fetch failure, sync errors) use banners or in-place text so dialogs never pile up. Message-to-kind mapping: detailed-spec 20.3 (the PLAN of each feature names the ID and kind).
- In-place text: hints and field errors inside panes/dialogs (red error line under a field).
- Dialog service: `Choose(title, message, buttons[], defaultIndex, safeIndex)` for 2- and 3-button dialogs; `Confirm(...)` (two buttons); large buttons (min 56 DIP), font from `ITextScale`; follows the text-size setting.

### 3.4 Window rules (window-layouts 1.1, detailed-spec 21; A56)

- Every dialog and every window opened from the main window (settings, address book, block list, filter rules, their edit dialogs, warnings, notices) is MODAL to its owner and centered on it (first-launch account-add: centered on the screen). Compose windows are the exception (independent, several allowed).
- Enter activates the SAFE default button: in confirm dialogs the cancel-type one (戻る / やめる / キャンセル); in form dialogs 保存 (or 登録する / 保存する; it only saves valid input and shows errors); in a multi-line field Enter inserts a new line. Exception (Q75): the compose close dialog's default is 「下書きに保存」 (only exception to "Enter = cancel-type button").
- Esc always cancels / closes (same as the cancel-type button; window X = Esc). Esc is not bound in compose.
- Button row right-aligned; order left to right: less safe / primary action first, SAFE choice LAST. Single-button notice: one big 「OK」, right-aligned, default.
- Default focus: main window = mail list; compose = 宛先; dialogs = default button (form dialogs: first input field).
- Shortcuts: F5 = refresh (same as 更新); Delete key = Delete action ONLY while the mail list has keyboard focus; no keyboard shortcut for Send; Ctrl+mouse wheel over the preview = zoom; no Ctrl+0 / Ctrl+plus / Ctrl+minus.
- Tab order of each window follows the field order in window-layouts.md (listed in each feature PLAN); main window tab order (window-layouts 2.5): account button 1, account button 2, HTML checkbox, 更新, six folder buttons, search box, クリア, すべて選ぶ, mail list (rows: Up/Down; Space toggles the row check), bottom strip (row by row), preview header buttons (返信, 全員に返信, 転送, [編集], 拡大, 縮小, 100%に戻す, 削除), preview body, attachment buttons.
- Visible focus rectangle 3 px (`Focus`), per-monitor DPI v2 manifest, no required animations.

### 3.5 Errors and log

- Global errors: `DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` -> write to log (exception type, message, stack only) and show the modal notice [BN-04] (title 「問題が発生しました」, [WD-30]). Non-fatal exceptions keep the app running.
- Local log (PQ29): `%LOCALAPPDATA%\Parent_Mail\logs\parentmail.log`, UTF-8, one line per entry `yyyy-MM-dd HH:mm:ss.fff [LEVEL] area: message`. Rolled at 1 MB, keeping 3 files (`parentmail.1.log`, `.2.log`). Allowed content: timestamps, component names, exception type/message/stack, counts, durations, IMAP/SMTP error categories. Forbidden: email addresses, subjects, bodies, attachment names, passwords, URLs from mail. Thread-safe, never throws.

## 4. Functions

- `AppLog.Info/Warn/Error(string area, string message, Exception? ex)`; `Rotate()` internal.
- `INotifier.ShowBanner(BannerKind kind, string text, (string label, Action action)? action)`, `HideBanner(BannerKind)`, `ShowNotice(string text, string? actionLabel, Action? action, int seconds)` (transient). `BannerKind` priority enum: WrongPassword, Offline, OutboxFailure, InitialSync, Other.
- `BannerHost`: keeps the active set, shows the top 2 by priority, handles dismissal of OutboxFailure.
- `IDialogService.ShowNotice(string title, string message)` (modal OK; wave-0 contract, title = window title from window-layouts.md), `Choose(...)`, `Confirm(...)`.
- `ChromeFontSize.Get(textPt)` = `min(textPt, ChromeTextMaxPt) * 96/72`.
- `SingleInstanceGuard.TryAcquire()`, `SignalExisting()`.
- `WindowRules.Apply(Window, defaultButton, cancelButton)`.

## 5. Tests and done criteria

- Unit: log rotation size/count; log scrubbing helper rejects strings that contain `@`; `Strings` has no empty entries and matches `ui-strings.md` IDs; `BannerHost` priority/limit (3 conditions -> 2 shown, lower appears when higher ends, only OutboxFailure closable and stays closed); `ChromeFontSize` caps at 20 pt (24 pt setting -> 20 pt chrome, 12 pt -> 12 pt).
- Manual: second launch focuses the first window; first launch is maximized with list about 35 percent; window state restores afterwards; 1024x700 minimum enforced and the two-row top bar fits at 20 pt cap; colours match the mockup; Enter/Esc/Tab order in dialogs; F5 refresh; Delete key works only with list focus; forced exception shows the modal notice and logs without mail data; Japanese user-folder path works for the log and data root.
