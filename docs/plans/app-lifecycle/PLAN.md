# app-lifecycle

Planning status: complete. Spec: detailed-spec 16, 7; A31, A32, A48, A56; PQ16, Q34, Q43, Q52, Q58, Q66, Q75. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Startup sequence, first-launch handling, quit by X, open compose windows at quit, Outbox quit warning, Windows shutdown interception.

## 2. Behavior

- Startup (D5): acquire single instance (app-shell) -> open `settings.db` and migrate -> load settings -> load accounts -> if none: show the account-add screen (no warning dialog, A31) and continue startup after a successful add (closing it without adding asks the confirm dialog [WD-09] 「このまま閉じるとアプリは終わります。よろしいですか?」 [AC-19] with 終わる / 戻る*; 終わる exits the app; accounts-auth PLAN-1) and the main window then opens maximized with the list pane about 35 percent wide (Q58, app-shell PLAN 3.2); on later launches the last size, state and splitter position are restored -> select last-used account (fallback first) -> show main window on its Inbox -> start `SyncCoordinator` (immediate sync of both accounts, then every 5 min) -> flush dirty drafts and retry Outbox at the first sync. No auto-start with Windows and no setting for it (D6). No tray, no background process.
- Quit by closing the main window (Q34): sequence in `QuitCoordinator.RequestQuitAsync(reason)`:
  1. Open compose windows (A32): for each, run its close dialog (compose PLAN-2); any 「戻る」 aborts the quit.
  2. If `IOutboxService.PendingCount() > 0` (both accounts): modal dialog [WD-25] (title and text [QT-01] 「送信待ちのメールがあります。このまま閉じると送信されません」) with large buttons left to right [QT-02] 「送信してから閉じる」, [QT-03] 「このまま閉じる」, [QT-04] 「戻る」* (default; Enter = 戻る, Esc = 戻る).
     - 送信してから閉じる: busy text [QT-06] 「送っています…」, `SendAllPendingAsync`; if all sent -> quit; if any failed -> stay open and show the failure notice (outbox-send).
     - このまま閉じる: quit; unsent mail stays in Outbox.
     - 戻る: abort.
  3. Stop sync and workers, flush settings (window state), dispose DB connections, exit.
- Windows shutdown/logoff (A48, PQ16): handle `SessionEnding`: if compose windows have content or Outbox is non-empty, register the shutdown block reason [QT-05] 「送信待ちのメールがあります」 (`ShutdownBlockReasonCreate`) and run the same sequence SYNCHRONOUSLY inside the handler (modal dialog on a nested dispatcher loop; no async work may continue after the handler returns). Decide `e.Cancel` from the result: 「このまま閉じる」, a completed 送信してから閉じる and nothing-to-warn -> `e.Cancel = false` (Windows proceeds); 「戻る」 -> `e.Cancel = true` (shutdown cancelled, app keeps running; the user must start the shutdown again). If Windows force-closes the app, nothing more is done (best effort).
- Unhandled exception during quit logs only.

## 3. Functions

- `StartupCoordinator.RunAsync()`.
- `QuitCoordinator.RequestQuitAsync(QuitReason reason)` returns `bool quitting`.
- `ShutdownBlocker.Create(hwnd, reason)/Destroy(hwnd)` (P/Invoke `ShutdownBlockReasonCreate/Destroy`).
- `MainWindow.Closing` and `App.SessionEnding` handlers delegate to `QuitCoordinator`.

## 4. Tests and done criteria

- Unit: quit decision table (compose cancel, outbox choices, send failure keeps app open); startup with zero/one/two accounts; last-account fallback.
- Manual: close with an unsent Outbox item for each button; Windows sign-out with unsent mail shows the dialog; first launch with a clean data folder opens the account-add screen.

## Change request (Q89, 2026-10-14): single compose window

User decided (Issue #5): at most ONE compose window at a time; a repeat request only restores (if minimized) and activates the open one, no notice (detailed-spec section 7, A77-A81). developer-lite must change:
- Quit step 1 ("for each compose window") may become "the compose window, if any". Keeping the loop over `ComposeWindowRegistry` is acceptable (simplest, generic); at most one entry exists.
- Shutdown (`SessionEnding`) check "compose windows have content" likewise concerns at most one window.
- Test: quit decision table needs only the 0 or 1 compose window cases.
