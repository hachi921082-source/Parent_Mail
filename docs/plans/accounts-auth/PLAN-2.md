# accounts-auth PLAN-2: rename, delete, re-entry, warning dialog, wrong-password flow

Planning status: complete. Spec: detailed-spec 2, 15, 20; A5, A7, A8, A56, A72, A73; Q31, Q65, Q68. Layout: `window-layouts.md` sections 4, 6. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Accounts section of the settings window; Q31 warning dialog wrapper; account deletion with full purge; password re-entry; reaction to authentication failure from sync.

## 2. Behavior

- Settings > accounts list: each row shows display name, address, buttons [AC-09] 「名前を変える」, [AC-10] 「パスワードを入れ直す」, [AC-11] 「削除」. Plus [AC-12] 「アカウントを追加」 (disabled at 2). Add, delete and password re-entry go through the warning dialog first; 名前を変える opens the rename window directly (no warning; user decision 2026-10-08).
- Rename window [WD-05] (title 「名前を変える」, MODAL): field [AC-24] 「表示名」 pre-filled and selected, buttons [AC-26] 保存* / やめる, error line [AC-23]; Enter = 保存, Esc = やめる.
- Warning dialog (Q31) [WD-06] (title 「確認」, MODAL) before add (when an account already exists), delete, and user-initiated password re-entry (NOT before rename): text [AC-13] 「この操作は詳しい方に相談してから行ってください。」, buttons [AC-14] 続ける / キャンセル* (default = キャンセル; Enter = キャンセル), no PIN.
- Delete: if the account has unsent Outbox mail, NO warning dialog follows; the deletion is stopped with modal notice [WD-08] (title 「アカウントを削除できません」, text [AC-20] 「送信待ちのメールが残っているため、このアカウントは削除できません。先にメールを送るか、下書きに戻してから、もう一度お試しください。」, button 「OK」) (Q68; the old extra line [AC-15] is superseded). Otherwise: warning dialog, then the confirm dialog [WD-07] (title 「アカウントの削除」, text [AC-27] 「このアカウントと、このパソコンに保存したメールを削除します。よろしいですか?」, buttons 削除する / やめる* default やめる). (A7) final removal sequence: stop sync for the account, delete credential, delete account row (cascade removes contacts, block entries, filter rules), delete `accounts\<id>\` folder (cache.db and attachments), delete its Outbox/drafts (they live in cache.db; Outbox is empty at this point, see the check above). If it was the current account, switch to the remaining one; if none remain, open the account-add screen (A31 style, no warning).
- Wrong/revoked password (A8): sync/send gateway raises `AuthFailed` -> `INotifier.ShowBanner(WrongPassword, [AC-16] 「パスワードが使えなくなりました。詳しい方にパスワードの入れ直しを頼んでください。」, action [AC-17] 「入れ直す」)` (red banner, highest priority) and the re-entry window opens automatically once (not preceded by the Q31 dialog). Re-entry window [WD-04] (title [AC-18] 「パスワードの入れ直し」, MODAL): shows the account name and address, field アプリパスワード (hidden), error line, buttons [AC-25] 保存する* / やめる; Enter = 保存する, Esc = やめる; initial focus = the field; while testing the button reads [AC-08] 「確認しています…」; errors [AC-22], [AC-06], [AC-07]. save runs the same IMAP+SMTP login test; on success the credential is replaced, banner cleared, sync resumes. Cached mail stays viewable throughout. Auth failures stop further sync attempts for that account until re-entry succeeds (avoids lockouts).

## 3. Functions

- `AccountSettingsViewModel { Accounts, RenameCommand, DeleteCommand, ReenterPasswordCommand, AddCommand }`.
- `AccountService.DeleteAsync(accountId)`, `ReplacePasswordAsync(accountId, password)`.
- `WarningGate.ConfirmAsync()` (uses `IDialogService`, default Cancel).
- `ISyncService.MarkAuthFailed(accountId)` / `ClearAuthFailed`.

## 4. Tests and done criteria

- Unit: delete purges all rows and the directory (temp dir with Japanese name); re-entry clears auth-failed flag; rename persists; warning gate Cancel aborts; rename opens without the warning.
- Manual: delete one account while the other keeps working; revoke the test app password to verify the banner and re-entry.
