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

- Phase: specification complete; implementation not started. No source code, no `docs/plans/<feature>/PLAN.md` yet.
- Repository: `main`, private GitHub repo `hachi921082-source/Parent_Mail`.
- Uncommitted at the time of writing: `docs/spec.md`, `docs/detailed-spec.md`, `docs/QA.md`, `docs/HANDOFF.md` (last pushed commit: detailed-spec first version, `bf238d2`).
- Agent set: lite (`designer-lite`, `developer-lite`, `reviewer-lite`); do not mix with the full set (see `CLAUDE.md`).

## 2. Documents and where decisions live

- `docs/spec.md` — confirmed spec (8 sections, index at top). Self-contained summary of decisions; read first.
- `docs/detailed-spec.md` — implementation-ready detail in 19 sections; source for PLAN.md files. Section 19 lists 48 assumptions (A1-A48).
- `docs/QA.md` — all questions Q1-Q43 resolved; no open items; approved defaults D1-D21.
- `docs/DECISIONS.md` — rationale only (top-bar layout, app password, no encryption, text default).
- `docs/images/layout-sketch.png` — main-window sketch (top bar, narrow list, wide preview).

## 3. Awaiting user confirmation

- A1-A48 in detailed-spec section 19 are assumptions added by designer-lite; the user can veto any.
- A37: "return to default at app exit" for the preview zoom was interpreted as "zoom is not persisted; every launch starts at 100%". Not explicitly confirmed.
- "Unread shown in bold" and the read/unread display were bundled into the text of an answer choice (Q8/Q9) and recorded as adopted; not separately confirmed.
- A41 (WebView2 ZoomFactor formula) and A42 (attachment size threshold computation) are to be settled in PLAN.md.

## 4. Known risks accepted by the user

- Q21: Send is immediate, with no confirmation and no undo; wrong recipients cannot be caught (empty-subject warning only).
- Q28/Q35: address book, block list and filter rules are per account (not shared).
- No app-level encryption, no BitLocker requirement; local mail DB is readable by anyone with access to the Windows account/disk.
- Closing the window quits the app; fetching and Outbox retry run only while the app is open (mitigated by the quit warning with unsent mail).

## 5. Recommended next steps

1. Review and commit the uncommitted docs (one file per commit, role in the message body, Japanese), then push.
2. Have `developer-lite` read `docs/spec.md` (index) and `docs/detailed-spec.md`, propose a feature split and dependency order, and create `docs/plans/<feature>/PLAN.md` per feature plus `docs/plans/README.md` (shared index, project layout, shared models, tier table). Suggested split by detailed-spec sections: accounts/auth, Gmail mapping + sync + SQLite cache, Outbox/send/retry, mail display (text/HTML/zoom), compose/reply/forward, folder menus, address book, block list + filter rules, search, safety warnings, settings, startup/shutdown.
3. Settle open implementation choices in PLAN.md: A29 (shortener list, look-alike domain detection), A41, A42, A9 (settings DB vs cache DB), UIDVALIDITY handling.
4. Implement in feature branches per `~/.claude/shared/dev-workflow.md` section 7; confirm with the user before merge/push/branch delete.

## 6. Working notes

- Out-of-scope ideas recommended but not adopted (only the preview zoom was adopted): scam-phrase detection, "help" status button, automatic backup of local settings, read-aloud, settings export/import, error log.
- External review (Gemini) only sees what it is given and repeated already-decided points three times; give it `spec.md` plus `detailed-spec.md` and `QA.md` together, or skip further rounds.
- Language policy: docs in English; code comments in Japanese; commit bodies in Japanese; final chat reports in Japanese. Build artifacts need a date-time in the file name.
- Paths and file names with Japanese characters must work everywhere (attachments, save dialogs, cache paths).
