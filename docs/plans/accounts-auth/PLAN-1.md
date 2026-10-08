# accounts-auth PLAN-1: model, credentials, add, switch, badge

Planning status: complete. Spec: detailed-spec 2; A4, A6, A31, A44; Q28.

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

`Account` model and repository, Credential Manager store, account-add screen (with login test), account switch button and other-account badge, current-account state (`IAppState`). Delete/rename/re-entry/warning dialog are in PLAN-2.

## 2. Behavior

- Max 2 accounts; the add action is disabled (greyed with 「アカウントは2つまでです」) at 2 (A4).
- Account-add screen: email, app password (PasswordBox), display name (defaults to the part before @). Text states a Gmail app password is required; no setup guide. First launch with no account opens it directly without the Q31 dialog (A31); the Q31 dialog for later adds comes from PLAN-2's wrapper.
- Save flow (Q28): validate fields (email format, non-empty); reject a duplicate address (case-insensitive) with 「このメールアドレスは登録済みです」; run IMAP login and SMTP auth against `imap.gmail.com:993` (SSL) and `smtp.gmail.com:465` (SSL) via the gateways, showing a busy state; on failure show a plain notice (login failed: 「ログインできませんでした。アプリパスワードを確認してください。」; network: 「接続できませんでした。ネットワークを確認してください。」) and save nothing; on success store the password in Credential Manager (target `ParentMail/<accountId>`, persistence LocalMachine-per-user "Enterprise"=false -> `CRED_PERSIST_LOCAL_MACHINE`), insert the account row, and trigger initial sync.
- Never write the password to SQLite, logs, or files.
- Account switch button (left end of top bar): labeled with the current account's display name; click switches to the other account (with only one account the button just shows the name and does nothing). On switch: `IAppState.CurrentAccount` changes, the Inbox of that account is selected, selection cleared, preview zoom reset, search cleared. Last-used account id stored in settings (`last_account_id`).
- Badge (A44): small badge with the other account's Inbox unread count (number) when > 0; refreshed after each sync of that account.
- Switching does not cancel running syncs (both accounts keep syncing).

## 3. Functions

- `IAccountRepository { GetAll(); Add(Account); Rename(id, name); Delete(id); }`
- `CredentialStore.Save/Get/Delete(accountId)` over P/Invoke (`CredWriteW`, `CredReadW`, `CredDeleteW`; blob = UTF-16 password; handle non-ASCII).
- `AccountService.ValidateAndAddAsync(email, password, displayName, ct)` returns `AddAccountResult { Ok, Duplicate, AuthFailed, NetworkError }`.
- `IAppState { Account? Current; SwitchTo(accountId); event CurrentChanged }`.
- `AccountSwitchViewModel { Label, OtherUnread, SwitchCommand }`.

## 4. Tests and done criteria

- Unit (fake gateways): duplicate rejected, auth failure saves nothing, success saves row and credential; max-2 enforcement; badge count mapping.
- Manual (test Gmail account): add via real app password; wrong password rejected; the credential appears in Credential Manager; switching works with two accounts.
