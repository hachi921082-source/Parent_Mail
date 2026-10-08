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

- Phase: specification complete; implementation plans exist (`docs/plans/README.md` + per-feature PLANs); `docs/plans/ui-strings.md` is approved by the user (2026-10-08) and is the source of truth for UI wording. Implementation not started; next = implement per `docs/plans/README.md` section 11 (tier order). The user can supply a test Gmail account for manual verification.
- Repository: `main`, private GitHub repo `hachi921082-source/Parent_Mail`.
- Specs updated 2026-10-08 with the planning decisions (QA.md Q44-Q57); commit state: see `git status`.
- Agent set: lite (`designer-lite`, `developer-lite`, `reviewer-lite`); do not mix with the full set (see `CLAUDE.md`).

## 2. Documents and where decisions live

- `docs/spec.md` — confirmed spec (8 sections, index at top). Self-contained summary of decisions; read first.
- `docs/detailed-spec.md` — implementation-ready detail in 19 sections; source for PLAN.md files. Section 19 lists 48 assumptions (A1-A48).
- `docs/QA.md` — all questions Q1-Q57 resolved; no open items; approved defaults D1-D21.
- `docs/DECISIONS.md` — rationale only (top-bar layout, app password, no encryption, text default, ZoomFactor /12, zoom reset, log file, executable list, Undo scope).
- `docs/images/layout-sketch.png` — main-window sketch (top bar, narrow list, wide preview).

## 3. Awaiting user confirmation

- A1-A48 in detailed-spec section 19 are assumptions added by designer-lite; the user can veto any.
- A37 confirmed by the user (Q46); A36 replaced: zoom resets to 100% on mail/folder/account change.
- "Unread shown in bold" and the read/unread display were bundled into the text of an answer choice (Q8/Q9) and recorded as adopted; not separately confirmed.
- A41 and A42 are settled (Q45, Q50). Superseded/settled assumptions are marked in detailed-spec section 19.

## 4. Known risks accepted by the user

- Q21: Send is immediate, with no confirmation and no undo; wrong recipients cannot be caught (empty-subject warning only).
- Q28/Q35: address book, block list and filter rules are per account (not shared).
- No app-level encryption, no BitLocker requirement; local mail DB is readable by anyone with access to the Windows account/disk.
- Closing the window quits the app; fetching and Outbox retry run only while the app is open (mitigated by the quit warning with unsent mail).

## 5. Recommended next steps

1. Commit the updated docs (one file per commit, role in the message body, Japanese).
2. Implement features per `docs/plans/README.md` section 11 (T0 app-shell and settings first), using the user's test Gmail account for manual checks.
3. Implement in feature branches per `~/.claude/shared/dev-workflow.md` section 7; confirm with the user before merge/push/branch delete.

## 6. Working notes

- Out-of-scope ideas recommended but not adopted (only the preview zoom was adopted): scam-phrase detection, "help" status button, automatic backup of local settings, read-aloud, settings export/import, error log (the local log file WAS later adopted, Q53).
- External review (Gemini) only sees what it is given and repeated already-decided points three times; give it `spec.md` plus `detailed-spec.md` and `QA.md` together, or skip further rounds.
- Language policy: docs in English; code comments in Japanese; commit bodies in Japanese; final chat reports in Japanese. Build artifacts need a date-time in the file name.
- Paths and file names with Japanese characters must work everywhere (attachments, save dialogs, cache paths).
