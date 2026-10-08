# mail-display PLAN-3: attachment list, save, open

Planning status: complete. Spec: detailed-spec 6, 14, 20; A17, A55; Q25, Q64. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Attachment list under the preview body, save and open actions, executable handling on receipt (rules from safety-warnings PLAN-2).

## 2. Behavior

- List shown below the body when the message has non-inline attachments: each row = file icon, name, size (KB/MB), buttons [PV-07] 「開く」 and 「保存」; sizes shown as KB/MB [PV-11]. Font follows `AppFontSize`.
- Executable check (`ExecutableChecker.Check(fileName, content?)`, PLAN-2) runs when the list is built using the file name; for `.zip` it needs the file content, so the zip is checked after it is downloaded (the row shows the plain name until then; opening a zip first downloads it, checks, and applies the result).
- Flagged attachment: row shows a red [PV-08] 「危険なファイルです」 marker (unreadable zip: reason line [SW-07] 「中身を確認できないファイルです。」); 「開く」 is disabled (opening forbidden); 「保存」 asks for confirmation: strong warning dialog [WD-27] (title 「危険なファイル」, text [PV-09] 「このファイルはパソコンに害を与える可能性があります。保存しますか?」, buttons [PV-10] 保存する / キャンセル*, modal, Enter = キャンセル), then standard Save dialog [WD-29] 「名前を付けて保存」.
- Normal attachment: 「保存」 uses `Microsoft.Win32.SaveFileDialog` with the original file name (Japanese-safe); 「開く」 copies the cached file to a temp folder (`%LOCALAPPDATA%\Parent_Mail\temp\<guid>\<original name>`) and starts it with the Windows default app (`UseShellExecute`); the temp folder is cleaned at app start.
- Download via `IAttachmentService.EnsureLocalAsync` (sync-cache PLAN-3) with busy state. Failures are modal notices (title 「お知らせ」, button 「OK」) because they follow a user action: not downloaded while offline [BN-09]; download failed [BN-14]; save failed [BN-15]; open failed [BN-16].
- Inline images (cid) are not listed.

## 3. Functions

- `AttachmentListViewModel { Items, OpenCommand, SaveCommand }`, `AttachmentItem { Id, FileName, SizeText, IsDangerous }`.
- `AttachmentActions.OpenAsync(item)`, `SaveAsync(item)`, `CleanTemp()`.
- File-name sanitizer for the temp copy (replace invalid path characters, keep non-ASCII).

## 4. Tests and done criteria

- Unit: dangerous attachments block Open and require confirmation to Save; file-name sanitizer keeps Japanese; zip content check after download.
- Manual: save/open a PDF with a Japanese name; an `.exe` test attachment cannot be opened; a zip containing `.exe` is flagged.
