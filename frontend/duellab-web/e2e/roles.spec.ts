import { expect, test } from '@playwright/test'

/** The role menu (one at a time) and role suggestions in the deck editor. */

const CARDS = [
  'Ash Blossom & Joyous Spring',
  'Effect Veiler',
  'Infinite Impermanence',
  'Nibiru, the Primal Being',
  'Mystical Space Typhoon',
  'Raigeki',
  'Dark Magician',
]

test('one role menu at a time, and suggested roles', async ({ page, request }) => {
  await expect
    .poll(async () => (await (await request.get('/api/cards/status')).json()).cardCount, {
      timeout: 30_000,
    })
    .toBeGreaterThan(0)
  const ids: number[] = []
  for (const name of CARDS) {
    const found = await (await request.get(`/api/cards?search=${encodeURIComponent(name)}`)).json()
    ids.push(found.items.find((c: { name: string }) => c.name === name).id)
  }
  const deck = await (await request.post('/api/decks', { data: { name: 'Roles deck' } })).json()
  const versionId: string = deck.versions[0].id
  await request.put(`/api/versions/${versionId}/cards`, {
    data: { cards: ids.map((cardId) => ({ cardId, zone: 'Main', quantity: 3 })) },
  })

  await page.goto(`/versions/${versionId}/edit`)
  const ash = page.getByTestId('entry-Main-Ash Blossom & Joyous Spring')
  // A card further down, whose button is not covered by the first card's open menu.
  const imperm = page.getByTestId('entry-Main-Infinite Impermanence')

  // Opening another card's menu closes the first; clicking elsewhere and Esc close it too.
  await ash.getByTestId('roles-toggle').click()
  await expect(page.getByTestId('roles-menu')).toHaveCount(1)
  await expect(ash.getByTestId('roles-menu')).toBeVisible()
  await imperm.getByTestId('roles-toggle').click()
  await expect(page.getByTestId('roles-menu')).toHaveCount(1)
  await expect(imperm.getByTestId('roles-menu')).toBeVisible()
  await page.getByRole('heading', { name: 'Deck check' }).click()
  await expect(page.getByTestId('roles-menu')).toHaveCount(0)
  await ash.getByTestId('roles-toggle').click()
  await page.keyboard.press('Escape')
  await expect(page.getByTestId('roles-menu')).toHaveCount(0)

  // Suggestions: well-known hand traps and board breakers, ticked by default.
  await page.getByTestId('suggest-roles').click()
  await expect(page.getByTestId('suggest-Ash Blossom & Joyous Spring-HandTrap')).toBeChecked()
  await expect(page.getByTestId('suggest-Raigeki-BoardBreaker')).toBeChecked()
  await expect(page.getByTestId('suggest-Dark Magician-HandTrap')).toHaveCount(0)
  await page.getByTestId('suggest-Raigeki-BoardBreaker').uncheck()
  await page.getByTestId('apply-suggestions').click()

  await expect(page.getByText('● Unsaved changes')).toBeVisible()
  await expect(ash.getByText('Hand trap', { exact: true })).toBeVisible()
  await expect(
    page.getByTestId('entry-Main-Nibiru, the Primal Being').getByText('Hand trap', { exact: true }),
  ).toBeVisible()
  await expect(
    page
      .getByTestId('entry-Main-Mystical Space Typhoon')
      .getByText('Board breaker', { exact: true }),
  ).toBeVisible()
  await expect(
    page.getByTestId('entry-Main-Raigeki').getByText('Board breaker', { exact: true }),
  ).toHaveCount(0)

  // Asking again only offers what is left.
  await page.getByTestId('suggest-roles').click()
  await expect(page.getByTestId('suggest-Raigeki-BoardBreaker')).toBeVisible()
  await expect(page.getByTestId('suggest-Ash Blossom & Joyous Spring-HandTrap')).toHaveCount(0)
})
