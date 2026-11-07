# Deployment

DuelLab ships as **one Docker image** (API and web app) plus PostgreSQL. See
[ADR 0007](adr/0007-single-image-deployment-without-accounts.md).

## Docker Compose (single server)

```bash
POSTGRES_PASSWORD='choose-a-strong-password' docker compose up -d --build
```

- The app listens on `127.0.0.1:8080` (`DUELLAB_PORT` to change it). It is deliberately **not**
  exposed to the network: DuelLab has no login.
- Migrations run on start (`Database__MigrateOnStartup=true`).
- The card list is imported on first start; card images are cached in the `app-data` volume.

CI publishes `ghcr.io/amtoniowns/duellab:latest` (and a tag per commit) from `main`, so a server can
`docker compose pull` instead of building.

## Exposing it safely

Put a reverse proxy with authentication and TLS in front, for example Caddy with basic auth:

```caddyfile
duellab.example.com {
  basic_auth {
    you <bcrypt-hash from `caddy hash-password`>
  }
  reverse_proxy 127.0.0.1:8080
}
```

## Configuration

| Setting (environment variable) | Default | Meaning |
| --- | --- | --- |
| `ConnectionStrings__DuelLab` | local `duellab` database | PostgreSQL connection string |
| `Database__MigrateOnStartup` | `true` | Apply EF Core migrations at start |
| `CardCatalog__Provider` | `YgoProDeck` | `YgoProDeck` or `File` |
| `CardCatalog__FilePath` | – | For `File`: a JSON file in YGOPRODeck's format |
| `CardCatalog__ImportOnStartup` | `true` | Import cards when the catalogue is empty |
| `CardCatalog__ImageCachePath` | `data/card-images` (`/app/data/card-images` in Docker) | Downloaded artwork |
| `CardCatalog__MaxImageDownloadsPerSecond` | `10` | Stays well under YGOPRODeck's 20 requests/second limit |

## Backups

Everything you create lives in PostgreSQL:

```bash
docker compose exec db pg_dump -U duellab duellab > duellab-$(date +%F).sql
```

Card data and images can always be re-imported, so they do not need backing up.

## Updating card data

`POST /api/cards/sync` refreshes the catalogue (allowed once an hour). Cards are never deleted, so
existing decks keep working. If the TCG Forbidden & Limited list changed, the import stores a new
dated snapshot; decks that follow the current list are checked against it from then on, so a deck
that was legal can become invalid until it is fixed or switched to Casual (or pinned to the older
list).

## Performance

Measured on the development container (Release build, warm process): a 100,000-hand simulation of a
42-card deck with six scenarios takes about 85 ms end to end, including storing the run. The first
request after start is about 0.5 s because of just-in-time compilation. No optimisation was needed.
