# gdzie-kupic-location-service

Stateless geocoding proxy for the **Gdzie Kupic** reverse marketplace platform.

Accepts an address string and returns coordinates (latitude, longitude, display name) by proxying requests to the Google Maps Geocoding API. It has no database and no persistent state.

Called only by `gdzie-kupic-service` when a user saves a location (account setup, adding or editing a saved address). Never called during post creation or merchant matching, and never called directly by the frontend.

## Tech Stack

- **Runtime**: .NET 10 / ASP.NET Core

## Documentation

See the main [`docs/`](../docs/index.md) for the full documentation index, in particular:

- [Architecture](../docs/architecture.md) — service boundaries and communication patterns
- [Local Dev](../docs/local-dev.md) — local development setup
