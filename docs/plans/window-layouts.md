# Window Layouts (APPROVED 2026-10-08)

Status: APPROVED by the user on 2026-10-08 (every sketch, size and common rule in this file; Q73, Q75). Compose close dialog default = 下書きに保存 (Q75). Once approved, it is the source for the XAML of each window. Japanese UI text comes from `docs/plans/ui-strings.md` (wording there wins on any difference). A colour mockup of the main window is in `docs/plans/mockups/main-window.html`.

ASCII sketches are schematic: Japanese characters are wider than the drawing grid, so only the order and grouping matter. Sizes are in DIP at the default 18pt (1 pt = 4/3 DIP, so 18pt = 24 DIP per full-width character); they grow with the text-size setting except where capped (section 1.3).

## Index

1. Common rules
2. Main window
3. Common dialogs (notice, confirm)
4. Settings
5. Account-add (and first launch)
6. Password re-entry, rename, warning dialog (not used for rename)
7. Address book and edit dialog
8. Block list and add form
9. Filter rules and edit dialog
10. Compose (new mail)
11. Open points for the user

## 1. Common rules

### 1.1 Behaviour of every window and dialog

| Rule | Value |
|---|---|
| Modality | Every dialog and every window opened from the main window (settings, address book, block list, filter rules, their edit dialogs, warnings) is MODAL to its owner. Compose windows are the exception: independent, several can be open. |
| Position | Centered on the owner window (first-launch account-add: centered on the screen). |
| Enter | Activates the SAFE default button. In confirm dialogs the safe button is the cancel-type one (戻る / やめる / キャンセル). In form dialogs (add/edit) Enter activates 保存 (it only saves valid input; errors are shown, nothing destructive). In a multi-line field Enter inserts a new line. |
| Esc | Always cancels / closes the dialog (same as the cancel-type button). Not bound in compose (see 10). |
| Button row | Right-aligned. Order left to right: the less safe / primary action first, the SAFE choice LAST (rightmost). Example: [続ける] [キャンセル]; [送信してから閉じる] [このまま閉じる] [戻る]. |
| Single-button notice | One big 「OK」 button, right-aligned, default. |
| Default focus | Main window: the mail list. Compose: the 宛先 field. Dialogs: the default button (form dialogs: the first input field; Enter still activates 保存). |
| Shortcut keys | F5 = refresh (same as 更新). Delete key = Delete action ONLY while the mail list has keyboard focus. No keyboard shortcut for Send. Ctrl+mouse wheel over the preview = zoom (decided, Q69). No Ctrl+0, Ctrl+plus, Ctrl+minus. |
| Focus indicator | Thick (3 px) visible focus rectangle, colour `#0B4FA8` or white on dark buttons. |
| Mouse | Right-click context menu rows have the same size as buttons (min height 48 DIP). |

### 1.2 Sizes (DIP)

| Item | Size |
|---|---|
| Button height | >= 56 (grows with text: line height + 24) |
| Button width | >= 120; padding 24 horizontal |
| Any clickable target (checkbox, link, icon button, list row) | >= 48 x 48 |
| Text box / drop-down height | >= 56 |
| Gap between neighbouring controls | 16 (label to its field: 8) |
| Dialog inner margin | 24 |
| Dialog width at 18pt | 520 (notice/confirm), 720 (forms). Dialogs size to content, maximum 90 percent of the work area, then scroll. |
| Text | Follows the text-size setting (12-48pt), EXCEPT top bar and list-pane bottom strip (1.3) |

### 1.3 Text-size cap for the top bar and bottom button strip (decided: capped; value approved)

Decided by the user: top-bar and bottom-strip text is capped while mail list, preview and body text still follow the setting up to 48pt. The user suggested 28pt. Measured against the minimum window (1024 wide, about 1008 DIP usable):

| Cap | 6 folder buttons in one row (needs / available about 960) | Result |
|---|---|---|
| 28pt (37 DIP per character) | about 1,140 | does NOT fit even with account button moved to its own row |
| 24pt (32 DIP) | about 1,030 | does not fit |
| 22pt (29 DIP) | about 960 | borderline, no margin |
| 20pt (27 DIP) | about 900 | fits with margin |

Proposal: cap = 20pt (constant `ChromeTextMaxPt`). Below 20pt the setting applies as is (default 18pt). Button HEIGHT still grows to the minimum 56 DIP. The list-pane bottom strip uses the same cap and wraps a long label onto two lines (e.g. 受信拒否リスト) instead of overflowing. If the user prefers a bigger cap, the top bar would have to wrap folder buttons onto two rows, which costs list space at 700 height.

## 2. Main window

Minimum size 1024x700. First launch: maximized, list pane about 35 percent of the width (decided, Q58); afterwards the last window size/state and splitter position are restored.

### 2.1 Top bar: two rows (approved)

At 1024 wide one row cannot hold account button + 6 folders + HTML checkbox + refresh even at 18pt (needs about 1,230 DIP), so the bar has two fixed rows. Reading order is unchanged.

```
Banner area (0 to 2 banners, section 2.3)
+----------------------------------------------------------------------------+
| [ 母のメール (3) ]                                  [ ] HTMLで表示  [ 更新 ] |  row 1
| [受信トレイ (12)] [送信トレイ (!1)] [送信済み] [ごみ箱] [下書き] [迷惑メール] |  row 2
+--------------------------+-+-----------------------------------------------+
| [検索ボックス....] [クリア] | |  [返信] [全員に返信] [転送]  [拡大][縮小][100%に戻す]   [削除] |
| [ ] すべて選ぶ             | |  差出人 / 宛先 / CC / 件名 / 日時 (+ notices)  |
| [ ] 山田太郎   14:30 clip   | |  -------------------------------------------- |
|     来週の予定について      | |  (red suspicious-link banner, when needed)     |
| [ ] 佐藤花子   10月8日 9:12 | |                                                |
|     お礼                    | |  Body text (plain by default)                  |
|  ...                        | |                                                |
| [新しいメール] [検索]       | |                                                |
| [設定]  [アドレス帳]        | |        (transient notice: bottom of preview)   |
| [受信拒否リスト][振り分けルール] | |                                            |
+--------------------------+-+-----------------------------------------------+
         list ~35%         splitter            preview ~65%
```

- Row 1: account switch button at the left (name only, small red number badge when the other account has unread mail); HTML checkbox 「HTMLで表示」 and 「更新」 at the right.
- Row 2: the six folder buttons in the spec order. Inbox unread count and Outbox failure count are shown as red badges inside the button (white text on `#A80000`).
- The active folder button is drawn pressed (thick border + blue fill).

### 2.2 List pane (top to bottom)

1. Search box + 「クリア」 (always visible).
2. List header row (48 DIP): 「すべて選ぶ」 checkbox (shown only in Inbox, Sent, Spam, Drafts and search results). When rows are checked the row also shows 「N通を選んでいます」 and 「選択をやめる」.
3. Rows (virtualized). Each row, minimum height 2 text lines + 16:
   - Left: checkbox, 48 x 48 hit target, ALWAYS shown (not shown in Trash and Outbox, approved).
   - Line 1: sender (display name, else address) left, ellipsis when long; date/time right-aligned; paperclip icon after the date when the mail has attachments. Sent/Drafts: 「宛先: <first recipient> ほか N 人」 in the sender position.
   - Line 2: subject, ellipsis when long.
   - Unread = bold on both lines (no dot, no colour). Selected row = fill `#D6E4F7` + 6 px left bar `#0B4FA8`.
4. Bottom button strip: 2 columns x 3 rows, each cell >= 56 DIP high, text capped (1.3), wrapping allowed. Order (row by row): 新しいメール / 検索 / 設定 / アドレス帳 / 受信拒否リスト / 振り分けルール. 新しいメール is drawn as the primary (blue) button.

At the 1024x700 minimum the list shows only about 2.5 rows (top bar 136 + search 64 + header 48 + strip about 200); on a 1080p maximized window about 8 rows. If this is too tight, the fallback is a 3 x 2 strip with shorter wrap; the user decides after seeing the mockup.

### 2.3 Banners (Q64; order and count approved)

- Region: above the top bar, full width. Maximum 2 banners at a time. Each banner >= 56 DIP high, bold text, optional action button at the right.
- Priority (higher shown first; lower ones wait until a higher one disappears when 2 are already shown): 1 wrong password, 2 offline, 3 Outbox failure, 4 initial sync (「メールを読み込んでいます…」), 5 others (missing Gmail folder, generic sync error).
- Close button 「×」 (48 x 48): ONLY on the Outbox failure banner. The others disappear when their condition ends.
- The suspicious-link red banner is NOT part of this stack: it sits at the top of the mail inside the preview pane.
- Failures the user must read and refusals = modal dialog with one 「OK」 button (section 3). Successes = transient notice at the bottom of the preview pane (4 seconds; delete notice 10 seconds with 「元に戻す」).

### 2.4 Preview pane

- Header buttons row: 返信, 全員に返信, 転送 on the left; zoom group (拡大, 縮小, 100%に戻す, percent) in the middle; 削除 at the FAR RIGHT, apart from the others, in red-outlined style (approved, to avoid pressing it instead of 返信/転送). Drafts folder: an extra large 「編集」 button after the left group (decided). 迷惑メールではない, 受信トレイに戻す, 今すぐ送る, 下書きに戻す stay right-click only (decided).
- Header block: 差出人, 宛先, CC (only when present, approved), 件名, 日時, then notices (「アドレス帳に登録されていません」, image hint). Attachments list below.
- No mail selected: guide text 「左の一覧からメールを選んでください」 centered (decided).
- Several rows checked: guide text 「N通のメールを選んでいます。「削除」を押すと、まとめてごみ箱に移します。」, header buttons other than 削除 disabled (approved).

### 2.5 Tab order (main window)

Initial focus = mail list. Tab order: account switch, HTML checkbox, 更新, six folder buttons, search box, クリア, すべて選ぶ, mail list (rows: Up/Down, Space toggles the check), bottom strip (6 buttons, row by row), preview header buttons (返信, 全員に返信, 転送, [編集], 拡大, 縮小, 100%に戻す, 削除), HTML preview/plain text, attachment buttons. Shift+Tab reverses.

## 3. Common dialogs

Notice (failure or refusal, one button):
```
+----------------------------------------------+
| お知らせ                                      |
|                                              |
|  今はインターネットにつながっていないため     |
|  操作できません。つながってからもう一度       |
|  お試しください。                            |
|                                              |
|                                   [   OK   ] |   default, Enter/Esc close
+----------------------------------------------+
```
Confirm (two buttons, safe last):
```
|  このまま閉じるとアプリは終わります。よろしいですか? |
|                          [ 終わる ] [ 戻る(既定) ]    |
```
Three buttons: quit with Outbox mail = leftmost riskiest, 戻る last and default; compose close = 下書きに保存 is the default (Q75).

Title is always filled in (see `ui-strings.md` section 18). Close box (X) = same as Esc.

## 4. Settings (modal, 720 wide)

```
+------------------------------------------------------------+
| 設定                                                        |
|  文字の大きさ                                                |
|  [ 18 ] pt   [-----------o--------]   (12 - 48)            |
|  +------------------------------------------------------+   |
|  | これは文字の大きさの見本です。 (live sample)         |   |
|  +------------------------------------------------------+   |
|  アカウント                                                  |
|  +------------------------------------------------------+   |
|  | 母のメール   mother@gmail.com                          |   |
|  |   [名前を変える] [パスワードを入れ直す] [削除]         |   |
|  | 父のメール   father@gmail.com                          |   |
|  |   [名前を変える] [パスワードを入れ直す] [削除]         |   |
|  +------------------------------------------------------+   |
|  [アカウントを追加]  (disabled with 「アカウントは2つまでです」 at 2) |
|                                                  [ 閉じる ]  |
+------------------------------------------------------------+
```
- Field order: text-size number box, slider, then per account row: 名前を変える, パスワードを入れ直す, 削除; then アカウントを追加; then 閉じる.
- Tab order = field order. Initial focus = the number box. Enter = 閉じる; Esc = 閉じる (changes apply live, so closing keeps them).
- Invalid number: 「12から48の数字を入れてください。」 under the box, the last valid value is kept.
- パスワードを入れ直す / 削除 / アカウントを追加 go through the warning dialog (section 6) first. 名前を変える does NOT (Q79).

## 5. Account-add (also first launch)

```
+------------------------------------------------------------+
| アカウントを追加                                             |
|  Gmailのアプリパスワードが必要です。                         |
|  メールアドレス   [______________________________]           |
|  アプリパスワード [______________________________] (hidden)  |
|  表示名           [______________________________]           |
|  (error line, red, appears here)                              |
|                                  [ 登録する ] [ やめる ]    |
+------------------------------------------------------------+
```
- Field order: メールアドレス, アプリパスワード, 表示名 (defaults to the part before @), 登録する, やめる. Initial focus = メールアドレス. Enter in a field = 登録する (validate first); Esc = やめる.
- While testing the login: all fields disabled, button text 「確認しています…」.
- First launch (no account yet): the window is centered on the screen, not preceded by the warning dialog. Closing it without adding (やめる, Esc or X) shows the confirm 「このまま閉じるとアプリは終わります。よろしいですか?」 with [終わる] [戻る(既定)]. Wording under review.

## 6. Password re-entry, rename, warning dialog (not used for rename)

Password re-entry (opened by the app after a wrong-password banner, or by パスワードを入れ直す after the warning dialog):
```
+--------------------------------------------------+
| パスワードの入れ直し                              |
|  母のメール (mother@gmail.com)                    |
|  アプリパスワード [____________________] (hidden)  |
|  (error line)                                     |
|                          [ 保存する ] [ やめる ]  |
+--------------------------------------------------+
```
Field order: アプリパスワード, 保存する, やめる. Initial focus = the field. Enter = 保存する; Esc = やめる. The login test runs before saving.

Rename: opens directly, NO warning dialog (Q79). Title 「名前を変える」, field 「表示名」 (pre-filled, selected), [ 保存 ] [ やめる ]. Initial focus = the field; Enter = 保存; Esc = やめる.

Warning dialog before add/delete/password re-entry only (Q31; not rename): text 「この操作は詳しい方に相談してから行ってください。」, buttons [ 続ける ] [ キャンセル(既定) ]. Account delete: if the account has Outbox mail, NO warning dialog follows; a notice dialog stops the deletion (text in `ui-strings.md`, AC-20).

## 7. Address book (modal, 900 x 600 at 18pt) and edit dialog

```
+----------------------------------------------------------------------+
| アドレス帳 - 母のメール                                                |
|  名前        ふりがな     メールアドレス          メモ                 |
| +------------------------------------------------------------------+ |
| | 山田 太郎    やまだ たろう  taro@example.com      弟               | |
| | 佐藤 花子    さとう はなこ  hanako@example.jp     友人              | |
| +------------------------------------------------------------------+ |
|  [ 追加 ] [ 編集 ] [ 削除 ]                              [ 閉じる ]  |
+----------------------------------------------------------------------+
```
Sorted by ふりがな (kana order; empty reading after, by name). Tab order: list, 追加, 編集, 削除, 閉じる. Initial focus = list; Enter on a row = 編集; Esc = 閉じる. Delete: confirm 「この人を削除しますか?」 [ 削除 ] [ やめる(既定) ].

Edit dialog (modal over the book):
```
+--------------------------------------------------+
| 人を追加 / 人を編集                                |
|  名前            [__________________]  (required) |
|  ふりがな        [__________________]  並べ替えに使います |
|  メールアドレス  [__________________] [この行をはずす] |
|                  [アドレスを増やす]                  |
|  メモ            [__________________]  (multi-line) |
|  (error line)                                       |
|                           [ 保存 ] [ やめる ]       |
+--------------------------------------------------+
```
Field order: 名前, ふりがな, メールアドレス (rows), アドレスを増やす, メモ, 保存, やめる. Initial focus = 名前. Enter (not in メモ) = 保存; Esc = やめる.

## 8. Block list (modal, 720 x 560) and add form

```
+------------------------------------------------------+
| 受信拒否リスト - 母のメール                            |
|  種類        値                                       |
| +--------------------------------------------------+ |
| | アドレス    spam@example.com                       | |
| | ドメイン    bad.example                            | |
| +--------------------------------------------------+ |
|  追加する:  (o) このアドレスだけ  ( ) このドメイン全体  |
|             [______________________________] [ 追加 ] |
|  (error line)                                         |
|  [ 削除 ]                                   [ 閉じる ] |
+------------------------------------------------------+
```
Field order: list, kind radio (2), value box, 追加, 削除, 閉じる. Initial focus = the value box. Enter in the value box = 追加; Esc = 閉じる. 削除 asks 「この受信拒否を解除しますか?」 [ 解除する ] [ やめる(既定) ]. A domain entry covers its subdomains (stated under the box in small text: approved 「ドメインを入れると、その下のアドレスもすべて受信拒否になります」).

## 9. Filter rules (modal, 900 x 600) and edit dialog

```
+----------------------------------------------------------------------+
| 振り分けルール - 母のメール                                            |
|  有効  条件の説明                                       動作           |
| +------------------------------------------------------------------+ |
| | [x]  差出人に『abc』を含む かつ 件名に『xyz』を含む → ごみ箱へ     | |
| | [ ]  件名に『お知らせ』を含む → 既読にする                        | |
| +------------------------------------------------------------------+ |
|  [ 追加 ] [ 編集 ] [ 削除 ] [ 上へ ] [ 下へ ]            [ 閉じる ]   |
+----------------------------------------------------------------------+
```
Tab order: list (the 有効 check is toggled with Space or click), 追加, 編集, 削除, 上へ, 下へ, 閉じる. Initial focus = list; Enter on a row = 編集; Esc = 閉じる. 削除 asks 「このルールを削除しますか?」 [ 削除 ] [ やめる(既定) ].

Edit dialog:
```
+--------------------------------------------------+
| ルールを追加 / ルールを編集                        |
|  条件をすべて満たすメールに使います                |
|  差出人に含む文字 [__________________]             |
|  件名に含む文字   [__________________]             |
|  本文に含む文字   [__________________]             |
|  動作             [ 迷惑メールへ移動 v ]            |
|  (error line)                                       |
|                           [ 保存 ] [ やめる ]       |
+--------------------------------------------------+
```
Field order: 差出人に含む文字, 件名に含む文字, 本文に含む文字, 動作, 保存, やめる. Initial focus = 差出人に含む文字. Enter = 保存; Esc = やめる. At least one condition and an action are required (the error line explains instead of silently disabling 保存; approved change from the PLAN's disabled button).

## 10. Compose (new mail; reply and forward are the same window), non-modal, 960 x 720 at 18pt

```
+----------------------------------------------------------------------+
| 新しいメール - 母のメール                                              |
|  宛先   [___________________________________________]  (autocomplete)|
|  CC     [___________________________________________]                |
|  [BCCを追加]      (BCC [____] appears here after the click)           |
|  件名   [___________________________________________]                |
|  本文   +--------------------------------------------------+         |
|         |                                                  |         |
|         |  (plain text, multi-line)                        |         |
|         +--------------------------------------------------+         |
|  添付:  [report.pdf  1.2MB  [削除]] [photo.jpg 0.8MB [削除]]          |
|  (error / over-limit line)                                            |
|  [ 添付 ]                                             [   送信   ]   |
+----------------------------------------------------------------------+
```
- Field order and tab order: 宛先, CC, BCCを追加 (then BCC when shown), 件名, 本文, 添付 files' 削除 buttons, 添付, 送信. Tab inside 本文 leaves the field (Tab is not inserted as text).
- Initial focus = 宛先 (for reply/forward also 宛先 is skipped only when pre-filled? approved: initial focus stays 宛先 for new mail; for reply the cursor goes to 本文 above the quote; for forward 宛先).
- 送信 is a large button at the bottom right (the only primary action). NO keyboard shortcut for Send; Enter in a single-line field moves to the next field (does not send). Esc is not bound (closing goes through the close dialog).
- Drag and drop of files onto any part of the window adds attachments.
- Closing with content: three-button dialog [ 下書きに保存(既定) ] [ 保存しないで閉じる(前に自動保存された下書きは残ります) ] [ 戻る ]. No content: closes without a dialog.
- Empty subject at send: [ このまま送る ] [ 戻る(既定) ].

## 11. Open points

None. All points were approved on 2026-10-08. Compose close dialog default = 下書きに保存 (Q75); chrome text cap 20pt, two-row top bar and 2 x 3 bottom strip approved (Q73).
