# UI Strings

Status: approved by user, 2026-10-08 (section 17 decisions applied). Source of truth for UI wording.

Single list of every user-visible Japanese string referenced by `docs/plans/**/PLAN*.md`, for user review before implementation. Once approved, this file is the source of truth and `Strings.ja.cs` is generated from it (PLANs keep their inline wording as drafts; on any difference, this file wins).

Column "Origin": **SPEC** = quoted in spec/detailed-spec/QA (fixed, only change on user request); **A2** = folder names listed as assumption A2 (approved unless vetoed); **PROPOSED** = wording written by developer-lite, needs review; **NEW** = string needed by the UI but not yet written in any PLAN, wording proposed here.
Tone rule (detailed-spec 17): polite plain Japanese, no technical terms, one sentence on what to do next; problems the parent cannot fix say to ask a knowledgeable person (詳しい方).

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

## 1. Top bar and shell

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| TB-01 | Folder button Inbox | 受信トレイ | A2 | detailed-spec 1 | |
| TB-02 | Folder button Outbox | 送信トレイ | A2 | detailed-spec 1 | |
| TB-03 | Folder button Sent | 送信済み | A2 | detailed-spec 1 | |
| TB-04 | Folder button Trash | ごみ箱 | A2 | detailed-spec 1 | |
| TB-05 | Folder button Drafts | 下書き | A2 | detailed-spec 1 | |
| TB-06 | Folder button Spam | 迷惑メール | A2 | detailed-spec 1 | |
| TB-07 | Inbox button with unread | 受信トレイ (N) | PROPOSED | mail-list | N = unread count; hidden when 0 |
| TB-08 | Outbox button with failed items | 送信トレイ (!N) | NEW | outbox-send | N = failed count, red; A40 |
| TB-09 | Account switch button | <display name> (user-chosen account name) | NEW | accounts-auth PLAN-1 | account name only, decided (section 17) |
| TB-10 | Account switch badge | N (number only) | NEW | accounts-auth PLAN-1 | A44 |
| TB-11 | HTML checkbox | HTMLで表示 | NEW | mail-display PLAN-1 | spec only says "HTML/plain-text checkbox" |
| TB-12 | Refresh button | 更新 | NEW | sync-cache PLAN-2 | |
| TB-13 | Refresh button while syncing | 更新中… | NEW | sync-cache PLAN-2 | busy state |
| TB-14 | List-pane bottom: compose | 新しいメール | PROPOSED | mail-list | decided, section 17 |
| TB-15 | List-pane bottom: search | 検索 | PROPOSED | mail-list | |
| TB-16 | List-pane bottom: settings | 設定 | PROPOSED | mail-list | |
| TB-17 | List-pane bottom: address book | アドレス帳 | PROPOSED | mail-list | |
| TB-18 | List-pane bottom: block list | 受信拒否リスト | PROPOSED | mail-list | name mismatch, see section 17 |
| TB-19 | List-pane bottom: filter rules | 振り分けルール | PROPOSED | mail-list | name mismatch, see section 17 |
| TB-20 | Preview header buttons | 返信 / 全員に返信 / 転送 / 削除 | PROPOSED | folder-actions | spec names Reply, Reply all, Forward, Delete |
| TB-21 | Preview zoom buttons | 拡大 / 縮小 / 100%に戻す | PROPOSED | mail-display PLAN-2 | decided, section 17 |
| TB-22 | Preview zoom percentage | 150% (example) | PROPOSED | mail-display PLAN-2 | |

## 2. Banners, notices, global errors

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| BN-01 | Offline banner | インターネットにつながっていません。保存済みのメールは読めます。 | PROPOSED | sync-cache PLAN-2 | required by spec (offline banner); wording proposed |
| BN-02 | Initial sync banner | メールを読み込んでいます… | PROPOSED | sync-cache PLAN-2, app-shell | named in the Q23 answer; tone ok |
| BN-03 | Missing special-use Gmail folder | Gmailの設定でIMAPの表示を確認してください。詳しい方に相談してください。 | PROPOSED | sync-cache PLAN-1 | contains technical term "IMAP"; see section 17 |
| BN-04 | Unhandled error dialog | 問題が発生しました。もう一度お試しください。直らない場合は詳しい方に連絡してください。 | PROPOSED | app-shell | |
| BN-05 | Move refused while offline | 今はインターネットにつながっていないため操作できません。つながってからもう一度お試しください。 | PROPOSED (decided) | folder-actions, sync-cache PLAN-2 | |
| BN-06 | Move failed | 操作できませんでした。もう一度お試しください。 | PROPOSED | folder-actions | |
| BN-07 | Delete notice | ごみ箱に移動しました [元に戻す] | SPEC | detailed-spec 9 (Q40) | fixed; shown 5 s |
| BN-08 | Not downloaded yet (body) | このメールはまだ保存されていません。インターネットにつながると自動で表示します。 | PROPOSED | sync-cache PLAN-3 | spec requires a "not downloaded yet" message |
| BN-09 | Not downloaded yet (attachment) | この添付ファイルはまだ保存されていません。インターネットにつながってから開いてください。 | PROPOSED | sync-cache PLAN-3 | |
| BN-10 | Body being fetched | 読み込んでいます… | PROPOSED | sync-cache PLAN-3 | |

## 3. Accounts

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| AC-01 | Account-add screen notice | Gmailのアプリパスワードが必要です。 | NEW | accounts-auth PLAN-1 | spec: must state a Gmail app password is required; add 「わからないときは詳しい方に相談してください。」? |
| AC-02 | Account-add field labels | メールアドレス / アプリパスワード / 表示名 | NEW | accounts-auth PLAN-1 | A6 |
| AC-03 | Account-add button | 登録する | NEW | accounts-auth PLAN-1 | |
| AC-04 | Add disabled at 2 accounts | アカウントは2つまでです | PROPOSED | accounts-auth PLAN-1 | A4 |
| AC-05 | Duplicate address | このメールアドレスは登録済みです | PROPOSED | accounts-auth PLAN-1 | |
| AC-06 | Login failed | ログインできませんでした。アプリパスワードを確認してください。 | PROPOSED | accounts-auth PLAN-1 | parent cannot fix: consider 「詳しい方に確認してもらってください。」 |
| AC-07 | Network error on add | 接続できませんでした。ネットワークを確認してください。 | PROPOSED | accounts-auth PLAN-1 | |
| AC-08 | Add: busy | 確認しています… | NEW | accounts-auth PLAN-1 | during login test |
| AC-09 | Settings: rename | 名前を変える | PROPOSED | accounts-auth PLAN-2 | |
| AC-10 | Settings: re-enter password | パスワードを入れ直す | PROPOSED | accounts-auth PLAN-2 | |
| AC-11 | Settings: delete account | 削除 | PROPOSED | accounts-auth PLAN-2 | |
| AC-12 | Settings: add account | アカウントを追加 | PROPOSED | accounts-auth PLAN-2 | |
| AC-13 | Q31 warning dialog text | この操作は詳しい方に相談してから行ってください。 | SPEC (content), wording PROPOSED | detailed-spec 2 (Q31) | spec fixes content (ask a knowledgeable person), default Cancel |
| AC-14 | Q31 dialog buttons | 続ける / キャンセル | PROPOSED | accounts-auth PLAN-2 | default = キャンセル (spec) |
| AC-15 | Delete dialog extra line | 送信待ちのメールも消えます | PROPOSED | accounts-auth PLAN-2 | only when Outbox has mail |
| AC-16 | Wrong/revoked password banner | パスワードが使えなくなりました。詳しい方にパスワードの入れ直しを頼んでください。 | PROPOSED | accounts-auth PLAN-2 | spec requires plain notice + ask a knowledgeable person |
| AC-17 | Wrong-password banner action | 入れ直す | PROPOSED | accounts-auth PLAN-2 | |
| AC-18 | Password re-entry screen title/field | パスワードの入れ直し / アプリパスワード | NEW | accounts-auth PLAN-2 | |

## 4. Settings

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| ST-01 | Settings window title | 設定 | NEW | settings | |
| ST-02 | Text size label | 文字の大きさ | NEW | settings | |
| ST-03 | Unit shown next to size | pt | NEW | settings | "ポイント" is an alternative |
| ST-04 | Live sample text | これは文字の大きさの見本です。 | NEW | settings | D21 live sample |
| ST-05 | Accounts section heading | アカウント | NEW | settings | |
| ST-06 | Invalid size input | 12から48の数字を入れてください。 | NEW | settings | |

## 5. Mail list

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| ML-01 | Empty folder | メールはありません | PROPOSED | mail-list | |
| ML-02 | Sent/Drafts row sender column | 宛先: <recipient> | PROPOSED | mail-list | |
| ML-03 | Outbox row failure marker | 送信できませんでした | PROPOSED | mail-list, outbox-send | |
| ML-04 | Date: today | HH:mm (e.g. 14:30) | SPEC (Q15 answer) | README Q15 | |
| ML-05 | Date: this year | M月d日 H:mm (e.g. 10月8日 14:30) | SPEC (Q15 answer) | README Q15 | |
| ML-06 | Date: older | yyyy年M月d日 H:mm (e.g. 2025年10月8日 14:30) | SPEC (Q15 answer) | README Q15 | |
| ML-07 | Attachment icon tooltip | 添付ファイルあり | NEW | mail-list | |

## 6. Preview and display

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| PV-01 | Header labels | 差出人 / 宛先 / 件名 / 日時 | NEW | mail-display PLAN-1 | |
| PV-02 | Not in address book notice | アドレス帳に登録されていません | PROPOSED (decided) | mail-display PLAN-1 | spec: "Not in your address book" (A2 asks for wording) |
| PV-03 | No body | このメールには表示できる本文がありません | PROPOSED | mail-display PLAN-1 | |
| PV-04 | Show remote images | 画像を表示 | PROPOSED | mail-display PLAN-1 | spec: "Show images" action |
| PV-05 | Remote images hint | 画像は表示されていません。 | NEW | mail-display PLAN-1 | shown next to PV-04 when HTML has remote images |
| PV-06 | WebView2 missing | HTML表示ができません。詳しい方に相談してください。 | PROPOSED | mail-display PLAN-1 | |
| PV-07 | Attachment buttons | 開く / 保存 | PROPOSED | mail-display PLAN-3 | |
| PV-08 | Dangerous attachment marker | 危険なファイルです | PROPOSED | mail-display PLAN-3 | |
| PV-09 | Dangerous attachment save confirm | このファイルはパソコンに害を与える可能性があります。保存しますか? | PROPOSED | mail-display PLAN-3 | spec: strong warning, save after confirmation; buttons see PV-10 |
| PV-10 | Save-confirm buttons | 保存する / キャンセル | NEW | mail-display PLAN-3 | default = キャンセル |
| PV-11 | Attachment sizes | KB / MB | NEW | mail-display PLAN-3 | |

## 7. Safety warnings

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| SW-01 | Red banner | このメールには、あやしいリンクが含まれています。リンクを開かないでください。 | PROPOSED | safety-warnings PLAN-1 | spec: red banner, wording not fixed |
| SW-02 | Link confirm dialog title | リンク先の確認 | NEW | safety-warnings PLAN-1 | |
| SW-03 | Link confirm dialog text | このリンクは本当のリンク先が違うかもしれません。リンク先: <URL> | NEW | safety-warnings PLAN-1 | shows the real destination (spec) |
| SW-04 | Link confirm buttons | それでも開く / やめる | PROPOSED | safety-warnings PLAN-1 | default = やめる (spec) |
| SW-05 | Refused scheme | このリンクは開けません | PROPOSED | safety-warnings PLAN-1 | |
| SW-06 | Executable attach blocked | このファイルは危険な可能性があるため、添付できません。 | PROPOSED | safety-warnings PLAN-2, compose | spec: warn and block |
| SW-07 | Unreadable zip reason | 中身を確認できないファイルです。 | NEW | safety-warnings PLAN-2 | shown with SW-06/PV-08 |

## 8. Compose

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| CP-01 | Window title | 新しいメール - <account display name> | PROPOSED | compose PLAN-1 | |
| CP-02 | Field labels | 宛先 / CC / BCC / 件名 / 本文 | PROPOSED | compose PLAN-1 | |
| CP-03 | Add BCC | BCCを追加 | PROPOSED | compose PLAN-1 | D14 |
| CP-04 | Buttons | 送信 / 添付 / 削除 (remove attachment) | PROPOSED | compose PLAN-1 | |
| CP-05 | Invalid recipient | 宛先のアドレスを確認してください | PROPOSED | compose PLAN-1 | |
| CP-06 | No recipient | 宛先を入れてください。 | NEW | compose PLAN-1 | |
| CP-07 | File too large | ファイルが大きすぎます | SPEC | detailed-spec 7 (D16, Q42) | fixed; plus amount line CP-08 |
| CP-08 | Over amount | あと約2.3MB減らしてください | PROPOSED | compose PLAN-1 | example |
| CP-09 | Whole message too large at Send | メールが大きすぎます。添付ファイルを減らしてください。 | PROPOSED | compose PLAN-1 | |
| CP-10 | Empty subject dialog | 件名が空です。このまま送りますか? | PROPOSED | compose PLAN-1 | spec: empty-subject warning (A15) |
| CP-11 | Empty subject buttons | このまま送る / 戻る | PROPOSED | compose PLAN-1 | default = 戻る |
| CP-12 | Close dialog: save | 下書きに保存 | SPEC | detailed-spec 7, 17 (Q20) | fixed |
| CP-13 | Close dialog: discard | 保存しないで閉じる(前に自動保存された下書きは残ります) | SPEC | detailed-spec 7, 17 (Q20, Q37) | fixed |
| CP-14 | Close dialog: cancel | 戻る | SPEC | detailed-spec 17 | fixed |
| CP-15 | Close dialog message | 書きかけのメールがあります。 | NEW | compose PLAN-2 | line above the three buttons |
| CP-16 | Attach dialog | ファイルを選ぶ | NEW | compose PLAN-1 | OpenFileDialog title |

## 9. Reply and forward

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| RF-01 | Reply subject prefix | Re: | PROPOSED (A22) | reply-forward | spec states "Re: " |
| RF-02 | Forward subject prefix | Fwd: | PROPOSED (A22) | reply-forward | spec states "Fwd: " |
| RF-03 | Reply quote header | yyyy年M月d日 H:mm、<送信者名> さんは書きました: | PROPOSED | reply-forward | |
| RF-04 | Forward header line | ---------- 転送メッセージ ---------- | PROPOSED | reply-forward | followed by 差出人: / 日時: / 件名: / 宛先: |
| RF-05 | Forward dropped files | 次のファイルは添付できませんでした: <file names> | PROPOSED | reply-forward | |
| RF-06 | Forward undownloaded files | 一部の添付ファイルはまだ保存されていないため、付けられませんでした。 | PROPOSED | reply-forward | |
| RF-07 | Prefixes recognized when de-duplicating | Re: / RE: / 返信: / Fwd: / Fw: / 転送: | PROPOSED | reply-forward | not displayed; for matching only |

## 10. Outbox

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| OB-01 | Failure banner (after 3 failures) | メールを送れませんでした。送信トレイを確認してください。 | PROPOSED | outbox-send | Q22: plain-language failure notice |
| OB-02 | Row marker | 送信できませんでした | PROPOSED | outbox-send | same as ML-03 |
| OB-03 | Menu: send now | 今すぐ送る | PROPOSED | outbox-send | spec: "Send now" |
| OB-04 | Menu: return to drafts | 下書きに戻す | PROPOSED | outbox-send | spec: "Return to drafts" |
| OB-05 | Send now failed | まだ送れませんでした。しばらくしてからもう一度お試しください。 | PROPOSED (decided) | outbox-send | |
| OB-06 | Message too large (should not occur) | メールが大きすぎて送れません | PROPOSED | outbox-send | |
| OB-07 | Send now busy | 送っています… | NEW | outbox-send | |

## 11. Folder actions and context menus

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| FA-01 | Menu: reply / reply all / forward | 返信 / 全員に返信 / 転送 | PROPOSED | folder-actions | same as TB-20 |
| FA-02 | Menu: mark read/unread | 既読にする / 未読にする | PROPOSED | folder-actions | one item, text depends on state |
| FA-03 | Menu: move to spam | 迷惑メールにする | PROPOSED | folder-actions | |
| FA-04 | Menu: delete | 削除 | PROPOSED | folder-actions | |
| FA-05 | Menu: add sender to address book | アドレス帳に追加 | PROPOSED | folder-actions | |
| FA-06 | Menu: block sender | 受信拒否に追加 | PROPOSED | folder-actions | name mismatch, section 17 |
| FA-07 | Drafts menu: edit | 編集 | PROPOSED | folder-actions | |
| FA-08 | Spam menu: not spam | 迷惑メールではない(受信トレイへ) | PROPOSED | folder-actions | |
| FA-09 | Trash menu: restore | 受信トレイに戻す | PROPOSED (decided) | folder-actions | decided, section 17 |
| FA-10 | Block dialog options | このアドレスだけ / このドメイン全体 | PROPOSED | folder-actions | default = this address |
| FA-11 | Block done | 受信拒否に追加しました | PROPOSED | folder-actions | |
| FA-12 | Already in address book | すでに登録されています | PROPOSED | folder-actions | |
| FA-13 | Delete notice | (see BN-07) | SPEC | | |

## 12. Address book

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| AB-01 | Window title | アドレス帳 - <account display name> | NEW | address-book | |
| AB-02 | Columns | 名前 / ふりがな / メールアドレス / メモ | NEW | address-book | spec: "reading (yomigana)" |
| AB-03 | Buttons | 追加 / 編集 / 削除 | PROPOSED | address-book | |
| AB-04 | Delete confirm | この人を削除しますか? | PROPOSED | address-book | buttons 削除 / やめる, default やめる |
| AB-05 | Reading hint | 並べ替えに使います | PROPOSED | address-book | |
| AB-06 | Duplicate email | このアドレスはすでに登録されています | PROPOSED | address-book | |
| AB-07 | Edit dialog | 名前を入れてください。 / 保存 / やめる | NEW | address-book | |

## 13. Block list

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| BL-01 | Window title | 受信拒否リスト - <account display name> | PROPOSED | block-list | name mismatch, section 17 |
| BL-02 | Columns | 種類 / 値 (e.g. アドレス, ドメイン) | PROPOSED | block-list | |
| BL-03 | Buttons | 追加 / 削除 | PROPOSED | block-list | |
| BL-04 | Remove confirm | この受信拒否を解除しますか? | PROPOSED | block-list | buttons 解除する / やめる, default やめる |
| BL-05 | Duplicate | すでに登録されています | PROPOSED | block-list | |

## 14. Filter rules

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| FR-01 | Window title | 振り分けルール - <account display name> | PROPOSED | filter-rules | name mismatch, section 17 |
| FR-02 | Actions | 迷惑メールへ移動 / ごみ箱へ移動 / 既読にする | PROPOSED | filter-rules | spec: move to Trash, move to Spam, mark as read |
| FR-03 | Columns | 有効 / 条件の説明 / 動作 | PROPOSED | filter-rules | |
| FR-04 | Rule description | 差出人に『abc』を含む かつ 件名に『xyz』を含む → ごみ箱へ | PROPOSED | filter-rules | example |
| FR-05 | Buttons | 追加 / 編集 / 削除 / 上へ / 下へ | PROPOSED | filter-rules | |
| FR-06 | Delete confirm | このルールを削除しますか? | NEW | filter-rules | default やめる |
| FR-07 | Edit dialog labels | 差出人に含む文字 / 件名に含む文字 / 本文に含む文字 / 動作 | NEW | filter-rules | |

## 15. Search

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| SR-01 | Search button / box action | 検索 | PROPOSED | search | |
| SR-02 | Clear button | クリア | PROPOSED | search | spec D20: "Clear" |
| SR-03 | Search box placeholder | 差出人・件名・本文から探す | NEW | search | |
| SR-04 | Folder label for All-Mail-only | すべてのメール | PROPOSED | search | |
| SR-05 | Note: old bodies not searched | まだ保存されていない古いメールの本文は検索されません。 | PROPOSED | search | spec Q30: UI must say so |
| SR-06 | Result cap | 結果が多いため一部のみ表示しています | PROPOSED | search | |
| SR-07 | No results | 見つかりませんでした | NEW | search | |

## 16. Quit and shutdown

| ID | Where shown | Japanese text | Origin | Source | Notes |
|---|---|---|---|---|---|
| QT-01 | Quit warning text | 送信待ちのメールがあります。このまま閉じると送信されません | SPEC | detailed-spec 16 (Q43) | fixed |
| QT-02 | Button 1 | 送信してから閉じる | SPEC | detailed-spec 16 | fixed |
| QT-03 | Button 2 | このまま閉じる | SPEC | detailed-spec 16 | fixed |
| QT-04 | Button 3 (default) | 戻る | SPEC | detailed-spec 16 | fixed |
| QT-05 | Windows shutdown block reason | 送信待ちのメールがあります | PROPOSED | app-lifecycle | shown by Windows |
| QT-06 | Send-then-close busy | 送っています… | NEW | app-lifecycle | |

## 17. Decided naming and wording (approved by the user, 2026-10-08)

| # | Topic | Decision (Japanese text as shown on screen) | Affects |
|---|---|---|---|
| N1 | Block list term | Bottom button of the list pane and window title: 「受信拒否リスト」; right-click menu item: 「受信拒否に追加」 | TB-18, FA-06, FA-10, FA-11, BL-* |
| N2 | Filter rules term | Bottom button and window title: 「振り分けルール」 | TB-19, FR-* |
| N3 | Compose button | Bottom button of the list pane and compose window title: 「新しいメール」 | TB-14, CP-01 |
| N4 | Header buttons of the preview | 「返信」 / 「全員に返信」 / 「転送」 / 「削除」 (unchanged) | TB-20, FA-01 |
| N5 | Preview zoom reset button | 「100%に戻す」 (was 「元に戻す」) | TB-21 |
| N5b | Trash right-click menu item that moves a mail back to the Inbox | 「受信トレイに戻す」 (was 「元に戻す(受信トレイへ)」) | FA-09 |
| N5c | Delete undo notice button | 「元に戻す」 stays (fixed by the spec: 「ごみ箱に移動しました [元に戻す]」) | BN-07 |
| N6 | Notice in the mail header when the sender is not in the address book | 「アドレス帳に登録されていません」 | PV-02 |
| N7 | Reading field name in the address book (column and edit field) | 「ふりがな」 (was 「よみがな」) | AB-02 |
| N8 | Technical terms (IMAP, アプリパスワード, pt) | Kept as written | BN-03, AC-01, AC-06, ST-03 |
| N9 | Tone gaps | Next-step sentence added only to: offline move refusal 「今はインターネットにつながっていないため操作できません。つながってからもう一度お試しください。」 and Outbox "send now" failure 「まだ送れませんでした。しばらくしてからもう一度お試しください。」. The login-failed (AC-06) and network-error (AC-07) messages stay as written | BN-05, OB-05 |
| N10 | Account switch button | Shows the account name only (no hint or arrow) | TB-09 |
| N11 | Q31 warning text | 「この操作は詳しい方に相談してから行ってください。」 (unchanged) | AC-13 |

Count: 156 IDs in sections 1-16 (one ID may hold several closely related strings, e.g. a set of buttons).
