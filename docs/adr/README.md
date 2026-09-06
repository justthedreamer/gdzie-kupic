# Architecture Decision Records

Formal, committed records of significant architecture decisions for the Gdzie Kupic platform.

Each ADR is numbered sequentially and immutable once accepted — a later decision that changes course adds a **new** ADR that supersedes the old one (mark the old one's status as `Superseded by ADR-NNNN`), rather than editing history.

For the high-level, always-current summary of *what* was decided (without the full reasoning trail), see [design-decisions.md](../design-decisions.md). ADRs here are the detailed *why*, kept for context that would otherwise be lost.

## Index

| ADR | Title | Status |
|---|---|---|
| [0001](./0001-backend-authentication-oauth.md) | Backend Authentication Approach (Password + Google OAuth) | Accepted |

## Format

Each ADR follows this structure:

- **Status** — `Proposed`, `Accepted`, or `Superseded by ADR-NNNN`
- **Context** — the problem and constraints that led to needing a decision
- **Decision Drivers** — what mattered when weighing options
- **Considered Options** — alternatives evaluated, with pros/cons
- **Decision** — the option chosen and why
- **Consequences** — what this enables, what it costs, follow-up work it creates
