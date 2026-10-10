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

- Phase: specification, window layouts, colour palette and ALL UI strings are fully approved by the user (2026-10-08, QA.md Q58-Q78); no proposals remain. Implementation plans (`docs/plans/README.md` + per-feature PLANs) have `docs/plans/pending-plan-edits.md` Parts A, B and C applied and were self-reviewed on 2026-10-08 (consistency fixes: incoming rules apply to new Inbox mail only, local drafts in the Drafts folder, SessionEnding handling, HTML Ctrl+wheel spike). Spec A43 (Inbox-only incoming rules) is aligned. Next: wave 0 of `docs/plans/README.md` section 11 (wave plan with parallel worktrees). `docs/plans/ui-strings.md` is the source of truth for wording. The user can supply a test Gmail account for manual verification.
- Repository: `main`, private GitHub repo `hachi921082-source/Parent_Mail`.
- Specs updated 2026-10-08 with the planning decisions (QA.md Q44-Q57); commit state: see `git status`.
- Agent set: lite (`designer-lite`, `developer-lite`, `reviewer-lite`); do not mix with the full set (see `CLAUDE.md`).

## 2. Documents and where decisions live

- `docs/spec.md` — confirmed spec (8 sections, index at top). Self-contained summary of decisions; read first.
- `docs/detailed-spec.md` — implementation-ready detail in 22 sections; source for PLAN.md files. Section 19 lists the assumptions A1-A76 (A49-A76 CONFIRMED); sections 20-22 = notices/banners/colours, keyboard and window rules, multi-select delete.
- `docs/QA.md` — questions Q1-Q78 recorded; no open items; approved defaults D1-D21.
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

1. Spike (first task of the mail-display lane, W3): Ctrl+wheel capture over the WebView2 HTML view (mail-display PLAN-2); if it fails, HTML mode keeps buttons only and the spec is updated.
2. Implement per the wave plan in `docs/plans/README.md` section 11 (revised 2026-10-10; details there): W0 skeleton + contracts (interfaces, models, fakes, generated `Strings.ja.cs`, DI registrar and MainWindow slots; serial, one worktree) -> W1 app-shell / settings / sync-cache PLAN-1 / safety-warnings (4 lanes) -> W2 accounts-auth PLAN-1, sync-cache PLAN-2, outbox-send, address-book, then block-list + filter-rules -> W3 mail-list, mail-display PLAN-1+2, sync-cache PLAN-3, accounts-auth PLAN-2, compose PLAN-1 -> W4 folder-actions, search, mail-display PLAN-3, compose PLAN-2 -> W5 reply-forward, app-lifecycle, integration pass. Max 4 active worktrees; one owner per hotspot file; manual integration gates after W2, W3, W5 with the user's test Gmail account. .NET SDK 10.0.401 is installed; the test account/app password is still needed by W2.
3. Implement in feature branches per `~/.claude/shared/dev-workflow.md` section 7; confirm with the user before merge/push/branch delete.

## 6. Working notes

- Out-of-scope ideas recommended but not adopted (only the preview zoom was adopted): scam-phrase detection, "help" status button, automatic backup of local settings, read-aloud, settings export/import, error log (the local log file WAS later adopted, Q53).
- External review (Gemini) only sees what it is given and repeated already-decided points three times; give it `spec.md` plus `detailed-spec.md` and `QA.md` together, or skip further rounds.
- Language policy: docs in English; code comments in Japanese; commit bodies in Japanese; final chat reports in Japanese. Build artifacts need a date-time in the file name.
- Paths and file names with Japanese characters must work everywhere (attachments, save dialogs, cache paths).
- Token-saving rules for implementation: one PLAN per session (commit, update this file, end the session); at session start read only this file and the one target PLAN, open `detailed-spec.md` / `ui-strings.md` only by the section numbers and IDs the PLAN cites; build/test with quiet output (`dotnet build -v q`, `dotnet test --nologo -v q`, `| tail -40`); write the PLAN's unit tests first and batch manual app runs at tier boundaries; delegate code search to Explore; parallel features via `parallel-plan-worktrees`.
- `tools/gen_strings.py` generates `src/ParentMail.App/Resources/Strings.ja.cs` from `docs/plans/ui-strings.md` (constants `AB_01` style, SUPERSEDED rows skipped; rows holding several strings such as `送信 / 添付 / 削除` stay one constant). Re-run it instead of copying strings by hand.
