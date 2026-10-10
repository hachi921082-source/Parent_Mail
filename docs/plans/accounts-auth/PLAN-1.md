# accounts-auth PLAN-1: model, credentials, add, switch, badge

Planning status: complete. Spec: detailed-spec 2; A4, A6, A31, A44, A56; Q53, Q56, Q66; PQ28. Layout: `window-layouts.md` sections 2.1, 5. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

`Account` model and repository, Credential Manager store, account-add screen (with login test), account switch button and other-account badge, current-account state (`IAppState`). Delete/rename/re-entry/warning dialog are in PLAN-2.

## 2. Behavior

- Max 2 accounts; the add action is disabled (greyed with [AC-04] 「アカウントは2つまでです」) at 2 (A4).
- Account-add window [WD-03] (title 「アカウントを追加」), MODAL (first launch: centered on the screen). Top to bottom: notice line [AC-01] 「Gmailのアプリパスワードが必要です。」; labels [AC-02] メールアドレス, アプリパスワード (PasswordBox, hidden), 表示名 (defaults to the part before @); a red error line; buttons [AC-03] 登録する* (default) and やめる. Field/tab order: メールアドレス, アプリパスワード, 表示名, 登録する, やめる; initial focus = メールアドレス; Enter in a field = 登録する (validate first); Esc = やめる. While the login test runs all fields are disabled and the button reads [AC-08] 「確認しています…」. No setup guide.
- Field errors (red error line, in place): empty/wrong address [AC-21] 「メールアドレスを正しく入れてください。」; empty password [AC-22] 「アプリパスワードを入れてください。」; empty name [AC-23] 「表示名を入れてください。」.
- First launch without an account: closing the window without adding (やめる, Esc or X) shows the confirm dialog [WD-09] (title 「アプリを終わりますか」) with text [AC-19] 「このまま閉じるとアプリは終わります。よろしいですか?」 and buttons 終わる / 戻る* (default 戻る; Enter = 戻る); 終わる exits the app, 戻る returns to the window (Q66). First launch with no account opens it directly without the Q31 dialog (A31); the Q31 dialog for later adds comes from PLAN-2's wrapper.
- Save flow (PQ28): validate fields (email format, non-empty); reject a duplicate address (case-insensitive) with the error line [AC-05] 「このメールアドレスは登録済みです」; run IMAP login and SMTP auth against `imap.gmail.com:993` (SSL) and `smtp.gmail.com:465` (SSL) via the gateways, showing a busy state; on failure show the red error line (login failed [AC-06] 「ログインできませんでした。アプリパスワードを確認してください。」; network [AC-07] 「接続できませんでした。ネットワークを確認してください。」) and save nothing; on success store the password in Credential Manager (target `ParentMail/<accountId>`, persistence LocalMachine-per-user "Enterprise"=false -> `CRED_PERSIST_LOCAL_MACHINE`), insert the account row, and trigger initial sync.
- Never write the password to SQLite, logs, or files.
- Account switch button (row 1 of the two-row top bar, left end; app-shell PLAN 3.2): [TB-09] shows the current account's display name only (no arrow or hint); click switches to the other account (with only one account the button just shows the name and does nothing). On switch: `IAppState.CurrentAccount` changes, the Inbox of that account is selected, selection cleared, preview zoom reset, search cleared. Last-used account id stored in settings (`last_account_id`).
- Badge (A44) [TB-10]: small red number badge (white text on #A80000) with the other account's Inbox unread count (number) when > 0; refreshed after each sync of that account.
- Switching does not cancel running syncs (both accounts keep syncing).

## 3. Functions

- `IAccountRepository { GetAll(); Add(Account); Rename(id, name); Delete(id); }`
- `CredentialStore.Save/Get/Delete(accountId)` over P/Invoke (`CredWriteW`, `CredReadW`, `CredDeleteW`; blob = UTF-16 password; handle non-ASCII).
- `AccountService.ValidateAndAddAsync(email, password, displayName, ct)` returns `AddAccountResult { Ok, Duplicate, AuthFailed, NetworkError }`.
- `IAppState { string? CurrentAccountId (get/set); FolderKind CurrentFolder; long? SelectedMessageId; events CurrentAccountChanged, CurrentFolderChanged, SelectedMessageChanged }` (wave-0 contract; the `Account` object is resolved through `IAccountRepository.Get(id)`; switching = setting `CurrentAccountId`).
- `AccountSwitchViewModel { Label, OtherUnread, SwitchCommand }`.

## 4. Tests and done criteria

- Unit (fake gateways): duplicate rejected, auth failure saves nothing, success saves row and credential; max-2 enforcement; badge count mapping.
- Manual (test Gmail account): add via real app password; wrong password rejected; the credential appears in Credential Manager; switching works with two accounts.
