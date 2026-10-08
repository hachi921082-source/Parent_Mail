@~/.claude/shared/dev-workflow.md

## About This App

Parent_Mail: Windows desktop Gmail client for the user's elderly parents (poor eyesight, unfamiliar with PCs/smartphones). Simple UI, large text, minimal operations.

- Stack: C# + WPF, MailKit (IMAP/SMTP), WebView2 (HTML display), SQLite (local mail cache), Windows Credential Manager (app password).
- Gmail only, app-password auth, max 2 renamable accounts, switched by a top-bar button.
- Layout: top bar (account switch + folders) / narrow resizable mail list / very wide preview. Compose, reply, forward open in a separate window.
- Plain-text display by default; top-bar checkbox switches to HTML. Remote images blocked by default.
- Local cache mirrors Gmail state; delete = move to Gmail Trash only; works offline with cached mail.
- No LLM features, no app-level encryption, no third-party transmission of mail content.

Authoritative details: `docs/spec.md` (read its index first, then only the needed section). Rationale: `docs/DECISIONS.md`. Doc structure: `docs/README.md`.

## Agent Set

Lite set: `designer-lite`, `developer-lite`, `reviewer-lite` (spec.md / detailed-spec.md / `docs/plans/<feature>/PLAN.md` / QA.md, one-way escalation, no persistent log).

- Reason: small self-contained personal app; no `AGENT_LOG.md`, no separate `reviewer.md` + `tester.md`.
- Do not mix with the full set (`designer`, `developer`, `reviewer`, `tester`) in this project.
