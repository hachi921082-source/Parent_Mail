# reply-forward

Planning status: complete. Spec: detailed-spec 8; A22; D14; Q26.

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Builds `ComposeInit` for reply, reply all and forward from a cached message and opens the compose window (compose PLAN-1).

## 2. Behavior

- Account = the displayed account. Source body = `body_text`, else `derived_text`. If the source body is not downloaded and offline, the action is refused with the not-downloaded notice (sync-cache PLAN-3); online, it is fetched first.
- Reply: To = Reply-To header if present, else From. Subject = `Re: ` + original subject unless it already starts with `Re:` (case-insensitive, also `RE:`/`返信:`). Quote (P2 default): first line `yyyy年M月d日 H:mm、<送信者名> さんは書きました:` then each original line prefixed with `> `; blank line above the quote and the cursor placed above it.
- Reply all: To = Reply-To/From; CC = original To + CC minus the account's own address and minus duplicates (case-insensitive).
- Forward: Subject = `Fwd: ` + subject (skip if present as `Fwd:`/`Fw:`/`転送:`). Body = blank lines then `---------- 転送メッセージ ----------`, From/Date/Subject/To lines, blank line, original text unquoted. To empty.
- Forward attachments (Q26): non-inline attachments are fetched (`EnsureLocalAsync`), then each goes through `ExecutableChecker` and the size estimator exactly as the Attach button; violators are dropped and one notice 「次のファイルは添付できませんでした: <file names>」 is shown in the new compose window; undownloaded while offline: notice 「一部の添付ファイルはまだ保存されていないため、付けられませんでした。」 and continue without them.
- Reply threading headers: `In-Reply-To` = original Message-ID, `References` = original References + Message-ID, passed via `ComposeInit.ReplyHeaders` and written by the send pipeline.
- Header buttons and context menu items call these builders (folder-actions wires enablement).

## 3. Functions

- `ReplyBuilder.BuildReply(MailMessage, accountEmail, replyAll)`, `ForwardBuilder.BuildForwardAsync(MailMessage, ct)` returning `ComposeInit` plus `ForwardNotices`.
- `SubjectPrefixer.Add(prefixKind, subject)`.
- `QuoteFormatter.Quote(text)`.

## 4. Tests and done criteria

- Unit: prefix de-duplication, reply-all own-address removal, Reply-To handling, quote formatting with Japanese, forward drops flagged/oversized/undownloaded attachments, References chain.
- Manual: reply, reply all, forward of a mail with a PDF and of an HTML-only mail.
