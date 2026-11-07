# ADR 0005 – Card data: bulk import from YGOPRODeck

- **Status:** Accepted; the banlist handling below is superseded by [ADR 0009](0009-banlist-enforcement-and-history.md)
- **Date:** 2026-11-07

## Context

The spec suggests YGOPRODeck and asks that its terms, rate limits and image rules be checked first.
The API guide (read 2026-11-07) says: store data locally and keep calls to a minimum; 20 requests
per second, and exceeding it blocks the IP for an hour; do not hotlink images, re-host them.

## Decision

- **Import the whole TCG card list in one request** (`cardinfo.php?format=tcg`, about 20 MB,
  14,083 entries) into PostgreSQL, on first start, in the background. Skill Cards and Tokens are
  skipped (13,949 cards remain).
- **Search locally** with case-insensitive `ILIKE` (wildcards escaped), exact and prefix matches
  first. No provider call per search.
- **Refresh on request** (`POST /api/cards/sync`), at most once an hour. Cards are upserted and
  never deleted, so decks keep their references.
- **Re-host images**: `/api/cards/{id}/image` downloads an image the first time it is needed (one
  download per file, rate-limited to 10 per second), stores it on disk and serves it from then on.
  The API never exposes provider image URLs to the browser.
- Keep the provider's response models inside Infrastructure; the mapper produces provider-neutral
  `CardData`. "?" ATK/DEF (reported as -1) becomes null. Fusion, Synchro, Xyz and Link frames are
  Extra Deck cards.
- Store the TCG Forbidden & Limited status from the same import, shown as a **warning**
  (dated snapshot), not a legality guarantee.
- A `File` provider reads the same format from disk, for offline development and tests.

## Consequences

- Search is fast and works when the provider is down. Only imports and first image views need the
  network.
- Data is as fresh as the last sync; the UI shows the sync date.
- Switching provider means writing one adapter that implements `ICardDataProvider`.
