# ADR 0009 – Forbidden & Limited enforcement and history

- **Status:** Accepted (supersedes the banlist part of ADR 0005)
- **Date:** 2026-11-07

## Context

Until now the TCG Forbidden & Limited status from YGOPRODeck was only a warning. The roadmap asks for
"validate deck legality against the applicable TCG banlist and effective date". YGOPRODeck's API
(checked 2026-11-07) reports each card's **current** status (`ban_tcg`) and a database version with a
last-update time (`checkDBVer.php`). It has no list history and no effective dates.

## Decision

1. **Formats.** A deck is either **TCG Advanced** (the list is enforced, the default and existing
   decks' format) or **Casual** (no list; the 3-copy rule and deck sizes still apply).
2. **Enforcement is an error.** In TCG Advanced, more copies than the status allows (0/1/2, across
   all zones) fails validation, so the deck cannot be simulated. The editor also refuses to add a
   copy past the limit. To analyse an illegal list anyway, the user switches the deck to Casual: an
   explicit choice, not a silent bypass.
3. **DuelLab keeps its own history.** Each import stores a snapshot (`banlists` and
   `banlist_entries`) when the list's content hash changed, with `firstSeenAt` and the provider's
   version and update time. Existing installs get a first snapshot at startup from their cards'
   statuses, without a provider call.
4. **Follow or pin.** A TCG Advanced deck follows the current (latest) snapshot by default, or is
   pinned to an earlier snapshot (`Deck.BanlistId`), for example to check a list from a past event.
5. **Honest dates.** Lists are labelled "first seen *date*", never "effective *date*". Messages name
   the list that was applied, and each simulation run records the format and list.
6. Opening an old run no longer requires its deck to be valid today, so a later list change never
   makes history unreadable.

## Consequences

- A list update can make a previously legal deck invalid. That is the point of enforcement, and the
  validation message says what changed and how to proceed.
- History only covers the time DuelLab has been running. Older lists, or a list with a future
  effective date, would need another source; the snapshot model could accept an imported list
  without code changes elsewhere.
- OCG and other formats are not supported; adding one means another format value and list source.
