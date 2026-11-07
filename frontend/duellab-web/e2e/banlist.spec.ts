import { expect, test } from '@playwright/test'

/** Forbidden & Limited enforcement for TCG Advanced decks, and the Casual escape hatch. */

const UNLIMITED = [
  'Ash Blossom & Joyous Spring',
  'Effect Veiler',
  'Infinite Impermanence',
  'Dark Magician',
  'Blue-Eyes White Dragon',
  'Mystical Space Typhoon',
  'Raigeki',
  'Polymerization',
  'Mirror Force',
  'Nibiru, the Primal Being',
  'Dark Magician Girl',
  'Elemental HERO Neos',
  'Black Luster Soldier - Envoy of the Beginning',
]

test('TCG Advanced enforces the list; Casual allows the same deck', async ({ page, request }) => {
  await expect
    .poll(async () => (await (await request.get('/api/cards/status')).json()).cardCount, {
      timeout: 30_000,
    })
    .toBeGreaterThan(0)

  // 39 unlimited copies + 1 Pot of Prosperity (Limited) = a legal 40-card deck.
  const ids: Record<string, number> = {}
  for (const name of [...UNLIMITED, 'Pot of Prosperity']) {
    const found = await (await request.get(`/api/cards?search=${encodeURIComponent(name)}`)).json()
    ids[name] = found.items.find((c: { name: string }) => c.name === name).id
  }
  const deck = await (await request.post('/api/decks', { data: { name: 'Banlist deck' } })).json()
  const versionId: string = deck.versions[0].id
  await request.put(`/api/versions/${versionId}/cards`, {
    data: {
      cards: [
        ...UNLIMITED.map((n) => ({ cardId: ids[n], zone: 'Main', quantity: 3 })),
        { cardId: ids['Pot of Prosperity'], zone: 'Main', quantity: 1 },
      ],
    },
  })

  // The editor refuses a second copy of a Limited card.
  await page.goto(`/versions/${versionId}/edit`)
  await expect(page.getByTestId('deck-format')).toContainText('TCG Advanced')
  const row = page.getByTestId('entry-Main-Pot of Prosperity')
  await expect(row.getByText('Limited')).toBeVisible()
  await expect(row.getByRole('button', { name: 'Add one Pot of Prosperity' })).toBeDisabled()
  await page.getByLabel('Search cards').fill('Pot of Prosperity')
  await page.getByRole('button', { name: 'Add Pot of Prosperity', exact: true }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'is Limited' })).toBeVisible()

  // Over the limit through the API: the deck check shows an error and simulation is refused.
  await request.put(`/api/versions/${versionId}/cards`, {
    data: {
      cards: [
        ...UNLIMITED.map((n) => ({ cardId: ids[n], zone: 'Main', quantity: 3 })),
        { cardId: ids['Pot of Prosperity'], zone: 'Main', quantity: 2 },
      ],
    },
  })
  await page.goto(`/simulate?versionId=${versionId}`)
  await expect(page.getByText('This version cannot be simulated yet:')).toBeVisible()
  await expect(page.getByText(/Pot of Prosperity is Limited/)).toBeVisible()
  await expect(page.getByTestId('run-simulation')).toBeDisabled()

  // Switch to Casual on the deck page: now valid and simulatable.
  await page.goto(`/decks/${deck.id}`)
  await page.getByTestId('deck-format-select').selectOption('casual')
  await expect(page.getByText('Forbidden and Limited cards are allowed.')).toBeVisible()
  await page.goto(`/simulate?versionId=${versionId}`)
  await expect(page.getByTestId('run-simulation')).toBeEnabled()
  await page.getByTestId('run-simulation').click()
  await expect(page.getByRole('heading', { name: 'How often each scenario holds' })).toBeVisible()

  // The list page shows the current list.
  await page.getByRole('link', { name: 'Banlist', exact: true }).click()
  await expect(page.getByRole('heading', { name: /^Limited/ })).toBeVisible()
  await expect(page.getByText('Pot of Prosperity')).toBeVisible()
})
