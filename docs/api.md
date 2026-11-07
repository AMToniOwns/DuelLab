# API reference

Base path `/api`. JSON uses camelCase; enums are strings. The machine-readable OpenAPI document is
served at `/openapi/v1.json`. There is no authentication (see
[ADR 0007](adr/0007-single-image-deployment-without-accounts.md)).

## Cards

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/cards?search=&kind=&page=1&pageSize=24` | Search by name (case-insensitive; exact and prefix matches first). `kind`: `Monster`, `Spell`, `Trap`. `pageSize` 1–100. |
| GET | `/cards/{id}` | One card. |
| GET | `/cards/{id}/image?size=small\|full` | The card's artwork, served from DuelLab's own copy (downloaded once). 404 if unavailable. |
| GET | `/cards/status` | `{ cardCount, lastSyncedAt, provider }` |
| POST | `/cards/sync` | Re-import from the provider. At most once an hour (`cards.sync_too_soon`). |

## Decks and versions

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/decks` | All decks with version summaries, most recently updated first. |
| POST | `/decks` | `{ name }` → 201 with the deck and its empty first version. |
| GET | `/decks/{id}` | One deck. |
| PUT | `/decks/{id}` | Rename: `{ name }`. |
| DELETE | `/decks/{id}` | Delete with all versions and runs → 204. |
| POST | `/decks/{id}/versions` | `{ versionName, sourceVersionId? }`: empty version, or a copy of `sourceVersionId`. |
| GET | `/versions/{id}` | Cards (with card details and roles) and validation. |
| PUT | `/versions/{id}` | `{ versionName, notes }` |
| DELETE | `/versions/{id}` | Delete (a deck keeps at least one: `deck.last_version`). |
| PUT | `/versions/{id}/cards` | Replace contents: `{ cards: [{ cardId, zone, quantity }] }` |
| PUT | `/versions/{id}/roles` | Replace roles: `{ cards: [{ cardId, roles: ["Starter", "Custom:Fusion line"] }] }` |

Saving accepts an incomplete deck (for example 25 Main Deck cards) but rejects impossible data:
quantity below 1, the same card twice in a zone, more than 3 copies across zones, a zone over its
maximum, unknown cards or roles. The response's `validation` lists what blocks simulation
(`Error`) and what is only advice (`Warning`, such as the banlist snapshot).

Role keys: `Starter`, `Extender`, `Searcher`, `HandTrap`, `BoardBreaker`, `Brick`, `Custom:<name>`.

## Simulations

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/versions/{id}/simulate` | Run and store a simulation (body below). |
| GET | `/versions/{id}/simulations?take=20` | The version's run history. |
| POST | `/versions/compare` | `{ baseVersionId, candidateVersionId, configuration }`: runs both with the same seed and returns both runs, per-scenario changes (percentage points, candidate − base) and card and role changes. |
| GET | `/simulations?take=10` | Recent runs across all decks. |
| GET | `/simulations/{id}` | A stored run, with `versionChangedSinceRun`. |
| POST | `/simulations/{id}/reproduce` | Re-run from the stored configuration and snapshot; `{ identical, engineVersionMatches, ... }`. |
| GET | `/simulations/default-scenarios` | The scenarios used when a request names none. |

Simulation request (every field is optional):

```json
{
  "sampleCount": 10000,
  "openingHandSize": 5,
  "seed": 12345,
  "sampleHandCount": 20,
  "scenarios": [
    {
      "name": "Starter + extender",
      "requireDistinctCards": true,
      "requirements": [
        { "role": "Starter", "minimumCount": 1 },
        { "role": "Extender", "minimumCount": 1 }
      ]
    },
    { "name": "No bricks", "requirements": [{ "role": "Brick", "minimumCount": 0, "maximumCount": 0 }] }
  ]
}
```

Limits: `sampleCount` 1–100,000; `openingHandSize` 1–20 (and at most the deck size); `seed`
0–9,007,199,254,740,991 (2⁵³ − 1, so it survives JavaScript unchanged; omitted → the server picks
and records one); up to 20 scenarios with up to 10 requirements each. The deck must pass
validation.

Each result scenario has `successes`, `samples`, `estimate`, `confidenceInterval` (95% Wilson),
`exact` (null only if the exact calculation would be too large) and a plain-language `description`.
The result also has `cardFrequencies` (exact and simulated chance of opening at least one copy) and
`sampleHands` (card ids in draw order, plus which scenarios each hand met).

## Errors

Every error is an RFC 9457 problem document with an extra `code`:

```json
{ "status": 400, "title": "Invalid request", "detail": "Card 12 has 4 copies. ...", "code": "deck.copy_limit" }
```

| Status | Codes (examples) |
| --- | --- |
| 400 | `deck.name_required`, `deck.copy_limit`, `deck.zone_too_large`, `deck.duplicate_entry`, `deck.unknown_card`, `deck.last_version`, `role.unknown`, `role.card_not_in_version`, `simulation.invalid_deck`, `simulation.sample_count`, `simulation.hand_size`, `simulation.seed`, `simulation.invalid`, `compare.same_version`, `cards.page_size`, `cards.sync_too_soon` |
| 404 | `not_found` (including unknown `/api` routes) |
| 503 | `external_unavailable` (the card provider could not be reached) |
| 500 | `internal_error` (details are logged, not returned) |

`GET /health` returns 200 when the database is reachable, 503 otherwise.
