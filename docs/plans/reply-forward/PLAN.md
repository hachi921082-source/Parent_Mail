# reply-forward

Planning status: complete. Spec: detailed-spec 8; A22, A56; Q70; D14; PQ26. Wording: `ui-strings.md` (IDs in brackets). Window titles [WD-21] 「返信 - <name>」, [WD-22] 「転送 - <name>」; layout = compose window (compose PLAN-1).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Builds `ComposeInit` for reply, reply all and forward from a cached message and opens the compose window (compose PLAN-1).

## 2. Behavior

- Account = the displayed account. Source body = `body_text`, else `derived_text`. If the source body is not downloaded and offline, the action is refused with modal notice [BN-23] 「このメールはまだ保存されていないため、返信や転送ができません。インターネットにつながってからもう一度お試しください。」 (title 「お知らせ」, button 「OK」); online, it is fetched first.
- Reply: To = Reply-To header if present, else From. Subject = [RF-01] `Re: ` + original subject unless it already starts with `Re:` (case-insensitive, also `RE:`/`返信:`). Quote (priority-2 default): first line [RF-03] `yyyy年M月d日 H:mm、<送信者名> さんは書きました:` then each original line prefixed with `> `; blank line above the quote and the cursor placed above it.
- Reply all: To = Reply-To/From; CC = original To + CC minus the account's own address and minus duplicates (case-insensitive).
- Forward: Subject = [RF-02] `Fwd: ` + subject (skip if present as `Fwd:`/`Fw:`/`転送:`). Body = blank lines then [RF-04] `---------- 転送メッセージ ----------`, From/Date/Subject/To lines, blank line, original text unquoted. To empty.
- Forward attachments (PQ26): non-inline attachments are fetched (`EnsureLocalAsync`), then each goes through `ExecutableChecker` and the size estimator exactly as the Attach button; violators are dropped and one modal notice [RF-05] 「次のファイルは添付できませんでした: <file names>」 is shown over the new compose window; undownloaded while offline: modal notice [RF-06] 「一部の添付ファイルはまだ保存されていないため、付けられませんでした。」 and continue without them.
- Reply threading headers: `In-Reply-To` = original Message-ID, `References` = original References + Message-ID, passed via `ComposeInit.ReplyHeaders` and written by the send pipeline.
- Header buttons and context menu items call these builders (folder-actions wires enablement).

## 3. Functions

- `ReplyBuilder.BuildReply(MailMessage, accountEmail, replyAll)`, `ForwardBuilder.BuildForwardAsync(MailMessage, ct)` returning `ComposeInit` plus `ForwardNotices`.
- `SubjectPrefixer.Add(prefixKind, subject)`.
- `QuoteFormatter.Quote(text)`.

## 4. Tests and done criteria

- Unit: prefix de-duplication, reply-all own-address removal, Reply-To handling, quote formatting with Japanese, forward drops flagged/oversized/undownloaded attachments, References chain.
- Manual: reply, reply all, forward of a mail with a PDF and of an HTML-only mail.

## Change request (Q89, 2026-10-14): single compose window

User decided (Issue #5): at most ONE compose window at a time; a repeat request only restores (if minimized) and activates the open one, no notice (detailed-spec section 7, A77-A81). developer-lite must change:
- Before building `ComposeInit` (and before fetching forward attachments), call the compose factory's `TryActivateExisting()` (compose PLAN-1 change request); if it activated a window, stop silently (no notice).
- Test: reply/reply all/forward with a compose window open activates it and builds nothing.
