# app-lifecycle

Planning status: complete. Spec: detailed-spec 16, 7; A31, A32, A48; Q16, Q34, Q43.

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Startup sequence, first-launch handling, quit by X, open compose windows at quit, Outbox quit warning, Windows shutdown interception.

## 2. Behavior

- Startup (D5): acquire single instance (app-shell) -> open `settings.db` and migrate -> load settings -> load accounts -> if none: show the account-add screen (no warning dialog, A31) and continue startup after a successful add (closing the screen without adding exits the app) -> select last-used account (fallback first) -> show main window on its Inbox -> start `SyncCoordinator` (immediate sync of both accounts, then every 5 min) -> flush dirty drafts and retry Outbox at the first sync. No auto-start with Windows and no setting for it (D6). No tray, no background process.
- Quit by closing the main window (Q34): sequence in `QuitCoordinator.RequestQuitAsync(reason)`:
  1. Open compose windows (A32): for each, run its close dialog (compose PLAN-2); any 「戻る」 aborts the quit.
  2. If `IOutboxService.PendingCount() > 0` (both accounts): dialog 「送信待ちのメールがあります。このまま閉じると送信されません」 with large buttons 「送信してから閉じる」, 「このまま閉じる」, 「戻る」 (default).
     - 送信してから閉じる: busy state, `SendAllPendingAsync`; if all sent -> quit; if any failed -> stay open and show the failure notice (outbox-send).
     - このまま閉じる: quit; unsent mail stays in Outbox.
     - 戻る: abort.
  3. Stop sync and workers, flush settings (window state), dispose DB connections, exit.
- Windows shutdown/logoff (A48, Q16): handle `SessionEnding`: if compose windows have content or Outbox is non-empty, set `e.Cancel = true` and run the same sequence (shutdown block reason text 「送信待ちのメールがあります」 registered through `ShutdownBlockReasonCreate`); 「このまま閉じる」 (and nothing-to-warn) -> `Application.Shutdown()` and let Windows proceed (no further cancel; Windows is not re-requested); 「戻る」 keeps the app running and the shutdown cancelled. If Windows force-closes the app, nothing more is done (best effort).
- Unhandled exception during quit logs only.

## 3. Functions

- `StartupCoordinator.RunAsync()`.
- `QuitCoordinator.RequestQuitAsync(QuitReason reason)` returns `bool quitting`.
- `ShutdownBlocker.Create(hwnd, reason)/Destroy(hwnd)` (P/Invoke `ShutdownBlockReasonCreate/Destroy`).
- `MainWindow.Closing` and `App.SessionEnding` handlers delegate to `QuitCoordinator`.

## 4. Tests and done criteria

- Unit: quit decision table (compose cancel, outbox choices, send failure keeps app open); startup with zero/one/two accounts; last-account fallback.
- Manual: close with an unsent Outbox item for each button; Windows sign-out with unsent mail shows the dialog; first launch with a clean data folder opens the account-add screen.
