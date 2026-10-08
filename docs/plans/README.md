# Implementation Plans

Each feature's plan lives in `docs/plans/<feature-name>/PLAN.md` (large features: `PLAN-1.md`, `PLAN-2.md`, ... in the same folder). Source: `docs/spec.md`, `docs/detailed-spec.md` (D# / A# refer to those docs; Q# refers to `docs/QA.md`; PQ# are the planning questions of section 9). Approved layouts: `docs/plans/window-layouts.md`; colour mockup: `docs/plans/mockups/main-window.html`; UI wording: `docs/plans/ui-strings.md`. Code comments are written in Japanese; identifiers in English; UI strings in Japanese.

## Index

1. Feature-to-spec map
2. Prerequisites
3. Project layout
4. Technology choices
5. Shared data models
6. Shared constants
7. Shared interfaces
8. Naming and coding rules
9. Decisions log (user answers during planning)
10. Spec changes needing designer-lite update
11. Implementation Order & Progress

## 1. Feature-to-spec map

| Feature | Plans | detailed-spec sections | Assumptions |
|---|---|---|---|
| app-shell | PLAN | 1, 17, 18, 20, 21 | A1-A3, A49, A50, A53-A56; Q58 |
| app-lifecycle | PLAN | 16 | A31, A32; Q52 (A48 settled) |
| accounts-auth | PLAN-1, PLAN-2 | 2 | A4-A8, A31, A44, A72, A73 |
| settings | PLAN | 15, 1 (persistence) | A5, A9, A70 |
| sync-cache | PLAN-1, PLAN-2, PLAN-3 | 3, 4, 5 (offline) | A9, A11, A12, A39, A43, A57; Q51 (A10 superseded) |
| mail-list | PLAN | 1 (list), 3 (view rule), 22 | Q44 (A45 superseded), A52, A58-A65, A67, A68 |
| mail-display | PLAN-1, PLAN-2, PLAN-3 | 6, 22 | A16-A18, A33-A35, A66, A70; Q45, Q46, Q69 (A36, A41 superseded) |
| safety-warnings | PLAN-1, PLAN-2 | 14 | A29, A30 |
| compose | PLAN-1, PLAN-2 | 7 | A12, A15, A19-A21, A42, A46, A69, A71, A74, A75 |
| reply-forward | PLAN | 8 | A22 |
| outbox-send | PLAN | 5 | A13-A15, A40, A46 |
| folder-actions | PLAN | 9, 1 (header buttons), 22 | A3, A51, A59-A65, A76; Q64 (A38 superseded) |
| address-book | PLAN | 10 | A23 |
| block-list | PLAN | 11 | A24, A25 |
| filter-rules | PLAN | 12 | A26, A27 |
| search | PLAN | 13, 22 | Q47 (A28 superseded), A47, A62, A63 |

## 2. Prerequisites

- .NET SDK 10 (`net10.0-windows`). The machine currently has only SDK 8.0.424; the user installs SDK 10 before implementation starts.
- WebView2 Evergreen runtime (already present on the dev machine).
- A dedicated test Gmail account with an app password, provided by the user. Credentials come from environment variables `PARENTMAIL_TEST_USER` / `PARENTMAIL_TEST_APPPASSWORD` or an untracked local file `tests/local.secrets.json` (listed in `.gitignore`); never committed. Used for manual verification only.

## 3. Project layout

```
Parent_Mail.sln
src/ParentMail.App/            WPF app (net10.0-windows): Views, ViewModels, App.xaml, DI composition root
  Features/<Feature>/          Views and ViewModels grouped by feature
  Resources/Strings.ja.cs      all Japanese UI strings (static class Strings)
src/ParentMail.Core/           library (net10.0-windows): no WPF types
  Models/                      shared models (section 5)
  Data/                        SQLite access: SettingsDb, CacheDb, migrations, repositories
  Mail/                        IMAP/SMTP gateways (MailKit), sync engine, send pipeline
  Features/<Feature>/          feature logic (rules, analyzers, formatters)
  Platform/                    CredentialStore (Credential Manager P/Invoke), paths, logging
tests/ParentMail.Tests/        xUnit tests (net10.0-windows)
docs/ ...
```

Runtime data root: `%LOCALAPPDATA%\Parent_Mail\`
- `settings.db`
- `accounts\<accountId>\cache.db`
- `accounts\<accountId>\attachments\<attachmentId>.bin` (stored by numeric ID, never by original file name; original name kept in DB)
- `logs\parentmail.log` (see section 9, Q29)

All paths are built with `Path.Combine` and handled as UTF-16 strings; test with Japanese user/folder names.

## 4. Technology choices

- C# / WPF, `net10.0-windows`, nullable enabled.
- MailKit (IMAP/SMTP), Microsoft.Web.WebView2, Microsoft.Data.Sqlite (hand-written SQL, WAL, `PRAGMA user_version` migrations), CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, HtmlAgilityPack (HTML parsing and derived text), xUnit (+ NSubstitute) for tests.
- Credential Manager through P/Invoke (`CredWrite`/`CredRead`/`CredDelete`), target name `ParentMail/<accountId>`.
- IMAP: polling only (no IDLE); CONDSTORE used when Gmail advertises it, otherwise UID comparison.
- Release build: self-contained single-file exe `Parent_Mail_YYYYMMDD-HHMMSS.exe` (publish script stamps the date-time); no installer.

## 5. Shared data models (namespace `ParentMail.Core.Models`)

- `Account { Id (Guid string), Email, DisplayName, SortOrder }`
- `FolderKind { Inbox, Outbox, Sent, Trash, Drafts, Spam, AllMail }` (Outbox is local only; AllMail is never shown as a view)
- `MailMessage { GmailMsgId (long, X-GM-MSGID), MessageIdHeader, Subject, FromAddress, FromName, To[], Cc[], DateUtc, IsRead, HasAttachments, BodyState (None|Fetched), BodyText, BodyHtml, DerivedText }`
- `MessageFolder { GmailMsgId, Folder, Uid, UidValidity }` (membership; one message, many rows)
- `AttachmentInfo { Id, GmailMsgId, PartId, FileName, ContentType, Size, IsInline, ContentId, LocalPath? }`
- `OutboxItem { Id, AccountId, ToCc/Bcc, Subject, Body, Attachments[], RetryCount, FailureNotified, CreatedUtc, SourceDraftId? }`
- `DraftItem { LocalId, AccountId, fields as OutboxItem, GmailUid?, Dirty }`
- `Contact { Id, AccountId, Name, Reading, Emails[], Memo }`
- `BlockEntry { Id, AccountId, Kind (Address|Domain), Value }`
- `FilterRule { Id, AccountId, SortOrder, Enabled, SenderContains?, SubjectContains?, BodyContains?, Action (MoveToTrash|MoveToSpam|MarkRead) }`
- `AppSettings { TextSizePt (double), WindowWidth/Height/Left/Top/State, SplitterWidth, LastAccountId }`

DB schemas are defined in `sync-cache/PLAN-1.md` (cache.db) and `settings/PLAN.md` (settings.db).

## 6. Shared constants (`ParentMail.Core.Constants`)

| Name | Value |
|---|---|
| SyncIntervalMinutes | 5 |
| DraftAutoSaveSeconds | 30 |
| UndoNoticeSeconds | 10 |
| InitialBodyMonths | 12 |
| DefaultTextSizePt / Min / Max | 18 / 12 / 48 |
| PreviewZoom Min / Max / Step | 1.00 / 3.00 / 0.25 |
| ZoomReferencePt | 12 |
| WebViewZoomMin / Max | 0.25 / 5.0 |
| SendFailureNoticeThreshold | 3 |
| MaxEncodedMessageBytes | 25,000,000 |
| HeaderOverheadBytes | 10,240 |
| MinWindow | 1024 x 700 |
| MaxAccounts | 2 |
| PointToDip | 96/72 |
| ChromeTextMaxPt | 20 (top bar and list-pane bottom strip text cap) |
| TransientNoticeSeconds | 4 |
| PreviewTextMaxPt | 60 (A70: effective preview size = min(pt x zoom, 60)) |
| MaxDeleteBatch | 200 |
| SearchResultCap | 500 |
| FirstLaunchListPaneRatio | 0.35 |
| IdleBackfill (A57) | idle 60 s, 20 bodies per cycle, inline parts <= 5 MB |
| MinButtonHeightDip / MinTargetDip | 56 / 48 |

## 7. Shared interfaces (`ParentMail.Core`)

- `ICredentialStore` (Save/Get/Delete by accountId).
- `IImapGateway` (connect/login test, list folders with special-use, fetch headers/bodies/parts by UID, search UIDs, store flags, move, append, delete, folder status incl. UIDVALIDITY/HIGHESTMODSEQ) — one instance per account; MailKit implementation plus fakes for tests.
- `ISmtpGateway` (Send(OutboxItem)).
- `ISettingsStore` (get/set app settings, window state), `IIncomingRule` (block list, filter rules), `IBodyFetcher` / `IAttachmentService` (on-demand fetch), `IIdleMonitor` (backfill), `IDraftService`, `IOutboxService`.
- `ISettingsDb` / `ICacheDb` factories and repositories (`IAccountRepository`, `IMailRepository`, `IOutboxRepository`, `IDraftRepository`, `IContactRepository`, `IBlockRepository`, `IFilterRepository`).
- `ISyncService` (per account: SyncNowAsync, state, events `SyncCompleted`, `NewMailProcessed`).
- `IAppState` (current account, current folder, selected message; change events).
- `INotifier` (banner show/hide with priority, transient notice with action), `IDialogService` (`ShowNotice` = modal with one 「OK」 button, `Choose`/`Confirm` with large buttons, default and safe button), `IClock`, `IAppLog`.
- `ITextScale` (Text size pt -> DIP, chrome size capped at 20 pt, effective preview size, zoom factors; see settings, mail-display PLAN-2).

## 8. Naming and coding rules

- Namespaces `ParentMail.App.Features.<Feature>` / `ParentMail.Core.Features.<Feature>`; PascalCase types, `_camel` private fields.
- Code comments in Japanese. Commit messages: Japanese body, one file per commit (mutually dependent new file sets may be committed together).
- Every per-account row/table is scoped by accountId (settings.db) or lives in the account's cache.db.
- All UI strings come from `Strings` (Japanese only). `docs/plans/ui-strings.md` is the reviewed list and the source of truth for wording; the whole file is approved (Q76); Japanese wording inside individual PLANs is a draft and the list wins on any difference; notices follow section 17 tone: polite plain Japanese, no technical terms, one sentence on what to do next; problems the parents cannot fix say to ask a knowledgeable person.
- All dialogs use large buttons; dangerous confirmations have Cancel as default. Window rules (modal, Enter/Esc, button order, tab order, initial focus, shortcuts), sizes and the palette come from `window-layouts.md` and app-shell PLAN 3.1-3.4; each window PLAN lists its own field/tab order.
- Never log or store mail content, addresses, subjects, or passwords outside the cache DB/Credential Manager.
- Dates shown in local time.
- Feature branches `feature/<feature-name>` (confirm with the user before first use, per dev-workflow section 7).

## 9. Decisions log (user answers during planning)

PQ = planning questions of this section; Q = questions of `docs/QA.md`.

- PQ1 .NET: net10.0-windows. PQ2 structure: App + Core (incl. Data) + Tests. PQ3 CommunityToolkit.Mvvm + MS DI. PQ4 Microsoft.Data.Sqlite, hand SQL, WAL, user_version.
- PQ5 tests: xUnit unit tests behind IMAP/SMTP interfaces; manual verification with a test Gmail account supplied by the user.
- PQ6 `settings.db` + per-account `cache.db`.
- PQ7 UIDVALIDITY change: drop only the folder's UID mappings, re-resolve via X-GM-MSGID, keep bodies/attachments; re-listed mail is not "new" (no block/filter, no unread-badge inflation).
- PQ8 bodies in DB, attachments as files by ID.
- PQ9 font: true points, WPF FontSize = pt x 96/72.
- PQ10 HTML ZoomFactor = EffectivePt / 12 where EffectivePt = min(textSizePt x zoom, 60) (A70), clamp 0.25-5.0, round 2 decimals (1.5 at default 18pt, so HTML matches plain text). WebView2 built-in zoom shortcuts disabled.
- PQ11 attachment limit: estimated encoded size = sum(ceil(n/3) x 4 x 78/76) + body bytes + 10 KB header <= 25,000,000 bytes; the message shows how far over.
- PQ12 shortener list = code constant (~20 hosts, exact host or subdomain). PQ13 look-alike = `xn--` label, any non-ASCII host, mixed-script label.
- PQ14 banner evaluates both HTML and text parts; non-suspicious links open in the default browser without a dialog; HTML navigation is always intercepted and opened externally; `mailto:` opens compose.
- PQ15 dates: today `H:mm`-style `HH:mm`; this year `M月d日 H:mm`; older `yyyy年M月d日 H:mm`.
- PQ16 shutdown: intercept `SessionEnding`, show the 3-button dialog; "close anyway" lets Windows continue.
- PQ17 unread bold confirmed. Zoom applies only while that mail is displayed; switching mail/folder/account resets to 100%; never persisted.
- PQ18 From = address only. PQ19 compose = plain text (text/plain, UTF-8). PQ20 search = `LIKE` scan, normalize case and width only. PQ21 single instance; second launch brings the existing window to front.
- PQ22 executable list = confirmed list + A30 macros + extras (hta, com, pif, reg, wsf, vbe, jse, msc, dll, cpl, iso, img, plus vhd, vhdx, appx, msix, wsh, wsc, ps1xml, psm1, scf, inf, gadget, application, msp, mst, sct, chm, hlp, ade, adp, mde); zip only, one level; unreadable/encrypted zip is suspicious; case-insensitive last extension, trailing spaces/dots stripped, RTL-override names flagged.
- PQ23 initial sync: banner 「メールを読み込んでいます…」, list fills progressively, UI not blocked.
- PQ24 block domain includes subdomains; filter "sender contains" matches address and display name; case-insensitive.
- PQ25 retry every cycle after 3 failures; failure notice shown once; marker stays.
- PQ26 forward fetches original attachments, applies same executable and size checks, drops violators with a notice; undownloaded offline: notice, continue without.
- PQ27 Restore -> Inbox always; Undo -> the folder the mail was viewed in only (other Gmail labels are not restored; QA.md Q55).
- PQ28 account-add tests IMAP+SMTP login first; duplicate address rejected.
- PQ29 a small local log file with no mail content (details in `app-shell/PLAN.md`).
- PQ30 self-contained single-file exe `Parent_Mail_YYYYMMDD-HHMMSS.exe`, no installer.
- Priority-2 defaults (written into the relevant PLANs): list sorted by date descending, virtualized, no threading; IMAP/SMTP timeouts 30 s; reply quote format and reply-all rules in reply-forward; Outbox quit warning counts both accounts; keyboard/focus/DPI rules in app-shell; address-book sort and duplicate rules; SQLite WAL + migrations; Drafts update = APPEND new + delete old; 12-month window by INTERNALDATE.

## 10. Spec changes needing designer-lite update

Applied 2026-10-08 (QA.md Q44-Q57); see QA.md. Later decisions (Q58-Q78: layouts, palette, banners, multi-select delete, ui-strings) are in spec.md / detailed-spec sections 20-22 and are applied to the PLANs on 2026-10-08.

## 11. Implementation Order & Progress

Tiers group features whose plans have no unmet dependency on a later tier. Features inside one tier can be implemented in parallel worktrees. Dependencies are read from the shared classes each plan calls into.

| Tier | Feature plan | Depends on | Status |
|---|---|---|---|
| T0 | app-shell/PLAN | none | not started |
| T0 | settings/PLAN | app-shell (solution skeleton) | not started |
| T1 | accounts-auth/PLAN-1 | settings (settings.db), app-shell | not started |
| T1 | sync-cache/PLAN-1 | accounts-auth/PLAN-1 (accountId, paths) | not started |
| T2 | sync-cache/PLAN-2 | sync-cache/PLAN-1 | not started |
| T2 | address-book/PLAN | settings, accounts-auth/PLAN-1 | not started |
| T2 | block-list/PLAN | settings, accounts-auth/PLAN-1 | not started |
| T2 | filter-rules/PLAN | settings, accounts-auth/PLAN-1 | not started |
| T2 | safety-warnings/PLAN-1, PLAN-2 | app-shell | not started |
| T3 | mail-list/PLAN | sync-cache/PLAN-1 | not started |
| T3 | mail-display/PLAN-1, PLAN-2 | sync-cache/PLAN-1, settings, safety-warnings/PLAN-1 | not started |
| T3 | sync-cache/PLAN-3 | sync-cache/PLAN-2 | not started |
| T3 | folder-actions/PLAN | mail-list, sync-cache/PLAN-2 | not started |
| T3 | search/PLAN | mail-list, sync-cache/PLAN-1 | not started |
| T3 | outbox-send/PLAN | sync-cache/PLAN-2, accounts-auth/PLAN-1 | not started |
| T4 | mail-display/PLAN-3 | mail-display/PLAN-1, sync-cache/PLAN-3, safety-warnings/PLAN-2 | not started |
| T4 | compose/PLAN-1 | outbox-send, address-book, safety-warnings/PLAN-2 | not started |
| T4 | compose/PLAN-2 | compose/PLAN-1, sync-cache/PLAN-2 | not started |
| T5 | reply-forward/PLAN | compose, mail-display/PLAN-3 | not started |
| T5 | accounts-auth/PLAN-2 | accounts-auth/PLAN-1, settings | not started |
| T5 | app-lifecycle/PLAN | outbox-send, compose/PLAN-2, accounts-auth | not started |

Status values: not started / in progress / review / merged. Update at each feature's merge.

The 2026-10-08 update (multi-select delete, two-row top bar, palette, window layouts) adds work inside existing PLANs only (app-shell, mail-list, folder-actions, mail-display PLAN-1/PLAN-2, search); dependencies between PLANs are unchanged, so the tiers stay as listed. Within T3, mail-list is implemented before folder-actions and search because they use its checkbox state.
