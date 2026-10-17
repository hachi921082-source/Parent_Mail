# Handoff

Session-handoff file. Read this first in a fresh conversation, then open only the needed sections of other docs.

## Index

1. Status
2. Documents and where decisions live
3. Awaiting user confirmation
4. Known risks accepted by the user
5. Recommended next steps
6. Working notes

## 1. Status

- Phase: implementation. Waves W0-W5 features of `docs/plans/README.md` section 11 (incl. reply-forward and app-lifecycle) are implemented, reviewed by reviewer-lite, and merged into `main` with `--no-ff` (1045 tests, 0 warnings, ~11 s full run). Remaining: release build, final push, Issue #1 (needs designer-lite first) and the open decision on double-click for drafts.
- Spec, window layouts, palette and ALL UI strings are approved (QA.md Q1-Q85). `docs/plans/ui-strings.md` is the source of truth for wording; `Strings.ja.cs` (246 constants) is generated from it by `tools/gen_strings.py`.
- Repository: `main`, private GitHub repo `hachi921082-source/Parent_Mail`. `main` was pushed to `origin/main` on 2026-10-13 (user confirmed). Push again only after the user confirms. No feature branches or worktrees remain.
- Agent set: lite (`designer-lite`, `developer-lite`, `reviewer-lite`); do not mix with the full set (see `CLAUDE.md`).
- Manual gates with the test Gmail account (env vars `PARENTMAIL_TEST_USER` / `PARENTMAIL_TEST_APPPASSWORD` are set in the user's environment; `tools/set-test-account.ps1` sets or clears them; values are never printed or committed):
  - W2 gate passed: account add (wrong password rejected, credential saved), initial sync (40 mails), periodic sync, send-to-self, block and filter rules applied to new Inbox mail. Automated by `tests/.../SyncCache/LiveGmailTests.cs` and `LiveGmailRulesAndSendTests.cs` (run only with `PARENTMAIL_LIVE_TESTS=1`; they clean up the mails they create).
  - W3 gate passed: settings window, mail display, HTML Ctrl+wheel (works), zoom resets per mail, image blocking, send-to-self reaches the Inbox, oversize-attachment warning keeps the compose window in front (DialogService owner fix).
  - W4 gate (2026-10-12) passed: Sent copy, Japanese-named attachment, offline Outbox, delete/undo, multi-delete, spam, search box, drafts, address-book picker, FHD compose window (the title-bar issue was the monitor). Attachment save/open was broken by a DI bug (TryAdd placeholders shadowed the real services; fixed, user re-confirmed save/open works; `RealServiceWiringTests` guards it).
  - W4 gate FINISHED 2026-10-13: the user re-checked restore-to-origin, the blank strip cell and the rest; the dangerous-attachment warning could not be tried on a real mail (Gmail blocks .exe/.reg/.iso attachments), so it is covered only by the 35 unit tests in `MailDisplay/AttachmentTests.cs`. Test dummy files were deleted.
  - Decisions 2026-10-12: Trash restore returns to the pre-delete folder (Q86, `trash_origin` table), menu wording 「元のフォルダに戻す」 (Q87); the bottom-left 検索 button of the list strip is removed and its cell stays EMPTY for future use (Q88). Implemented, committed and re-checked by the user.
  - Delete/move speed: row removal is already optimistic; the notice and the Trash list entry wait for the IMAP move. Only the per-mail destination-folder lookup was cached (`ImapGateway`; helps multi-delete). Each move batch logs `FolderActions` count/moved/ms to `%LOCALAPPDATA%\Parent_Mail\logs\parentmail.log`. User decision: leave as is. If it feels slow later: keep one IMAP connection open (reconnect once on failure; Gmail drops idle connections, PC sleep) or move many mails with one MOVE call.
  - W5 integration pass (2026-10-13/15) done by hand and by live tests: reply / reply all / forward (PDF, text-only mail, quote lines), quit dialog with Outbox (3 buttons, Enter/Esc = 戻る, send on sync/更新), Windows sign-out interception, window restore, sync right after launch, no leftover process, clean-data first launch (Japanese data path via `PARENTMAIL_DATA_ROOT`). Live Gmail test `LiveGmailReplyForwardTests` covers Reply-To, forward attachments incl. the dangerous-attachment notice (.exe is dropped, file name in the notice).
  - Issues fixed in this pass (all merged to `main`): #2 Reply-To stored in cache.db v3 and used for reply; #3 confirm dialogs clipped at large text sizes; #4 closing a compose window with 下書きに保存 no longer waits for the Gmail upload (background upload, bounded 5 s wait at quit); #5 at most ONE compose window (Q89, A77-A81; existing window is only brought to front). Also fixed: settings-window account list updated off the UI thread. Release script: `tools/publish.ps1` (single-file exe `publish/Parent_Mail_YYYYMMDD-HHMMSS.exe`; the process name of that exe includes the date-time, so `tasklist | grep Parent_Mail.exe` does not match it).
  - Done 2026-10-15/16: Issue #6 (double-click on a draft opens it, Q90, A78) and Issue #7 (RFC 2047 encoded recipient names decoded for display) merged into `main`.
  - Known minor risk: if the app quits while a draft upload is in flight longer than 5 s, a duplicate draft can remain in Gmail.
  - Issues #1 and #8 merged into `main` locally 2026-10-16 (NOT pushed; checked by eye by the user 2026-10-17 with 2 real + 3 dummy accounts, all checklist items OK): account buttons side by side (Q91), up to 5 accounts with horizontal scroll arrows (Q92, assumptions A82-A91 are user-vetoable), sequential sync per round, sequential send on quit, settings account list scrolls vertically, Issue #1 focus-loss fix. 1114 tests green, reviewer-lite approved. Visual check list that was used (dummy accounts via a copy of the data root: set `PARENTMAIL_DATA_ROOT` to a temp copy and add rows to its settings.db `accounts`; dummies have no password so sync shows the wrong-password banner): 5-account bar at 1024 px (arrows only on overflow, nothing clipped), arrow scrolling and grey end state, hidden-unread badge + tooltip, selected account auto-scrolls into view, Tab onto hidden button, focus kept after Enter/Space switch, settings list 3 rows + add disabled at 5, 220 DIP width with ellipsis + tooltip. Known low items: first startup sync round runs in registration order (displayed account not yet known); a 16 DIP trailing margin may show an arrow at borderline widths.
  - Fixed 2026-10-17 (`QuitCloseRunner`, merged locally): the main window X needed two clicks because the quit confirmation finished synchronously inside the Closing event and `Close()` threw into an un-awaited Task (later shown as the generic error modal that kept the process alive). Close now runs after Closing returns; quit-time exceptions are logged (type, message, 3 stack frames) and never shown as a modal. User confirmed by hand: one click closes, 戻る keeps the window and the next X asks again, rapid clicks show one dialog.
  - Parked: duplicate Gmail draft after quitting during a long upload (plan if wanted: search Drafts by Message-ID `<draft.N@...>` after APPEND and delete all other UIDs; add `IImapGateway.SearchUidsByMessageIdAsync`); not started, user did not ask for it.
- Decisions of this session worth knowing: address-book picker button beside 宛先/CC/BCC (Q80-Q85, `compose/PLAN-3.md`, implemented); the address book window has NO search box (approved layout; search is only for autocomplete); remote images stay blocked by default and the broken-image look is accepted; password field label reads アプリパスワード（スペースなし）; settings window UI had been missed in W1 and was added as an extra lane in W3.
- Test flakiness fixed: full runs used to fail about 1 in 10-15 (different tests each time) because many fixtures call the global `SqliteConnection.ClearAllPools()` while xUnit ran classes in parallel. `tests/ParentMail.Tests/AssemblyInfo.cs` turns parallelization off (30 consecutive green runs). Re-check if it reappears.
- Tools: `Start-ParentMail.bat` (repo root) builds and starts the app; do not run it while the app is open (the build would fail on locked files); env var `PARENTMAIL_DATA_ROOT` (absolute path) redirects the data root for testing only and must never be set for normal use.

## 2. Documents and where decisions live

- `docs/spec.md` — confirmed spec (8 sections, index at top). Self-contained summary of decisions; read first.
- `docs/detailed-spec.md` — implementation-ready detail in 22 sections; source for PLAN.md files. Section 19 lists the assumptions A1-A76 (A49-A76 CONFIRMED); sections 20-22 = notices/banners/colours, keyboard and window rules, multi-select delete.
- `docs/QA.md` — questions Q1-Q85 recorded; no open items; approved defaults D1-D21.
- `docs/DECISIONS.md` — rationale only (top-bar layout, app password, no encryption, text default, ZoomFactor /12, zoom reset, log file, executable list, Undo scope).
- `docs/images/layout-sketch.png` — main-window sketch (top bar, narrow list, wide preview).
- `docs/plans/window-layouts.md` — approved window layouts and common window rules (settings, account-add, address book, block list, filter rules, compose).
- `docs/plans/mockups/main-window.html` — approved colour/layout mockup of the main window (open in a browser).
- `docs/plans/pending-plan-edits.md` — edit list for developer-lite (PLAN files were not touched by designer-lite).

## 3. Awaiting user confirmation

Nothing pending from the 2026-10-08 review round (Q73-Q78). Two changes made by the user: the mail auto-selected after a delete is NOT marked read (A58); the compose close dialog default is 下書きに保存 (A75).
- A1-A48 in detailed-spec section 19 are assumptions added by designer-lite; the user can veto any.
- A37 confirmed by the user (Q46); A36 replaced: zoom resets to 100% on mail/folder/account change.
- "Unread shown in bold" and the read/unread display were bundled into the text of an answer choice (Q8/Q9) and recorded as adopted; not separately confirmed.
- A41 and A42 are settled (Q45, Q50). Superseded/settled assumptions are marked in detailed-spec section 19 (A10, A11, A28, A36, A38, A41, A45 superseded).

## 4. Known risks accepted by the user

- Q21: Send is immediate, with no confirmation and no undo; wrong recipients cannot be caught (empty-subject warning only).
- Q28/Q35: address book, block list and filter rules are per account (not shared).
- No app-level encryption, no BitLocker requirement; local mail DB is readable by anyone with access to the Windows account/disk.
- Closing the window quits the app; fetching and Outbox retry run only while the app is open (mitigated by the quit warning with unsent mail).

## 5. Recommended next steps

1. W5 per `docs/plans/README.md` section 11 (see 2.). Test discipline: run only the tests of the touched area; the full suite only before merges or after DB-migration / DI-registration changes. The assistant cannot click the UI: the user operates the windows and must close the app (including compose windows) before any build/test; check `tasklist | grep Parent_Mail` first.
2. W5 detail: reply-forward (needs compose PLAN-1 and sync-cache PLAN-3, both merged), app-lifecycle (needs outbox-send and compose PLAN-2, both merged), then the integration pass and the release build `Parent_Mail_YYYYMMDD-HHMMSS.exe`. Full flow check: delete/undo, multi-select, search, drafts, quit with Outbox, Windows sign-out, 2 accounts, Japanese user-folder path.
3. Branch cycle (user decision 2026-10-10; rule in `docs/plans/README.md` sections 8 and 11): `feature/<feature-name>[-n]` per lane; each lane session runs developer-lite then reviewer-lite itself in its own worktree (max 2 fix rounds) and reports; the coordinating session does NOT re-review: it checks build/test and the shared files touched, merges with `--no-ff`, resolves `FeatureRegistrars.cs` conflicts by keeping all lines, builds and tests on `main`, removes the worktree and branch. Only one lane at a time launches the app (single-instance mutex).
4. Lane instruction texts: give each lane ONE self-contained paste-ready block (no separate common part).

## 6. Working notes

- Out-of-scope ideas recommended but not adopted (only the preview zoom was adopted): scam-phrase detection, "help" status button, automatic backup of local settings, read-aloud, settings export/import, error log (the local log file WAS later adopted, Q53).
- External review (Gemini) only sees what it is given and repeated already-decided points three times; give it `spec.md` plus `detailed-spec.md` and `QA.md` together, or skip further rounds.
- Language policy: docs in English; code comments in Japanese; commit bodies in Japanese; final chat reports in Japanese. Build artifacts need a date-time in the file name.
- Paths and file names with Japanese characters must work everywhere (attachments, save dialogs, cache paths).
- Token-saving rules for implementation: one PLAN per session (commit, update this file, end the session); at session start read only this file and the one target PLAN, open `detailed-spec.md` / `ui-strings.md` only by the section numbers and IDs the PLAN cites; build/test with quiet output (`dotnet build -v q`, `dotnet test --nologo -v q`, `| tail -40`); write the PLAN's unit tests first and batch manual app runs at tier boundaries; delegate code search to Explore; parallel features via `parallel-plan-worktrees`.
- `tools/gen_strings.py` generates `src/ParentMail.App/Resources/Strings.ja.cs` from `docs/plans/ui-strings.md` (constants `AB_01` style, SUPERSEDED rows skipped; rows holding several strings such as `送信 / 添付 / 削除` stay one constant). Re-run it instead of copying strings by hand.
