# ADR 0007 – Single-image deployment without accounts

- **Status:** Accepted
- **Date:** 2026-11-07

## Context

The first release is a personal tool. The spec says not to add authentication, cloud infrastructure
or commercial features until they are needed (§13, rule 17), but asks for a working deployment.

## Decision

- **One container image**: the ASP.NET Core app serves the API and the built Vue app (`wwwroot`,
  SPA fallback, unknown `/api` routes → 404). One origin, so no CORS and nothing else to deploy.
- **Docker Compose** with PostgreSQL for a single server; CI builds, smoke-tests and publishes the
  image to GHCR from `main`.
- **No user accounts.** Compose binds the app to `127.0.0.1`. To reach it remotely, put an
  authenticating reverse proxy in front ([deployment guide](../deployment.md)).
- Migrations run at start (configurable), since a single instance owns the database.

## Consequences

- Anyone who can reach the app can edit and delete decks. This is acceptable only behind the proxy
  or on a private machine. Adding accounts later means an owner column on decks plus ASP.NET Core
  authentication; the services already receive everything through one composition root.
- Running more than one instance would need migrations to move out of startup.
