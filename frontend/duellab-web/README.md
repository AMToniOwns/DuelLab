# duellab-web

The DuelLab web app: Vue 3, TypeScript, Vite, Tailwind CSS 4, Pinia and Vue Router.
See the [project README](../../README.md) for how to run the whole application.

| Command | What it does |
| --- | --- |
| `npm ci` | Install dependencies |
| `npm run dev` | Dev server on :5173 (proxies `/api` to the API on :5080, or `DUELLAB_API_URL`) |
| `npm run build` | Type-check and build to `dist/` |
| `npx vitest run` | Unit and component tests |
| `npx playwright test` | End-to-end tests (starts the API and the app; needs .NET and PostgreSQL) |
| `npx oxlint . && npx eslint .` | Lint |

Layout: `src/api` (typed API client), `src/lib` (pure logic such as the deck draft), `src/stores`
(Pinia), `src/components`, `src/views`, `e2e` (Playwright).
