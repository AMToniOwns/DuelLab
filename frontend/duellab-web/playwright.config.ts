import process from 'node:process'
import { fileURLToPath } from 'node:url'
import { defineConfig, devices } from '@playwright/test'

/**
 * End-to-end tests run the real API (its own database, offline card fixture) and the Vite app, and
 * drive the full user journey in Chromium.
 *
 * Requirements: the .NET SDK and a PostgreSQL server. Override the connection with
 * DUELLAB_E2E_DB (default: local "duellab" user, database "duellab_e2e").
 */
const root = fileURLToPath(new URL('../..', import.meta.url))
const apiPort = 5081
const webPort = 5174
const connection =
  process.env.DUELLAB_E2E_DB ??
  'Host=localhost;Port=5432;Database=duellab_e2e;Username=duellab;Password=duellab'

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: `http://localhost:${webPort}`,
    trace: 'off',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1400, height: 900 } },
    },
  ],
  webServer: [
    {
      command: `dotnet run --project "${root}src/DuelLab.Api" --no-launch-profile`,
      url: `http://localhost:${apiPort}/health`,
      timeout: 180_000,
      reuseExistingServer: false,
      env: {
        ASPNETCORE_URLS: `http://localhost:${apiPort}`,
        ASPNETCORE_ENVIRONMENT: 'Testing',
        ConnectionStrings__DuelLab: connection,
        CardCatalog__Provider: 'File',
        CardCatalog__FilePath: `${root}tests/fixtures/ygoprodeck-cards.json`,
        CardCatalog__ImportOnStartup: 'true',
        CardCatalog__ImageCachePath: `${root}frontend/duellab-web/test-results/card-images`,
      },
    },
    {
      command: `npx vite --port ${webPort} --strictPort`,
      url: `http://localhost:${webPort}`,
      timeout: 60_000,
      reuseExistingServer: false,
      env: { DUELLAB_API_URL: `http://localhost:${apiPort}` },
    },
  ],
})
