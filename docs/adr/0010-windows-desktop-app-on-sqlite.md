# ADR 0010 – Windows desktop app on SQLite

- **Status:** Accepted (complements ADR 0007)
- **Date:** 2026-11-07

## Context

Similar calculators already exist online for free, and an ad-supported public site would need
accounts, privacy compliance and permission to use Konami's card content commercially. The owner
chose to share DuelLab with friends as a free Windows program instead: no hosting costs, no
accounts, everyone's decks on their own PC.

## Decision

1. **Same app, desktop mode.** The ASP.NET Core app and the Vue UI are shipped unchanged. Desktop
   mode turns on when `appsettings.Desktop.json` is next to the program (only the Windows package has
   it), so Docker and server setups are unaffected.
2. **SQLite for the desktop.** The database provider is configurable (`Database:Provider` =
   `PostgreSQL` or `Sqlite`). PostgreSQL-only mappings (identity column, `bigint[]` with a GIN index,
   `jsonb`, `ILIKE`) are applied only on PostgreSQL. SQLite uses JSON for the alternate-passcode list,
   `LIKE` for search, and stores `DateTimeOffset` as sortable 64-bit values (SQLite cannot compare
   them natively). SQLite has its own migrations (`SqliteDuelLabDbContext`), and CI checks both
   providers for missing migrations and runs the full API suite on both.
3. **Desktop behaviour.** Data in `%LOCALAPPDATA%\DuelLab`; listens on `127.0.0.1:5180` only (falls
   back to the next free port); opens the browser when ready; a second launch opens the running
   copy and exits; environment variables and command-line options still override the defaults.
4. **Packaging.** A self-contained, compressed single-file `DuelLab.exe` (about 54 MB; zip about
   47 MB) plus `wwwroot` and a README. CI builds it on Windows, starts it, imports a `.ydk`, checks an
   exact probability, checks the second-launch behaviour, uploads the zip, and attaches it to a
   GitHub Release for `v*` tags.

## Consequences

- Friends need nothing installed, but Windows SmartScreen warns about the unsigned program (a code
  signing certificate would remove that, at a yearly cost).
- Updates are manual (replace the folder; data is kept). There is no cross-device sync.
- Two databases to support. The shared test suite and per-provider migration checks keep them
  equivalent; provider-specific code is limited to the model mapping and the search query.
