# Documentation

Design and planning documentation for the **Gdzie Kupic** platform.

## Documents

| Document | Description |
|---|---|
| [specification.md](specification.md) | Project vision, problem statement, solution overview, roadmap |
| [design-decisions.md](design-decisions.md) | Record of all system design decisions made during the architecture phase |
| [architecture.md](architecture.md) | Authoritative architecture reference — service boundaries, modules, communication patterns, infrastructure |
| [data-model.md](data-model.md) | Database schema — all tables, columns, constraints, and indexes |
| [requirements.md](requirements.md) | Functional and non-functional requirements derived from design decisions |
| [planning.md](planning.md) | Implementation plan — phases, GitHub epics, and tickets with size estimates |
| [local-dev.md](local-dev.md) | Local development setup — Docker Compose, ports, env vars, common tasks |
| [observability.md](observability.md) | Logging architecture, Serilog setup, Seq, enrichment properties, verification guide |

## Repositories

| Repo | Role |
|---|---|
| [`gdzie-kupic-service`](https://github.com/justthedreamer/gdzie-kupic-service) | Core backend — modular monolith |
| [`gdzie-kupic-ui`](https://github.com/justthedreamer/gdzie-kupic-ui) | PWA frontend (Vue.js + Vite) |
