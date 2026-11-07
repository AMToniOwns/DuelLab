# DuelLab

A personal Yu-Gi-Oh! TCG deck builder and **opening-hand consistency analyser**. Build a deck, label
your starters, extenders and bricks, simulate thousands of opening hands, see the **exact**
probabilities next to the simulated ones, and compare two versions of a deck before you take it
to locals.

![CI](https://github.com/AMToniOwns/DuelLab/actions/workflows/ci.yml/badge.svg)

## What it does

- **Card database**: the full TCG card list (about 14,000 cards) imported from
  [YGOPRODeck](https://ygoprodeck.com/api-guide/) and searched locally. Card art is downloaded once
  and served by DuelLab, as the provider requires.
- **Deck builder**: Main (40–60), Extra (0–15) and Side (0–15) Deck; the 3-copy rule across all
  zones; Extra Deck placement checks.
- **Forbidden & Limited list**: TCG Advanced decks are checked against the TCG list (over-limit
  decks cannot be simulated, and the editor will not add a copy past the limit); Casual decks skip
  it. DuelLab keeps a dated history of the list from each import, and a deck can follow the current
  list or be pinned to an earlier one.
- **Versions**: duplicate a version to experiment; the original never changes. Unsaved edits
  survive navigation and page reloads.
- **Roles**: label cards as Starter, Extender, Searcher, Hand trap, Board breaker, Brick or any custom
  role. A card can have several.
- **Combo lines**: save the pieces each line needs ("Ash *or* the card that searches it" + "any
  extender") and get the exact chance of opening each line, and any of them.
- **Simulator**: seeded, reproducible Monte Carlo draws (100 to 100,000 hands) for any set of
  scenarios, with AND, OR, specific cards or roles, and maximums ("(starter AND extender, from
  different cards) OR 2+ starters", "no bricks", ...), 95% confidence intervals, **exact
  probabilities**, per-card opening odds and sample hands.
- **History and reproduction**: every run stores its configuration and a snapshot of the deck, can
  be re-run and verified to give identical results, and warns if the deck changed since.
- **Comparison**: runs two versions with the same settings and seed, and shows the change in
  percentage points for each scenario, plus which cards and roles changed.

DuelLab measures how often a hand contains the cards *you* labelled. It does not decide whether a
hand can complete a combo. See [statistical assumptions and limitations](docs/statistics.md).

## Run it with Docker

```bash
docker compose up --build
```

Then open http://localhost:8080. On first start the app imports the card list (one request to
YGOPRODeck, a few seconds). Data lives in Docker volumes (`db-data`, `app-data`).

DuelLab has **no user accounts**. Compose binds it to `127.0.0.1` only; put it behind an
authenticating reverse proxy before exposing it to a network. See [deployment](docs/deployment.md).

## Develop locally

Prerequisites: .NET 10 SDK, Node.js 22.18+, PostgreSQL 16+.

1. Create the database user (once):

   ```bash
   sudo -u postgres psql -c "CREATE ROLE duellab LOGIN PASSWORD 'duellab' CREATEDB;" -c "CREATE DATABASE duellab OWNER duellab;"
   ```

2. Start the API on http://localhost:5080. It applies migrations and imports cards on first start.

   ```bash
   dotnet run --project src/DuelLab.Api --launch-profile http
   ```

3. Start the web app on http://localhost:5173. It proxies `/api` to the API.

   ```bash
   cd frontend/duellab-web && npm ci && npm run dev
   ```

To work offline, use the bundled card fixture instead of YGOPRODeck: set
`CardCatalog__Provider=File` and `CardCatalog__FilePath=tests/fixtures/ygoprodeck-cards.json`.

## Tests

| Suite | Command | Needs |
| --- | --- | --- |
| Domain, simulator, API integration (xUnit) | `dotnet test` | PostgreSQL (`DUELLAB_TEST_POSTGRES` to override) |
| Frontend unit and component (Vitest) | `cd frontend/duellab-web && npx vitest run` | – |
| End-to-end (Playwright, Chromium) | `cd frontend/duellab-web && npx playwright test` | .NET, PostgreSQL |

The API tests create a throwaway database per test class and never touch the network. CI
(`.github/workflows/ci.yml`) runs all three suites, checks for missing migrations, and builds,
smoke-tests and publishes the Docker image to GHCR from `main`.

## Using DuelLab

1. **My decks → Create deck.** You land in the editor for the deck's first version. Decks start in
   TCG Advanced; change the format on the deck page (Casual, or a pinned list).
2. **Add cards** from the search panel. Extra Deck monsters go to the Extra Deck automatically.
   Use "→ Side" to move a copy to the Side Deck.
3. **Assign roles** with "+ Roles" on each card. Custom roles are allowed. The panel on the right
   counts copies per role in the Main Deck.
4. **Add combo lines** (optional) under the deck: one piece per card you need, and for each piece
   every card that can stand in for it, such as the card itself or its searchers.
5. **Save.** The deck check lists anything that blocks simulation (for example, fewer than 40 cards).
6. **Simulate.** Pick the number of hands, the hand size (5 going first, 6 to include the first
   draw going second) and optionally a seed. Edit or add scenarios ("+ And…", "+ Or…", and the
   role/card picker on each requirement), keep "Also measure combo lines" on, then run.
7. **Read the results.** Bars show the simulated share; the black tick is the exact probability; the
   whisker is the 95% interval. The table has every number. "Reproduce" re-runs the saved
   configuration and confirms the results are identical.
8. **Experiment.** On the deck page, "Duplicate" a version, change ratios, save, and use
   "Compare latest two" (or **Compare**) to see the change in percentage points.

## Documentation

- [Architecture](docs/architecture.md): projects, boundaries and request flow
- [API reference](docs/api.md): endpoints, payloads and error codes (OpenAPI at `/openapi/v1.json`)
- [Statistics](docs/statistics.md): what is measured, exact vs simulated, assumptions and limitations
- [Deployment](docs/deployment.md): Docker, configuration, backups
- Architecture Decision Records:
  - [0001 – Modular monolith on .NET 10 LTS](docs/adr/0001-modular-monolith-on-dotnet-10.md)
  - [0002 – Hand sampling algorithm and source of randomness](docs/adr/0002-hand-sampling-algorithm-and-randomness.md)
  - [0003 – Own seeded random generator](docs/adr/0003-own-seeded-random-generator.md)
  - [0004 – Scenario semantics and exact probabilities](docs/adr/0004-scenario-semantics-and-exact-probabilities.md)
  - [0005 – Card data: bulk import from YGOPRODeck](docs/adr/0005-card-data-bulk-import.md)
  - [0006 – Versions, snapshots and reproducible runs](docs/adr/0006-versions-snapshots-and-reproducible-runs.md)
  - [0007 – Single-image deployment without accounts](docs/adr/0007-single-image-deployment-without-accounts.md)
  - [0008 – OR conditions, card targets and combo lines](docs/adr/0008-or-conditions-card-targets-and-combo-lines.md)
  - [0009 – Forbidden & Limited enforcement and history](docs/adr/0009-banlist-enforcement-and-history.md)

## Credits

Card data and images: [YGOPRODeck](https://ygoprodeck.com/). Yu-Gi-Oh! is a trademark of Konami.
DuelLab is an unofficial fan tool.
