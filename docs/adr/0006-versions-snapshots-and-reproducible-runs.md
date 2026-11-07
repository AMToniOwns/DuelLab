# ADR 0006 – Versions, snapshots and reproducible runs

- **Status:** Accepted
- **Date:** 2026-11-07

## Context

"Deck experiments must not destroy previous versions" and "later edits cannot change the meaning of
historical results" (spec §1, §5.6). Users also build decks gradually.

## Decision

- **Versions are editable.** Experiments happen by duplicating a version (`sourceVersionId`), which
  copies cards and roles and records `ParentVersionId`. The source is never modified.
- **Runs store a snapshot**, not just a reference: the normalised configuration (with the seed that
  was used) and the Main Deck's cards, quantities and roles, plus the engine version and a SHA-256 of
  the snapshot. A run's meaning never depends on the current state of the version.
- `versionChangedSinceRun` compares the snapshot hash with the version's current contents, so the UI
  can say "this run describes the deck as it was".
- **Reproduce** re-runs from the stored record and compares every count, probability and sample hand
  exactly. Descriptive text is excluded, so rewording never invalidates old runs.
- **Saving partial decks is allowed**: structurally impossible data is rejected, while size minimums
  and Extra Deck placement are validation errors that block simulation.
- Deleting a deck or version deletes its runs (cascade). Deleting a parent version leaves its copies
  (`SET NULL`).

## Consequences

- Runs are self-contained and slightly larger (a few KB each).
- Immutable versions (copy-on-write) were considered and rejected: they would complicate editing
  for no benefit, since snapshots already protect history.
