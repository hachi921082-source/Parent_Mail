# UI Strings

Status (2026-10-08): FULLY APPROVED by the user (section 17 decisions on 2026-10-08, all remaining rows on 2026-10-08 in the follow-up review, Q76). This file is the source of truth for UI wording; `Strings.ja.cs` is generated from it. The earlier note that only part of the file was approved is obsolete. Exception already applied: FA-08 is 「迷惑メールではない」 (suffix dropped by the user).

Addendum (2026-10-16, Issue #8, Q92): maximum 5 accounts. AC-04 now reads 「アカウントは5つまでです」; new rows TB-26, TB-27, TB-28 for the account-strip scroll arrows (A82, A85, A88; the wording is a designer-lite assumption the user may veto).

Addendum (2026-10-16, Issue #1, Q91): the single account switch button is replaced by two account buttons; TB-09, TB-10 and N10 reworded accordingly. No new Japanese string was added (the check mark is a symbol, not a string).

Addendum (2026-10-12): FA-09 reworded to 「元のフォルダに戻す」 and TB-15 (bottom 検索 button) SUPERSEDED, both by user decision (Q87, Q88).

Addendum (2026-10-11): rows CP-18 to CP-22 and WD-31 (address-book picker in compose, Q80-Q85) are APPROVED (wording approved by the user on 2026-10-11).

Single list of every user-visible Japanese string referenced by `docs/plans/**/PLAN*.md` plus the strings the UI needs but no PLAN contains yet. When a row is APPROVED it is the source of truth (PLANs keep inline wording as drafts; on any difference this file wins). 

Columns: **Status** = APPROVED (user decided or approved in review, 2026-10-08) / SUPERSEDED. **Where shown** = the exact place on screen. **Origin**: SPEC = quoted in spec/detailed-spec/QA; A2 = folder names of assumption A2; PROPOSED = written by developer-lite; NEW = written by designer-lite (2026-10-08, or earlier where needed).
Tone rule (detailed-spec 17): polite plain Japanese, no technical terms, one sentence on what to do next; problems the parent cannot fix say to ask a knowledgeable person (詳しい方). "N", "M" in a text are numbers filled in by the app; `<...>` is a value filled in by the app.

## Index

1. Top bar and shell
2. Banners, notices, global errors
3. Accounts
4. Settings
5. Mail list
6. Preview and display
7. Safety warnings
8. Compose
9. Reply and forward
10. Outbox
11. Folder actions and context menus
12. Address book
13. Block list
14. Filter rules
15. Search
16. Quit and shutdown
17. Decided naming and wording
18. Windows and dialogs: titles and buttons

## 1. Top bar and shell

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| TB-01 | Folder button Inbox | 受信トレイ | APPROVED | A2 | unvetoed assumption A2 |
| TB-02 | Folder button Outbox | 送信トレイ | APPROVED | A2 | |
| TB-03 | Folder button Sent | 送信済み | APPROVED | A2 | |
| TB-04 | Folder button Trash | ごみ箱 | APPROVED | A2 | |
| TB-05 | Folder button Drafts | 下書き | APPROVED | A2 | |
| TB-06 | Folder button Spam | 迷惑メール | APPROVED | A2 | |
| TB-07 | Inbox button with unread | 受信トレイ + red number badge N | APPROVED | PROPOSED | hidden when 0; layout may show the number as a badge instead of "(N)" |
| TB-08 | Outbox button with failed items | 送信トレイ + red badge !N | APPROVED | NEW | N = failed count; A40 |
| TB-09 | Account buttons (two, one per account; Issue #1) | <display name> | APPROVED | NEW | N10: account name only on each button. The selected button also has a leading check mark (✓): a symbol drawn by the view, NOT a localized string, so it has no ID and is not in `Strings.ja.cs`. Tooltip = full display name (not new wording). Long names end with an ellipsis |
| TB-10 | Account button badge | N (number only) | APPROVED | NEW | A44; shown only inside the NON-selected account button, right of the name (Issue #1, Q91) |
| TB-11 | Top bar, HTML checkbox | HTMLで表示 | APPROVED | NEW | |
| TB-12 | Top bar, refresh button | 更新 | APPROVED | NEW | |
| TB-13 | Refresh button while syncing | 更新中… | APPROVED | NEW | |
| TB-14 | List pane bottom: compose | 新しいメール | APPROVED | PROPOSED | N3 |
| TB-15 | List pane bottom: search | 検索 | SUPERSEDED | PROPOSED | the bottom 検索 button is removed (user, 2026-10-12, Q88); its grid cell stays blank. The search box's own action SR-01 stays |
| TB-16 | List pane bottom: settings | 設定 | APPROVED | PROPOSED | |
| TB-17 | List pane bottom: address book | アドレス帳 | APPROVED | PROPOSED | |
| TB-18 | List pane bottom: block list | 受信拒否リスト | APPROVED | PROPOSED | N1 |
| TB-19 | List pane bottom: filter rules | 振り分けルール | APPROVED | PROPOSED | N2 |
| TB-20 | Preview header buttons | 返信 / 全員に返信 / 転送 / 削除 | APPROVED | PROPOSED | N4 |
| TB-21 | Preview zoom buttons | 拡大 / 縮小 / 100%に戻す | APPROVED | PROPOSED | N5 |
| TB-22 | Preview zoom percentage | 150% (example) | APPROVED | PROPOSED | |
| TB-23 | Preview header Delete button while rows are checked | 選んだN通を削除 | APPROVED | NEW | multi-select (detailed-spec 22) |
| TB-24 | Preview header, Drafts only | 編集 | APPROVED | NEW | Q63: the only extra header button |
| TB-25 | Outbox failure banner, close button (accessible name; shown as ×) | 閉じる | APPROVED | NEW | only banner with a close button (Q64) |
| TB-26 | Account strip, left scroll arrow (tooltip and accessible name; the arrow itself is the symbol ◀) | 左のアカウントを見る | APPROVED | NEW | Issue #8, Q92, A82/A88 (user may veto); shown only when account buttons overflow |
| TB-27 | Account strip, right scroll arrow (tooltip and accessible name; the arrow itself is the symbol ▶) | 右のアカウントを見る | APPROVED | NEW | Issue #8, Q92, A82/A88 (user may veto) |
| TB-28 | Scroll arrow carrying an unread badge, extra tooltip line | 見えていないアカウントに未読メールがあります | APPROVED | NEW | Issue #8, Q92, A85/A88 (user may veto); the badge number itself is a number only like TB-10 |

## 2. Banners, notices, global errors

Kind of each message (banner / modal OK / transient / in-place): detailed-spec section 20.3.

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| BN-01 | Banner, offline (yellow) | インターネットにつながっていません。保存済みのメールは読めます。 | APPROVED | PROPOSED | banner required by spec; wording proposed |
| BN-02 | Banner, initial sync (light blue) | メールを読み込んでいます… | APPROVED | PROPOSED | quoted in spec.md section 8 / Q52 |
| BN-03 | Banner, Gmail folder missing | Gmailの設定でIMAPの表示を確認してください。詳しい方に相談してください。 | APPROVED | PROPOSED | N8: technical term kept as written |
| BN-04 | Modal OK, unhandled error | 問題が発生しました。もう一度お試しください。直らない場合は詳しい方に連絡してください。 | APPROVED | PROPOSED | |
| BN-05 | Modal OK, move refused offline | 今はインターネットにつながっていないため操作できません。つながってからもう一度お試しください。 | APPROVED | PROPOSED | N9, Q55 |
| BN-06 | Modal OK, move failed | 操作できませんでした。もう一度お試しください。 | APPROVED | PROPOSED | |
| BN-07 | Transient notice with button, after delete (10 s) | ごみ箱に移動しました [元に戻す] | APPROVED | SPEC | text fixed by spec; duration 10 s (Q64) |
| BN-08 | Preview in-place, body not downloaded | このメールはまだ保存されていません。インターネットにつながると自動で表示します。 | APPROVED | PROPOSED | spec requires a "not downloaded yet" message |
| BN-09 | Modal OK, attachment not downloaded | この添付ファイルはまだ保存されていません。インターネットにつながってから開いてください。 | APPROVED | PROPOSED | |
| BN-10 | Preview in-place, body loading | 読み込んでいます… | APPROVED | PROPOSED | |
| BN-11 | Transient notice with button, after deleting several (10 s) | N通をごみ箱に移動しました [元に戻す] | APPROVED | NEW | text given by the user (Q71) |
| BN-12 | Button of every modal notice | OK | APPROVED | NEW | Q64: one big 「OK」 button |
| BN-13 | Preview in-place, body fetch failed | このメールの内容を読み込めませんでした。しばらくしてからもう一度お試しください。 | APPROVED | NEW | automatic action, so not a dialog |
| BN-14 | Modal OK, attachment download failed | 添付ファイルを取り込めませんでした。しばらくしてからもう一度お試しください。 | APPROVED | NEW | |
| BN-15 | Modal OK, attachment save failed | ファイルを保存できませんでした。保存先を変えてもう一度お試しください。 | APPROVED | NEW | |
| BN-16 | Modal OK, attachment open failed | ファイルを開けませんでした。詳しい方に相談してください。 | APPROVED | NEW | parent cannot fix |
| BN-17 | Modal OK, draft save failed after the user chose 下書きに保存 | 下書きを保存できませんでした。もう一度お試しください。 | APPROVED | NEW | |
| BN-18 | Compose window status line, draft not yet uploaded | 下書きはこのパソコンに保存しています。インターネットにつながると自動で保存します。 | APPROVED | NEW | A12, A74 |
| BN-19 | Banner, generic sync error (red) | メールの受信がうまくいきませんでした。しばらくしてから「更新」を押してください。 | APPROVED | NEW | details only in the log |
| BN-20 | Transient notice, generic success text template | (none: each success has its own text, e.g. BN-07, BN-11, FA-11) | APPROVED | NEW | duration 4 s (A54) |
| BN-21 | Modal OK, some of several mails could not be moved | N通のうちM通を移せませんでした。もう一度お試しください。 | APPROVED | NEW | multi-select (A64) |
| BN-22 | Modal OK, more than 200 checked | 一度に削除できるのは200通までです。選びなおしてください。 | APPROVED | NEW | A61 |
| BN-23 | Modal OK, reply/forward of a not-downloaded mail while offline | このメールはまだ保存されていないため、返信や転送ができません。インターネットにつながってからもう一度お試しください。 | APPROVED | NEW | reply-forward PLAN refuses the action |
| BN-24 | Modal OK, mail list could not load | メールの一覧を表示できませんでした。詳しい方に相談してください。 | APPROVED | NEW | local cache error |

## 3. Accounts

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| AC-01 | Account-add window, notice line | Gmailのアプリパスワードが必要です。 | APPROVED | NEW | spec: must state a Gmail app password is required; term アプリパスワード approved (N8) |
| AC-02 | Account-add window, field labels | メールアドレス / アプリパスワード（スペースなし） / 表示名 | APPROVED | NEW | A6 |
| AC-03 | Account-add window, buttons | 登録する / やめる | APPROVED | NEW | |
| AC-04 | Settings, add button when 5 accounts exist (was 2 until Issue #8) | アカウントは5つまでです | APPROVED | PROPOSED | A4, A88; changed from 「アカウントは2つまでです」 by Q92 (max 5 accounts) |
| AC-05 | Account-add window, error line, duplicate address | このメールアドレスは登録済みです | APPROVED | PROPOSED | Q53 |
| AC-06 | Account-add window, error line, login failed | ログインできませんでした。アプリパスワードを確認してください。 | APPROVED | PROPOSED | N9: stays as written |
| AC-07 | Account-add window, error line, network error | 接続できませんでした。ネットワークを確認してください。 | APPROVED | PROPOSED | N9: stays as written |
| AC-08 | Account-add and password re-entry, button while testing | 確認しています… | APPROVED | NEW | |
| AC-09 | Settings, per-account button | 名前を変える | APPROVED | PROPOSED | |
| AC-10 | Settings, per-account button | パスワードを入れ直す | APPROVED | PROPOSED | |
| AC-11 | Settings, per-account button | 削除 | APPROVED | PROPOSED | |
| AC-12 | Settings, add button | アカウントを追加 | APPROVED | PROPOSED | |
| AC-13 | Warning dialog text (add/delete/re-entry) | この操作は詳しい方に相談してから行ってください。 | APPROVED | SPEC | N11 |
| AC-14 | Warning dialog buttons | 続ける / キャンセル | APPROVED | PROPOSED | default = キャンセル (spec) |
| AC-15 | Delete-account dialog extra line | 送信待ちのメールも消えます | SUPERSEDED | PROPOSED | replaced by AC-20 (Q68: deletion is stopped) |
| AC-16 | Banner, wrong/revoked password (red) | パスワードが使えなくなりました。詳しい方にパスワードの入れ直しを頼んでください。 | APPROVED | PROPOSED | spec requires plain notice + ask a knowledgeable person |
| AC-17 | Wrong-password banner, action button | 入れ直す | APPROVED | PROPOSED | |
| AC-18 | Password re-entry window, title and field | パスワードの入れ直し / アプリパスワード（スペースなし） | APPROVED | NEW | |
| AC-19 | Confirm dialog, first account-add screen closed without adding | このまま閉じるとアプリは終わります。よろしいですか? [終わる] [戻る] | APPROVED | NEW | text given by the user, default 戻る; wording kept as given after review (Q76) |
| AC-20 | Modal OK, deleting an account that has Outbox mail | 送信待ちのメールが残っているため、このアカウントは削除できません。先にメールを送るか、下書きに戻してから、もう一度お試しください。 | APPROVED | NEW | Q68; one next-step sentence |
| AC-21 | Account-add window, error line | メールアドレスを正しく入れてください。 | APPROVED | NEW | empty or wrong format |
| AC-22 | Account-add and re-entry windows, error line | アプリパスワードを入れてください。 | APPROVED | NEW | empty |
| AC-23 | Account-add and rename windows, error line | 表示名を入れてください。 | APPROVED | NEW | empty |
| AC-24 | Rename window, field label | 表示名 | APPROVED | NEW | |
| AC-25 | Re-entry window, buttons | 保存する / やめる | APPROVED | NEW | |
| AC-26 | Rename window, buttons | 保存 / やめる | APPROVED | NEW | |
| AC-27 | Confirm dialog, delete account (after AC-13) | このアカウントと、このパソコンに保存したメールを削除します。よろしいですか? [削除する] [やめる] | APPROVED | NEW | default やめる; Gmail itself is not changed |

## 4. Settings

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| ST-01 | Settings window title | 設定 | APPROVED | NEW | |
| ST-02 | Settings, text size label | 文字の大きさ | APPROVED | NEW | |
| ST-03 | Settings, unit next to the size | pt | APPROVED | NEW | N8 |
| ST-04 | Settings, live sample | これは文字の大きさの見本です。 | APPROVED | NEW | D21 |
| ST-05 | Settings, accounts heading | アカウント | APPROVED | NEW | |
| ST-06 | Settings, error line under the size box | 12から48の数字を入れてください。 | APPROVED | NEW | |
| ST-07 | Settings, close button | 閉じる | APPROVED | NEW | |
| ST-08 | Settings, slider accessible name | 文字の大きさを変える | APPROVED | NEW | |

## 5. Mail list

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| ML-01 | Empty folder, centered in the list (NOT shown during the first sync) | メールはありません | APPROVED | PROPOSED | Q62 |
| ML-02 | Sent/Drafts row, line 1 sender position | 宛先: <first recipient> | APPROVED | PROPOSED | ML-14 adds the suffix |
| ML-03 | Outbox row, failure marker (red pill) | 送信できませんでした | APPROVED | PROPOSED | |
| ML-04 | Row date, today | HH:mm (e.g. 14:30) | APPROVED | SPEC | Q44 |
| ML-05 | Row date, this year | M月d日 H:mm (e.g. 10月8日 14:30) | APPROVED | SPEC | Q44 |
| ML-06 | Row date, older | yyyy年M月d日 H:mm (e.g. 2025年10月8日 14:30) | APPROVED | SPEC | Q44 |
| ML-07 | Attachment icon tooltip | 添付ファイルあり | APPROVED | NEW | |
| ML-08 | Row checkbox, accessible name | このメールを選ぶ | APPROVED | NEW | Q71 |
| ML-09 | List header, select-all checkbox | すべて選ぶ | APPROVED | NEW | A61 |
| ML-10 | List header, while rows are checked | N通を選んでいます | APPROVED | NEW | |
| ML-11 | List header, clear button | 選択をやめる | APPROVED | NEW | |
| ML-12 | Row line 1 when no sender | (差出人なし) | APPROVED | NEW | |
| ML-13 | Row line 2 when no subject | (件名なし) | APPROVED | NEW | |
| ML-14 | Sent/Drafts row, when more than one recipient | 宛先: <first recipient> ほか N 人 | APPROVED | NEW | A67 |

## 6. Preview and display

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| PV-01 | Preview header, labels | 差出人 / 宛先 / CC / 件名 / 日時 | APPROVED | NEW | CC line only when the mail has CC (A66) |
| PV-02 | Preview header notice | アドレス帳に登録されていません | APPROVED | PROPOSED | N6 |
| PV-03 | Preview, mail without a text body | このメールには表示できる本文がありません | APPROVED | PROPOSED | |
| PV-04 | Preview, show remote images button | 画像を表示 | APPROVED | PROPOSED | |
| PV-05 | Preview, next to PV-04 | 画像は表示されていません。 | APPROVED | NEW | |
| PV-06 | Modal OK, WebView2 missing | HTML表示ができません。詳しい方に相談してください。 | APPROVED | PROPOSED | |
| PV-07 | Attachment row buttons | 開く / 保存 | APPROVED | PROPOSED | |
| PV-08 | Attachment row, dangerous marker | 危険なファイルです | APPROVED | PROPOSED | |
| PV-09 | Confirm dialog, saving a dangerous attachment | このファイルはパソコンに害を与える可能性があります。保存しますか? | APPROVED | PROPOSED | |
| PV-10 | Buttons of PV-09 | 保存する / キャンセル | APPROVED | NEW | default キャンセル |
| PV-11 | Attachment row, sizes | KB / MB | APPROVED | NEW | |
| PV-12 | Preview when no mail is selected | 左の一覧からメールを選んでください | APPROVED | NEW | text given by the user (Q62) |
| PV-13 | Preview while rows are checked | N通のメールを選んでいます。「削除」を押すと、まとめてごみ箱に移します。 | APPROVED | NEW | A60 |
| PV-14 | Preview header, attachments heading | 添付 | APPROVED | NEW | |

## 7. Safety warnings

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| SW-01 | Preview top, red banner | このメールには、あやしいリンクが含まれています。リンクを開かないでください。 | APPROVED | PROPOSED | |
| SW-02 | Link confirm dialog, title | リンク先の確認 | APPROVED | NEW | |
| SW-03 | Link confirm dialog, text | このリンクは本当のリンク先が違うかもしれません。リンク先: <URL> | APPROVED | NEW | |
| SW-04 | Link confirm dialog, buttons | それでも開く / やめる | APPROVED | PROPOSED | default やめる (spec) |
| SW-05 | Modal OK, scheme refused | このリンクは開けません | APPROVED | PROPOSED | |
| SW-06 | Modal OK, executable attach blocked | このファイルは危険な可能性があるため、添付できません。 | APPROVED | PROPOSED | |
| SW-07 | Reason line for an unreadable zip | 中身を確認できないファイルです。 | APPROVED | NEW | |

## 8. Compose

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| CP-01 | Compose window title | 新しいメール - <account display name> | APPROVED | PROPOSED | N3 (title wording); reply/forward titles: WD-21/22 |
| CP-02 | Compose, field labels | 宛先 / CC / BCC / 件名 / 本文 | APPROVED | PROPOSED | |
| CP-03 | Compose, add BCC button | BCCを追加 | APPROVED | PROPOSED | D14 |
| CP-04 | Compose, buttons | 送信 / 添付 / 削除 (remove one attachment) | APPROVED | PROPOSED | |
| CP-05 | Compose, error line / modal for a bad recipient | 宛先のアドレスを確認してください | APPROVED | PROPOSED | |
| CP-06 | Compose, no recipient | 宛先を入れてください。 | APPROVED | NEW | |
| CP-07 | Modal OK, file too large | ファイルが大きすぎます | APPROVED | SPEC | D16, Q42; plus CP-08 |
| CP-08 | Same modal, second line | あと約2.3MB減らしてください | APPROVED | PROPOSED | example; number now in raw-file terms (encoded overage / 1.37, A69) |
| CP-09 | Modal OK, whole message too large at Send | メールが大きすぎます。添付ファイルを減らしてください。 | APPROVED | PROPOSED | |
| CP-10 | Confirm dialog, empty subject | 件名が空です。このまま送りますか? | APPROVED | PROPOSED | A15 |
| CP-11 | Buttons of CP-10 | このまま送る / 戻る | APPROVED | PROPOSED | default 戻る |
| CP-12 | Close dialog, button | 下書きに保存 | APPROVED | SPEC | Q20 |
| CP-13 | Close dialog, button | 保存しないで閉じる(前に自動保存された下書きは残ります) | APPROVED | SPEC | Q20, Q37 |
| CP-14 | Close dialog, button | 戻る | APPROVED | SPEC | |
| CP-15 | Close dialog, message | 書きかけのメールがあります。 | APPROVED | NEW | |
| CP-16 | File picker title | ファイルを選ぶ | APPROVED | NEW | |
| CP-17 | Compose, attachment list label | 添付: | APPROVED | NEW | |
| CP-18 | Compose, button beside each of 宛先 / CC / BCC | アドレス帳から選ぶ | APPROVED | NEW | Q80-Q85 |
| CP-19 | Address-book picker, hint line | 入れたい人にチェックを付けてください | APPROVED | NEW | Q81 |
| CP-20 | Address-book picker, error line when nothing is checked | 入れたい人を選んでください。 | APPROVED | NEW | Q81 |
| CP-21 | Address-book picker, tag on a row whose address is already in the field | 入力ずみ | APPROVED | NEW | Q83 |
| CP-22 | Notice (modal, one OK) when the address book is empty | アドレス帳にまだ登録されていません。先にアドレス帳で登録してください。 | APPROVED | NEW | Q81; title via WD-10 |

## 9. Reply and forward

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| RF-01 | Reply subject prefix | Re: | APPROVED | PROPOSED | A22 |
| RF-02 | Forward subject prefix | Fwd: | APPROVED | PROPOSED | A22 |
| RF-03 | Reply body, first quote line | yyyy年M月d日 H:mm、<送信者名> さんは書きました: | APPROVED | PROPOSED | Q70 |
| RF-04 | Forward body, header block | ---------- 転送メッセージ ---------- followed by 差出人: / 日時: / 件名: / 宛先: | APPROVED | PROPOSED | Q70 |
| RF-05 | Modal OK in the new compose window, files dropped | 次のファイルは添付できませんでした: <file names> | APPROVED | PROPOSED | |
| RF-06 | Same, not downloaded while offline | 一部の添付ファイルはまだ保存されていないため、付けられませんでした。 | APPROVED | PROPOSED | |
| RF-07 | Prefixes recognized for de-duplication (not displayed) | Re: / RE: / 返信: / Fwd: / Fw: / 転送: | APPROVED | PROPOSED | |

## 10. Outbox

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| OB-01 | Banner after 3 failures (red, with ×) | メールを送れませんでした。送信トレイを確認してください。 | APPROVED | PROPOSED | Q22 |
| OB-02 | Outbox row marker | 送信できませんでした | APPROVED | PROPOSED | same as ML-03 |
| OB-03 | Outbox right-click menu | 今すぐ送る | APPROVED | PROPOSED | name given by the user in Q63 |
| OB-04 | Outbox right-click menu | 下書きに戻す | APPROVED | PROPOSED | name given by the user in Q63 |
| OB-05 | Modal OK, Send now failed | まだ送れませんでした。しばらくしてからもう一度お試しください。 | APPROVED | PROPOSED | N9 |
| OB-06 | Modal OK, message too large (should not occur) | メールが大きすぎて送れません | APPROVED | PROPOSED | |
| OB-07 | Busy text after Send now | 送っています… | APPROVED | NEW | |

## 11. Folder actions and context menus

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| FA-01 | Right-click menu | 返信 / 全員に返信 / 転送 | APPROVED | PROPOSED | N4 |
| FA-02 | Inbox right-click menu | 既読にする / 未読にする | APPROVED | PROPOSED | one item |
| FA-03 | Inbox right-click menu | 迷惑メールにする | APPROVED | PROPOSED | no undo notice (Q63) |
| FA-04 | Right-click menu | 削除 | APPROVED | PROPOSED | |
| FA-05 | Inbox right-click menu | アドレス帳に追加 | APPROVED | PROPOSED | |
| FA-06 | Inbox right-click menu | 受信拒否に追加 | APPROVED | PROPOSED | N1 |
| FA-07 | Drafts right-click menu | 編集 | APPROVED | PROPOSED | Q63 |
| FA-08 | Spam right-click menu | 迷惑メールではない | APPROVED | PROPOSED | suffix 「(受信トレイへ)」 dropped by the user (Q76) |
| FA-09 | Trash right-click menu | 元のフォルダに戻す | APPROVED | PROPOSED | N5b; changed by the user on 2026-10-12 (Q87) from 「受信トレイに戻す」 because Restore now returns to the folder the mail was deleted from (Q86) |
| FA-10 | Block-sender dialog, options | このアドレスだけ / このドメイン全体 | APPROVED | PROPOSED | default = address |
| FA-11 | Transient notice after blocking | 受信拒否に追加しました | APPROVED | PROPOSED | |
| FA-12 | Modal OK, sender already in the address book | すでに登録されています | APPROVED | PROPOSED | |
| FA-13 | Delete notice | (see BN-07, BN-11) | APPROVED | SPEC | |

## 12. Address book

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| AB-01 | Window title | アドレス帳 - <account display name> | APPROVED | NEW | |
| AB-02 | List columns and edit fields | 名前 / ふりがな / メールアドレス / メモ | APPROVED | NEW | N7 |
| AB-03 | List window buttons | 追加 / 編集 / 削除 / 閉じる | APPROVED | PROPOSED | 閉じる is new |
| AB-04 | Confirm dialog, delete a person | この人を削除しますか? [削除] [やめる] | APPROVED | PROPOSED | default やめる |
| AB-05 | Edit dialog, hint under ふりがな | 並べ替えに使います | APPROVED | PROPOSED | |
| AB-06 | Edit dialog, error line, duplicate address | このアドレスはすでに登録されています | APPROVED | PROPOSED | Q70 |
| AB-07 | Edit dialog, error line, empty name | 名前を入れてください。 | APPROVED | NEW | |
| AB-08 | Edit dialog, titles | 人を追加 / 人を編集 | APPROVED | NEW | |
| AB-09 | Edit dialog, error line, bad address | メールアドレスを正しく入れてください。 | APPROVED | NEW | |
| AB-10 | Edit dialog, error line, no address | メールアドレスを1つ以上入れてください。 | APPROVED | NEW | |
| AB-11 | Edit dialog, address row buttons | アドレスを増やす / この行をはずす | APPROVED | NEW | |
| AB-12 | Edit dialog, buttons | 保存 / やめる | APPROVED | NEW | |
| AB-13 | List, when empty | まだ登録されていません | APPROVED | NEW | |

## 13. Block list

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| BL-01 | Window title | 受信拒否リスト - <account display name> | APPROVED | PROPOSED | N1 (the name; the account suffix follows the other windows) |
| BL-02 | List columns and kinds | 種類 / 値; アドレス, ドメイン | APPROVED | PROPOSED | |
| BL-03 | Window buttons | 追加 / 削除 / 閉じる | APPROVED | PROPOSED | 閉じる is new |
| BL-04 | Confirm dialog, remove an entry | この受信拒否を解除しますか? [解除する] [やめる] | APPROVED | PROPOSED | default やめる |
| BL-05 | Modal OK / error line, duplicate | すでに登録されています | APPROVED | PROPOSED | |
| BL-06 | Add form, kind options and value label | このアドレスだけ / このドメイン全体; アドレスまたはドメイン | APPROVED | NEW | |
| BL-07 | Add form, error line | アドレスまたはドメインを正しく入れてください。 | APPROVED | NEW | |
| BL-08 | List, when empty | 受信拒否はありません | APPROVED | NEW | |
| BL-09 | Add form, hint | ドメインを入れると、その下のアドレスもすべて受信拒否になります | APPROVED | NEW | Q48 |

## 14. Filter rules

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| FR-01 | Window title | 振り分けルール - <account display name> | APPROVED | PROPOSED | N2 (the name) |
| FR-02 | Edit dialog, action drop-down | 迷惑メールへ移動 / ごみ箱へ移動 / 既読にする | APPROVED | PROPOSED | |
| FR-03 | List columns | 有効 / 条件の説明 / 動作 | APPROVED | PROPOSED | |
| FR-04 | Rule description in the list | 差出人に『abc』を含む かつ 件名に『xyz』を含む → ごみ箱へ | APPROVED | PROPOSED | example |
| FR-05 | Window buttons | 追加 / 編集 / 削除 / 上へ / 下へ / 閉じる | APPROVED | PROPOSED | 閉じる is new |
| FR-06 | Confirm dialog, delete a rule | このルールを削除しますか? [削除] [やめる] | APPROVED | NEW | default やめる |
| FR-07 | Edit dialog, labels | 差出人に含む文字 / 件名に含む文字 / 本文に含む文字 / 動作 | APPROVED | NEW | |
| FR-08 | Edit dialog, titles | ルールを追加 / ルールを編集 | APPROVED | NEW | |
| FR-09 | Edit dialog, error line, no condition | 条件を1つ以上入れてください。 | APPROVED | NEW | |
| FR-10 | Edit dialog, error line, no action | 動作を選んでください。 | APPROVED | NEW | |
| FR-11 | List, when empty | ルールはありません | APPROVED | NEW | |
| FR-12 | Edit dialog, hint at the top | 条件をすべて満たすメールに使います | APPROVED | NEW | A26 |
| FR-13 | Edit dialog, buttons | 保存 / やめる | APPROVED | NEW | |

## 15. Search

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| SR-01 | Search box action | 検索 | APPROVED | PROPOSED | |
| SR-02 | Clear button | クリア | APPROVED | PROPOSED | D20 |
| SR-03 | Search box placeholder | 差出人・件名・本文から探す | APPROVED | NEW | |
| SR-04 | Result row, folder label for All-Mail-only | すべてのメール | APPROVED | PROPOSED | |
| SR-05 | Note under the box while results are shown | まだ保存されていない古いメールの本文は検索されません。 | APPROVED | PROPOSED | Q30 requires the UI to say so; wording draft |
| SR-06 | Note when more than 500 results | 結果が多いため一部のみ表示しています | APPROVED | PROPOSED | Q70 |
| SR-07 | List, no results | 見つかりませんでした | APPROVED | NEW | |

## 16. Quit and shutdown

| ID | Where shown | Japanese text | Status | Origin | Notes |
|---|---|---|---|---|---|
| QT-01 | Quit dialog text | 送信待ちのメールがあります。このまま閉じると送信されません | APPROVED | SPEC | Q43 |
| QT-02 | Quit dialog button 1 | 送信してから閉じる | APPROVED | SPEC | |
| QT-03 | Quit dialog button 2 | このまま閉じる | APPROVED | SPEC | |
| QT-04 | Quit dialog button 3 (default) | 戻る | APPROVED | SPEC | |
| QT-05 | Windows shutdown block reason | 送信待ちのメールがあります | APPROVED | PROPOSED | shown by Windows |
| QT-06 | Busy text while sending before closing | 送っています… | APPROVED | NEW | |

## 17. Decided naming and wording (approved by the user, 2026-10-08)

| # | Topic | Decision (Japanese text as shown on screen) | Affects |
|---|---|---|---|
| N1 | Block list term | Bottom button of the list pane and window title: 「受信拒否リスト」; right-click menu item: 「受信拒否に追加」 | TB-18, FA-06, BL-01 |
| N2 | Filter rules term | Bottom button and window title: 「振り分けルール」 | TB-19, FR-01 |
| N3 | Compose button | Bottom button of the list pane and compose window title: 「新しいメール」 | TB-14, CP-01 |
| N4 | Header buttons of the preview | 「返信」 / 「全員に返信」 / 「転送」 / 「削除」 (unchanged) | TB-20, FA-01 |
| N5 | Preview zoom reset button | 「100%に戻す」 (was 「元に戻す」) | TB-21 |
| N5b | Trash right-click menu item that moves a mail back to the folder it was deleted from | 「元のフォルダに戻す」 (2026-10-12, Q87; earlier 「受信トレイに戻す」, before that 「元に戻す(受信トレイへ)」) | FA-09 |
| N5c | Delete undo notice button | 「元に戻す」 stays (fixed by the spec: 「ごみ箱に移動しました [元に戻す]」) | BN-07 |
| N6 | Notice in the mail header when the sender is not in the address book | 「アドレス帳に登録されていません」 | PV-02 |
| N7 | Reading field name in the address book (column and edit field) | 「ふりがな」 (was 「よみがな」) | AB-02 |
| N8 | Technical terms (IMAP, アプリパスワード, pt) | Kept as written | BN-03, AC-01, AC-06, ST-03 |
| N9 | Tone gaps | Next-step sentence added only to: offline move refusal 「今はインターネットにつながっていないため操作できません。つながってからもう一度お試しください。」 and Outbox "send now" failure 「まだ送れませんでした。しばらくしてからもう一度お試しください。」. The login-failed (AC-06) and network-error (AC-07) messages stay as written | BN-05, OB-05, AC-06, AC-07 |
| N10 | Account buttons | Each shows its account name only (no hint or arrow); the selected one adds a leading check mark symbol (not a string); badge only on the non-selected one (Issue #1, Q91) | TB-09, TB-10 |
| N11 | Q31 warning text | 「この操作は詳しい方に相談してから行ってください。」 (unchanged) | AC-13 |
| N12 | Preview guide text with no mail selected (Q62) | 「左の一覧からメールを選んでください」 | PV-12 |
| N13 | Extra header button in Drafts (Q63) | 「編集」; the Outbox/Spam/Trash actions 「今すぐ送る」「下書きに戻す」「迷惑メールではない」「元のフォルダに戻す」 are right-click only | TB-24, OB-03, OB-04 |
| N14 | Button of every notice the user must read (Q64) | 「OK」 | BN-12 |
| N15 | Multi-delete notice (Q71) | 「N通をごみ箱に移動しました [元に戻す]」, 10 seconds | BN-11 |
| N16 | Approved in the PLANs (Q70) | Reply quote line, forward header block, 「このアドレスはすでに登録されています」, 「結果が多いため一部のみ表示しています」 | RF-03, RF-04, AB-06, SR-06 |
| N17 | All other strings | Every remaining row was approved in the follow-up review; FA-08 reads 「迷惑メールではない」 | all rows |

## 18. Windows and dialogs: titles and buttons

Buttons are listed left to right as drawn (safe choice last). "Enter" = the key that activates a button; "Esc" always cancels. All rows are APPROVED (Q76). Layouts: `docs/plans/window-layouts.md`.

| ID | Window / dialog | Title | Buttons (default marked *) | Status | Notes |
|---|---|---|---|---|---|
| WD-01 | Main window | Parent_Mail | none | APPROVED | |
| WD-02 | Settings | 設定 | 閉じる* | APPROVED | modal |
| WD-03 | Account-add (also first launch) | アカウントを追加 | 登録する*, やめる | APPROVED | Enter = 登録する |
| WD-04 | Password re-entry | パスワードの入れ直し | 保存する*, やめる | APPROVED | |
| WD-05 | Rename | 名前を変える | 保存*, やめる | APPROVED | no warning dialog before it (Q79) |
| WD-06 | Warning before add / delete / re-entry | 確認 | 続ける, キャンセル* | APPROVED | text AC-13 (APPROVED) |
| WD-07 | Delete account confirm | アカウントの削除 | 削除する, やめる* | APPROVED | text AC-27 |
| WD-08 | Account delete blocked | アカウントを削除できません | OK* | APPROVED | text AC-20 |
| WD-09 | First-launch close confirm | アプリを終わりますか | 終わる, 戻る* | APPROVED | text AC-19 |
| WD-10 | Generic notice (failures/refusals) | お知らせ | OK* | APPROVED | text per message |
| WD-11 | Address book | アドレス帳 - <name> | 追加, 編集, 削除, 閉じる | APPROVED | modal; Enter on a row = 編集 |
| WD-12 | Person add/edit | 人を追加 / 人を編集 | 保存*, やめる | APPROVED | |
| WD-13 | Delete person confirm | 人の削除 | 削除, やめる* | APPROVED | text AB-04 |
| WD-14 | Block list | 受信拒否リスト - <name> | 追加, 削除, 閉じる | APPROVED | the name is APPROVED (N1) |
| WD-15 | Remove block entry confirm | 受信拒否の解除 | 解除する, やめる* | APPROVED | text BL-04 |
| WD-16 | Block sender (from the right-click menu) | 受信拒否に追加 | 追加*, やめる | APPROVED | options FA-10; default option = address |
| WD-17 | Filter rules | 振り分けルール - <name> | 追加, 編集, 削除, 上へ, 下へ, 閉じる | APPROVED | the name is APPROVED (N2) |
| WD-18 | Rule add/edit | ルールを追加 / ルールを編集 | 保存*, やめる | APPROVED | |
| WD-19 | Delete rule confirm | ルールの削除 | 削除, やめる* | APPROVED | text FR-06 |
| WD-20 | Compose | 新しいメール - <name> | 添付, 送信 | APPROVED | title APPROVED (N3); no Enter/Send shortcut |
| WD-21 | Compose, reply | 返信 - <name> | 添付, 送信 | APPROVED | |
| WD-22 | Compose, forward | 転送 - <name> | 添付, 送信 | APPROVED | |
| WD-23 | Compose close dialog | 書きかけのメールがあります | 下書きに保存*, 保存しないで閉じる(前に自動保存された下書きは残ります), 戻る | APPROVED | button texts SPEC; default = 下書きに保存 (Q75) |
| WD-24 | Empty subject | 件名が空です | このまま送る, 戻る* | APPROVED | |
| WD-25 | Quit with Outbox mail | 送信待ちのメールがあります | 送信してから閉じる, このまま閉じる, 戻る* | APPROVED | texts APPROVED (SPEC) |
| WD-26 | Suspicious link confirm | リンク先の確認 | それでも開く, やめる* | APPROVED | |
| WD-27 | Dangerous attachment save confirm | 危険なファイル | 保存する, キャンセル* | APPROVED | |
| WD-28 | File picker for attachments | ファイルを選ぶ | (Windows standard) | APPROVED | |
| WD-29 | Save attachment | 名前を付けて保存 | (Windows standard) | APPROVED | |
| WD-30 | Unhandled error | 問題が発生しました | OK* | APPROVED | text BN-04 |
| WD-31 | Compose, address-book picker | アドレス帳から選ぶ - <name> | 入れる*, やめる | APPROVED | modal to the compose window; Enter = 入れる (Q81); buttons reuse AB-12-style wording |
