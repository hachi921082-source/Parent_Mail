# accounts-auth PLAN-2: rename, delete, re-entry, warning dialog, wrong-password flow

Planning status: complete. Spec: detailed-spec 2, 15; A5, A7, A8; Q31.

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Accounts section of the settings window; Q31 warning dialog wrapper; account deletion with full purge; password re-entry; reaction to authentication failure from sync.

## 2. Behavior

- Settings > accounts list: each row shows display name, address, buttons 「名前を変える」(Rename, inline editor, no warning dialog), 「パスワードを入れ直す」, 「削除」. Plus 「アカウントを追加」 (disabled at 2).
- Warning dialog (Q31) before add (when an account already exists), delete, and user-initiated password re-entry: text 「この操作は詳しい方に相談してから行ってください。」, buttons 「続ける」 and 「キャンセル」 (default = キャンセル), no PIN.
- Delete (A7): after the warning, final removal sequence: stop sync for the account, delete credential, delete account row (cascade removes contacts, block entries, filter rules), delete `accounts\<id>\` folder (cache.db and attachments), delete its Outbox/drafts (they live in cache.db; if the Outbox holds unsent mail the dialog text adds 「送信待ちのメールも消えます」). If it was the current account, switch to the remaining one; if none remain, open the account-add screen (A31 style, no warning).
- Wrong/revoked password (A8): sync/send gateway raises `AuthFailed` -> `INotifier.ShowBanner(WrongPassword, 「パスワードが使えなくなりました。詳しい方にパスワードの入れ直しを頼んでください。」, action 「入れ直す」)` and the re-entry screen opens automatically once (not preceded by the Q31 dialog). The screen has the app-password field; save runs the same IMAP+SMTP login test; on success the credential is replaced, banner cleared, sync resumes. Cached mail stays viewable throughout. Auth failures stop further sync attempts for that account until re-entry succeeds (avoids lockouts).

## 3. Functions

- `AccountSettingsViewModel { Accounts, RenameCommand, DeleteCommand, ReenterPasswordCommand, AddCommand }`.
- `AccountService.DeleteAsync(accountId)`, `ReplacePasswordAsync(accountId, password)`.
- `WarningGate.ConfirmAsync()` (uses `IDialogService`, default Cancel).
- `ISyncService.MarkAuthFailed(accountId)` / `ClearAuthFailed`.

## 4. Tests and done criteria

- Unit: delete purges all rows and the directory (temp dir with Japanese name); re-entry clears auth-failed flag; rename persists; warning gate Cancel aborts.
- Manual: delete one account while the other keeps working; revoke the test app password to verify the banner and re-entry.
