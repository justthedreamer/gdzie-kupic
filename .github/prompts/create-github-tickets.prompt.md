---
name: "Create GitHub Tickets"
description: "Use when you want to create GitHub issues for a planning phase/epic. Reads planning.md, deeply refines docs, then creates an epic issue and sub-issues following the project template."
argument-hint: "Phase number or name, e.g. 'P1' or 'Authentication'"
agent: "agent"
tools: ["github_repo", "read_file", "file_search"]
---

You are creating GitHub issues for the **Gdzie Kupic** monorepo.

## Step 1 — Read the planning document

Read [docs/planning.md](../../docs/planning.md) and identify the phase/epic the user specified: **$input**.

If no input was given, ask which phase or epic to target before continuing.

## Step 2 — Deeply read the relevant docs

For every task in the chosen phase, read all documentation sections that are directly relevant:

- [docs/requirements.md](../../docs/requirements.md) — functional requirements (`FR-*` codes)
- [docs/architecture.md](../../docs/architecture.md) — service/module boundaries and responsibilities
- [docs/data-model.md](../../docs/data-model.md) — schema details for any data-related task
- [docs/design-decisions.md](../../docs/design-decisions.md) — rationale behind key choices
- [docs/specification.md](../../docs/specification.md) — user-facing behaviour and platform strategy

Do **not** create any issues until you have read every relevant section.

## Step 3 — Plan all issues

Before creating anything, output a structured plan listing:

1. **Epic issue** — one issue that represents the whole phase
2. **Sub-issues** — one issue per task listed in that phase (across all services/repos)

For each issue, note:
- Which service it belongs to: `Service`, `UI`, or `Location Service`
- The task name
- Which `FR-*` requirements and doc sections apply

Ask for confirmation before proceeding to creation.

## Step 4 — Create the epic issue first

Create the epic GitHub issue using the template below, then create each sub-issue and **add their numbers as a task list** in the epic issue body.

---

## Issue Template

Use this template **exactly** for every issue created.

### Title format

```
[<label>]: <Task Name>
```

Where `<label>` is one of:
- `Service` — core backend (`gdzie-kupic-service`)
- `UI` — frontend (`gdzie-kupic-ui`)
- `Epic` — phase-level epic issue

### Body format

```markdown
## Brief Description
<One or two sentences describing what this issue delivers.>

---

## User Story
As a <role>, I want to <action> so that <benefit>.

---

## Description
<Detailed explanation of the task. Cover what needs to be built, how it fits into the system, and any important constraints or edge cases. Reference the relevant sections below.>

**Documentation:** [architecture.md](docs/architecture.md) · [requirements.md](docs/requirements.md) · <add other relevant doc links>
**Requirements:** <comma-separated list of FR-* codes that apply, e.g. FR-AUTH-1, FR-AUTH-2>

---

## Definition of Done
- [ ] <Acceptance criterion 1>
- [ ] <Acceptance criterion 2>
- [ ] <Add as many as needed — be specific and testable>
```

---

## Rules

- **Epic issues** must contain a task list (`- [ ] #<issue-number> <title>`) linking all sub-issues once they are created.
- Every sub-issue must link back to the epic in the description with: `Part of epic: #<epic-number>`.
- Use the size label from `planning.md` (S / M / L / XL / XXL) as a GitHub label if labels exist.
- If a task spans multiple services (e.g. a backend endpoint + UI view), create **one issue per service**.
- Do not invent requirements. Only reference `FR-*` codes that genuinely apply to the task.
- Keep User Stories simple: one sentence, role → action → benefit.
- Acceptance criteria must be concrete and testable, not vague ("works correctly" is not acceptable).

## What NOT to do

- **Do not prescribe implementation.** Tickets describe *what* needs to work, not *how* to build it. Do not specify class names, method signatures, patterns, algorithms, or code structure. The developer decides the implementation.
- **Do not over-describe.** If the documentation already covers a detail, link to it — do not copy it into the ticket.
- **Do not add tasks that are not in `planning.md`.** Stick strictly to what is planned; do not expand scope.
