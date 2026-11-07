import { expect, test } from '@playwright/test'

/** Combo lines (deck editor) and OR / card-specific scenario requirements (simulator). */

const CARDS = [
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
  'Swords of Revealing Light',
]

test('define a combo line and OR scenarios, then check exact odds', async ({ page, request }) => {
  await expect
    .poll(async () => (await (await request.get('/api/cards/status')).json()).cardCount, {
      timeout: 30_000,
    })
    .toBeGreaterThan(0)

  // Build a legal 42-card deck through the API (the UI path is covered by the journey test).
  const deck = await (await request.post('/api/decks', { data: { name: 'Combo deck' } })).json()
  const versionId: string = deck.versions[0].id
  const ids: Record<string, number> = {}
  for (const name of CARDS) {
    const page1 = await (await request.get(`/api/cards?search=${encodeURIComponent(name)}`)).json()
    ids[name] = page1.items.find((c: { name: string }) => c.name === name).id
  }
  await request.put(`/api/versions/${versionId}/cards`, {
    data: { cards: CARDS.map((n) => ({ cardId: ids[n], zone: 'Main', quantity: 3 })) },
  })
  await request.put(`/api/versions/${versionId}/roles`, {
    data: {
      cards: [
        { cardId: ids['Ash Blossom & Joyous Spring'], roles: ['Starter'] },
        { cardId: ids['Effect Veiler'], roles: ['Starter'] },
      ],
    },
  })

  // Define the combo line in the editor.
  await page.goto(`/versions/${versionId}/edit`)
  await page.getByTestId('add-combo-line').click()
  const line = page.getByTestId('combo-line-0')
  await line.getByLabel('Combo line name').fill('Ash + Veiler')
  await line
    .getByTestId('add-target-piece-0-0')
    .selectOption({ label: 'Ash Blossom & Joyous Spring' })
  await line.getByRole('button', { name: '+ Piece' }).click()
  await line.getByTestId('add-target-piece-0-1').selectOption({ label: 'Effect Veiler' })
  await expect(page.getByText('● Unsaved changes')).toBeVisible()
  await page.getByTestId('save-deck').click()
  await expect(page.getByText('All changes saved')).toBeVisible()

  // The line survives a reload (it was saved, not just kept in the draft).
  await page.reload()
  await expect(page.getByTestId('combo-line-0').getByLabel('Combo line name')).toHaveValue(
    'Ash + Veiler',
  )

  // Simulator: widen "Starter" to "starter or Raigeki", and give "2+ starters" an OR alternative.
  await page.getByTestId('simulate-deck').click()
  await page.getByLabel('Seed').fill('7')
  await page.getByRole('button', { name: 'Reset to defaults' }).click()
  await page.getByTestId('add-target-target-0-0-0').selectOption({ label: 'Raigeki' })
  await page.getByTestId('scenario-1').getByRole('button', { name: '+ Or…' }).click()
  await expect(page.getByTestId('scenario-1')).toContainText('Either')
  await expect(page.getByTestId('include-combos')).toBeChecked()
  await page.getByTestId('run-simulation').click()

  const results = page.getByRole('table', { name: 'Scenario results' })
  // Ash and Veiler, 3 copies each in 42 cards: 1 − 2·C(39,5)/C(42,5) + C(36,5)/C(42,5) = 8.95%.
  await expect(results.getByRole('row', { name: /^Combo: Ash \+ Veiler/ })).toContainText('8.95%')
  // 6 starters + 3 Raigeki = 9 copies: 1 − C(33,5)/C(42,5) = 72.10%.
  await expect(
    results.getByRole('row', {
      name: /^Starter Hand contains at least 1 of Raigeki or any starter/,
    }),
  ).toContainText('72.10%')
  // "2+ starters" OR "1+ starter" is just "1+ starter": 1 − C(36,5)/C(42,5) = 55.68%.
  await expect(
    results.getByRole('row', { name: /^2\+ starters Hand contains either/ }),
  ).toContainText('55.68%')

  await page.getByTestId('reproduce').click()
  await expect(page.getByTestId('reproduction-result')).toContainText('identical results')
})
