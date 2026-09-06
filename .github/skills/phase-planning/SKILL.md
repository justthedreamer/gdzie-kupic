---
name: phase-planning
description: 'Use when the architect/developer wants to plan or refine tasks for an implementation phase of the Gdzie Kupic project — reviewing docs and recent commits, discussing architecture/technology choices area by area, and creating or updating GitHub epic + sub-issues once decisions are approved. Triggers: "planowanie fazy", "zaplanujmy fazę", "plan phase P<n>", "epic", "ADR", "przygotuj ADR", "architecture decision", mid-phase ticket refinement.'
argument-hint: "Phase (e.g. 'P2') or focus area (e.g. 'user model ADR')"
---

# Phase Planning

Guides collaborative planning of an implementation phase with the architect, from research through GitHub issue creation.

## When to Use
- Starting to plan a whole phase from [docs/planning.md](../../../docs/planning.md), e.g. "zacznijmy planować fazę 2"
- Planning/refining a single area within an already-started phase, e.g. "zacznijmy od ADR na model użytkownika"
- Mid-phase: an epic and some sub-issues already exist and need new/changed/removed tasks

## Step 1 — Orient Yourself

Before discussing anything, ground yourself in the current real state:

1. Read [docs/planning.md](../../../docs/planning.md) to find the phase and its listed tasks.
2. Read the docs directly relevant to the area under discussion: [docs/requirements.md](../../../docs/requirements.md) (`FR-*`), [docs/architecture.md](../../../docs/architecture.md), [docs/data-model.md](../../../docs/data-model.md), [docs/design-decisions.md](../../../docs/design-decisions.md) (high-level, always-current decision summary), [docs/adr/](../../../docs/adr/README.md) (detailed, immutable Architecture Decision Records — check the index for one already covering this area), [docs/specification.md](../../../docs/specification.md).
3. Check recent commit history (`git log`) and the actual current code (search/read the relevant projects in `gdzie-kupic-service`/`gdzie-kupic-ui`) to see what's already implemented. Docs can lag behind code — verify, don't assume.
4. Check GitHub for an existing epic/sub-issues covering this phase (`github_repo`). If found, this is a refinement, not a fresh plan — go in with that context.

Do not skip straight to discussion or issue creation before this step.

## Step 2 — Architect-Led Discussion

- The architect drives the process and names the area to plan (e.g. "let's start with an ADR for the user model"). Follow their lead on ordering.
- For each area, one at a time:
  - Summarize what the docs/code currently say, and call out any gaps or contradictions.
  - Where a decision is genuinely open, **propose 2–3 concrete options with trade-offs** rather than asking an open-ended question first. Recommend one if you have a preference, but let the architect decide — they don't always know the right answer upfront, and that's expected.
  - Record agreed decisions as you go (session memory or a running scratch summary) instead of waiting until the end.
  - Only move to the next area once the architect confirms the current one is settled.
- Do not create or modify any GitHub issues during this discussion step.
- If a decision materially changes a doc (e.g. a new design decision), propose the doc edit and apply it once confirmed — keep [docs/design-decisions.md](../../../docs/design-decisions.md) as the source of truth for architecture rationale.
- If the architect wants a decision **formally committed** with its full reasoning trail (not just the one-line summary in `design-decisions.md`), write it as a proper ADR in [docs/adr/](../../../docs/adr/README.md): next sequential number, filename `NNNN-kebab-title.md`, sections Status/Context/Decision Drivers/Considered Options/Decision/Consequences (see existing ADRs for the exact shape). Add it to `docs/adr/README.md`'s index. A superseding decision later adds a **new** ADR rather than editing the old one.

## Step 3 — Draft the Plan

Once all areas in scope for this session are settled, produce a structured draft (no GitHub writes yet):

- **Full phase**: one epic + one sub-issue per task from `planning.md`, across all relevant repos/services.
- **Partial/mid-phase**: list of additions, edits, and removals against the existing epic/sub-issues.

For each issue, note: service (`Service` / `UI` / `Location Service`), task name, applicable `FR-*` codes, relevant doc sections, and the key decisions from Step 2 that shape it.

**Working file:** keep a per-phase planning file at `planning/phase-<n>-<slug>.md` (e.g. `planning/phase-2-authentication.md`). This file is a permanent, committed part of the repo — a historical record of how the phase was planned — not a temp/scratch file to delete afterward. As each ticket is drafted:

- Write the full drafted ticket body directly into the planning file (status table row + full "Drafted — ..." section), not just into the chat response.
- Present/confirm each draft through normal chat replies, one ticket at a time — do not use confirmation dialogs/popups for this; the architect reviews and replies in the conversation, and may edit the file content directly themselves.
- Once the architect approves a ticket, mark its status `Ready` in the table before moving to the next one.
- Only use a dialog/popup-style tool for a quick multiple-choice pick when there's no other reasonable way to gather the answer (e.g. picking between enumerated options) — never for presenting or confirming drafted ticket content itself.

After every ticket in scope is `Ready`, present the full consolidated draft one more time and **ask for explicit confirmation** before touching GitHub.

## Step 4 — Create/Update GitHub Issues

Only after the architect approves the draft. Follow the issue template and rules defined in [create-github-tickets.prompt.md](../../prompts/create-github-tickets.prompt.md) exactly (title format, body sections, Definition of Done style, "what NOT to do" rules).

- **New phase**: create the epic first, then each sub-issue, then update the epic body with a task list (`- [ ] #<issue-number> <title>`) linking all sub-issues.
- **Mid-phase refinement**:
  - Edit existing sub-issues in place (title/body) rather than closing and recreating them.
  - If the architect wants a task removed, close it with a brief comment explaining why — never close silently.
  - Add new sub-issues for newly agreed tasks and append them to the epic's task list.
  - Never close the epic, force-push, or merge anything without explicit confirmation.
- After every write, summarize exactly what changed (issue numbers + titles) so the architect can verify.

## Key Principles

- The architect leads; the model proposes analysis and options, it never forces a single "correct" answer onto an undecided question.
- Nothing is written to GitHub without explicit confirmation of the final result.
- Ground every task in the actual current code/doc state, not assumptions — verify before claiming something is missing, done, or already decided.
- Drafts live in the phase's `planning/phase-<n>-<slug>.md` file and are confirmed conversationally in chat, not via popups/dialogs — the planning file is the durable record, kept in the repo permanently.
