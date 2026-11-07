# ADR 0001 – Modular monolith on .NET 10 LTS

- **Status:** Accepted
- **Date:** 2026-11-07

## Context

DuelLab is a personal project built part-time. Statistical correctness and maintainability matter
more than scalability. The specification recommends a modular monolith (ASP.NET Core, EF Core,
PostgreSQL, Vue 3), with the version check for .NET LTS left until setup.

## Decision

- Build a **modular monolith**: separate projects for Domain, Application, Infrastructure, Api and
  Simulator in one solution and one deployable unit.
- Target **.NET 10**, the current LTS release (released November 2025, supported until November
  2028). `global.json` pins the SDK to the 10.0 line with `rollForward: latestFeature`.
- Build **`DuelLab.Simulator` first** and keep it free of UI, database and provider dependencies.
- Add each project when its milestone starts, rather than creating empty projects in advance.
- Make warnings errors and enable the `latest-recommended` analyzers from the start, so code quality
  is enforced while the codebase is still small.

## Consequences

- One build, one deployment, simple local development. No distributed-systems overhead.
- Boundaries between projects are kept by project references, not by network calls. They need
  discipline in code review: Simulator must never reference Infrastructure, for example.
- If a part ever needs to scale on its own (for example, very large simulation batches), the
  Simulator library can be moved out without rewriting it.
