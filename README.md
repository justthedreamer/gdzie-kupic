# Gdzie Kupic

**Gdzie Kupic** is a reverse marketplace for local shopping. Instead of searching for products, buyers publish a purchase request and nearby merchants respond with offers in real time.

A buyer describes what they need — product, location, radius, budget — and the platform notifies matching merchants in the area. Merchants reply directly; the buyer sees a live status panel showing exactly how many merchants were notified, are checking availability, or have confirmed stock.

## Monorepo structure

| Directory | Description |
|---|---|
| [`gdzie-kupic-service/`](gdzie-kupic-service/) | Core backend — modular monolith (C# / ASP.NET Core) |
| [`gdzie-kupic-ui/`](gdzie-kupic-ui/) | PWA frontend (Vue.js + Quasar) |
| [`gdzie-kupic-location-service/`](gdzie-kupic-location-service/) | Geocoding service — stateless Google Maps proxy (C# / ASP.NET Core) |

## Documentation

Full design and planning docs live in [`docs/`](docs/index.md).

| Document | Description |
|---|---|
| [Specification](docs/specification.md) | Project vision, problem statement, solution overview |
| [Architecture](docs/architecture.md) | Service boundaries, modules, communication patterns, infrastructure |
| [Data Model](docs/data-model.md) | Database schema — tables, columns, constraints, indexes |
| [Requirements](docs/requirements.md) | Functional and non-functional requirements |
| [Design Decisions](docs/design-decisions.md) | Record of key architectural decisions |
| [Planning](docs/planning.md) | Implementation phases, epics, and tickets |
| [Local Dev](docs/local-dev.md) | Local development setup — Docker Compose, ports, env vars |
