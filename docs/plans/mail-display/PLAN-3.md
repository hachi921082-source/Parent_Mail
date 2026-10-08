# mail-display PLAN-3: attachment list, save, open

Planning status: complete. Spec: detailed-spec 6, 14; A17; Q25.

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Attachment list under the preview body, save and open actions, executable handling on receipt (rules from safety-warnings PLAN-2).

## 2. Behavior

- List shown below the body when the message has non-inline attachments: each row = file icon, name, size (KB/MB), buttons 「開く」 and 「保存」. Font follows `AppFontSize`.
- Executable check (`ExecutableChecker.Check(fileName, content?)`, PLAN-2) runs when the list is built using the file name; for `.zip` it needs the file content, so the zip is checked after it is downloaded (the row shows the plain name until then; opening a zip first downloads it, checks, and applies the result).
- Flagged attachment: row shows a red 「危険なファイルです」 marker; 「開く」 is disabled (opening forbidden); 「保存」 asks for confirmation: strong warning dialog 「このファイルはパソコンに害を与える可能性があります。保存しますか?」, default = キャンセル, then standard Save dialog.
- Normal attachment: 「保存」 uses `Microsoft.Win32.SaveFileDialog` with the original file name (Japanese-safe); 「開く」 copies the cached file to a temp folder (`%LOCALAPPDATA%\Parent_Mail\temp\<guid>\<original name>`) and starts it with the Windows default app (`UseShellExecute`); the temp folder is cleaned at app start.
- Download via `IAttachmentService.EnsureLocalAsync` (sync-cache PLAN-3) with busy state; offline messages as defined there.
- Inline images (cid) are not listed.

## 3. Functions

- `AttachmentListViewModel { Items, OpenCommand, SaveCommand }`, `AttachmentItem { Id, FileName, SizeText, IsDangerous }`.
- `AttachmentActions.OpenAsync(item)`, `SaveAsync(item)`, `CleanTemp()`.
- File-name sanitizer for the temp copy (replace invalid path characters, keep non-ASCII).

## 4. Tests and done criteria

- Unit: dangerous attachments block Open and require confirmation to Save; file-name sanitizer keeps Japanese; zip content check after download.
- Manual: save/open a PDF with a Japanese name; an `.exe` test attachment cannot be opened; a zip containing `.exe` is flagged.
