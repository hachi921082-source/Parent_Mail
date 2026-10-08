# app-shell

Planning status: complete. Spec: detailed-spec 1, 17, 18; A1-A3; Q21, Q29 (see `docs/plans/README.md` section 9).

## Index

1. Scope
2. Files and structure
3. Behavior
4. Functions
5. Tests and done criteria

## 1. Scope

Solution bootstrap, DI composition root, main window skeleton (top bar, list pane, preview pane, splitter), banner/notice host, dialog service, Japanese string catalog, single-instance guard, global error handling, local log file, keyboard/focus/DPI rules.

## 2. Files and structure

- `Parent_Mail.sln`, `src/ParentMail.App`, `src/ParentMail.Core`, `tests/ParentMail.Tests`; `.gitignore` (bin, obj, `tests/local.secrets.json`, publish output).
- `App/App.xaml.cs`: single-instance mutex `Global\Parent_Mail_SingleInstance` plus a named event; a second launch signals the first (window restored and activated) and exits. Builds the DI container (`ServiceCollection`), registers all interfaces of README section 7, then shows `MainWindow`.
- `App/Features/Shell/MainWindow.xaml(.cs)`, `MainViewModel`: grid rows = banner area, top bar, content. Content columns = list pane, `GridSplitter`, preview pane. MinWidth 1024, MinHeight 700.
- `Resources/Strings.ja.cs`: static `Strings` with every UI string (folder labels A2: 受信トレイ, 送信トレイ, 送信済み, ごみ箱, 下書き, 迷惑メール).
- `Core/Platform/AppPaths.cs`, `AppLog.cs`.
- `App/Services/NotifierService` (implements `INotifier`), `DialogService` (implements `IDialogService`).

## 3. Behavior

- Top bar left to right: account switch button (content from accounts-auth), six folder buttons (Inbox with unread count text), then on the right (A1) the HTML checkbox and the refresh button. Inbox count and Outbox failure marker bind to `MainViewModel`.
- Banner host: one persistent banner slot (offline, wrong password, initial-sync 「メールを読み込んでいます…」), with severity color and optional action button. Transient notice host (bottom of preview pane): text plus optional action button, auto-hide after N seconds (used by Delete undo, 5 s).
- Dialog service: `ShowWarning(title, message, buttons[], defaultIndex)` with large buttons (min height 56 DIP, font from ITextScale); returns chosen index. Dialogs follow the text-size setting.
- Accessibility defaults (P2): tab order left to right, visible focus rectangle (thick 3 px), minimum hit target 48 DIP, per-monitor DPI v2 manifest, no animations required.
- Window size/position/state and splitter width: loaded from `ISettingsStore` at startup and saved on closing and on splitter drag end (store lives in the settings feature). Clamp to the screen if the saved position is off-screen.
- Global errors: `DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` -> write to log (exception type, message, stack only) and show 「問題が発生しました。もう一度お試しください。直らない場合は詳しい方に連絡してください。」 Non-fatal exceptions keep the app running.
- Local log (Q29): `%LOCALAPPDATA%\Parent_Mail\logs\parentmail.log`, UTF-8, one line per entry `yyyy-MM-dd HH:mm:ss.fff [LEVEL] area: message`. Rolled at 1 MB, keeping 3 files (`parentmail.1.log`, `.2.log`). Allowed content: timestamps, component names, exception type/message/stack, counts, durations, IMAP/SMTP error categories. Forbidden: email addresses, subjects, bodies, attachment names, passwords, URLs from mail. Thread-safe, never throws.

## 4. Functions

- `AppLog.Info/Warn/Error(string area, string message, Exception? ex)`; `Rotate()` internal.
- `INotifier.ShowBanner(BannerKind kind, string text, (string label, Action action)? action)`, `HideBanner(BannerKind)`, `ShowNotice(string text, string? actionLabel, Action? action, int seconds)`.
- `IDialogService.Choose(...)`, `Confirm(...)`.
- `SingleInstanceGuard.TryAcquire()`, `SignalExisting()`.

## 5. Tests and done criteria

- Unit: log rotation size/count; log scrubbing helper rejects strings that contain `@`; string catalog has no empty entries.
- Manual: second launch focuses the first window; window state restores; 1024x700 minimum enforced; forced exception shows the plain notice and logs without mail data; Japanese user-folder path works for the log and data root.
