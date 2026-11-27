# Silo

A cross-platform .NET 11 desktop UI framework with its own renderer and CSS-like styling defined in JSON. "Silo" is a placeholder name.

## Agent skills

### Issue tracker

Issues and specs live in GitHub Issues on Nacorpio/Silo, managed with the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default vocabulary: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.

## Reviewing a new control

After a new control is implemented, add `src/Silo.Studio/Reviews/<Control>.review.jsonc` (questions about the control, see `ReviewDocument`) and open the review window with `silo-studio --review <Control> --dark`. A window that is already open for the same control is closed first. Submit writes `reviews/review-<Control>-<timestamp>.json`; read it back and act on it.
The answers file has a `verdict`: `keep` (improve it from the answers) or `redo` (the user wants the whole iteration thrown away; do not patch it, redo it from the design using the comment as the reason).
The window is a wizard: one question per step, and a question's `variant`/`state` switches the sample when its step comes up, so tie every question to the sample it is about. Later rounds show what the user said last time (from the earlier files in the same `--out` folder). Submit also writes `review-<Control>.submitted` (the path of the answers file) and closes the window, so the next build is not blocked.
