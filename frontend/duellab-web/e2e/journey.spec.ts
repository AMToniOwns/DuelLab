import { expect, test, type Page } from '@playwright/test'

/**
 * The MVP's core journey (spec section 3): create a deck, build it from the catalogue, assign roles,
 * save, simulate with a seed, check exact values, reproduce, duplicate, change ratios, compare.
 */

const MAIN_CARDS = [
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

test.beforeAll(async ({ request }) => {
  // Wait for the fixture import, then start from an empty deck list.
  await expect
    .poll(async () => (await (await request.get('/api/cards/status')).json()).cardCount, {
      timeout: 30_000,
    })
    .toBeGreaterThan(0)
  for (const deck of await (await request.get('/api/decks')).json())
    await request.delete(`/api/decks/${deck.id}`)
})

async function addCopies(page: Page, name: string, copies: number): Promise<void> {
  const search = page.getByLabel('Search cards')
  await search.fill(name)
  const add = page.getByRole('button', { name: `Add ${name}`, exact: true })
  for (let i = 0; i < copies; i++) await add.click()
  await expect(page.getByTestId(`entry-Main-${name}`).getByLabel(`${copies} copies`)).toBeVisible()
}

async function setRole(page: Page, cardName: string, role: string): Promise<void> {
  const row = page.getByTestId(`entry-Main-${cardName}`)
  const toggle = row.getByTestId('roles-toggle')
  await toggle.click()
  await row.getByRole('checkbox', { name: role }).check()
  await toggle.click()
  await expect(row.getByText(role, { exact: true })).toBeVisible()
}

test('build, simulate, reproduce, duplicate and compare a deck', async ({ page }) => {
  // Create a deck: lands in the editor for its first version.
  await page.goto('/decks')
  await page.getByLabel('Create an empty deck').fill('E2E Test Deck')
  await page.getByRole('button', { name: 'Create deck' }).click()
  await expect(page.getByRole('heading', { name: /^v1/ })).toBeVisible()

  // Build a 42-card Main Deck from the catalogue.
  for (const name of MAIN_CARDS) await addCopies(page, name, 3)
  await expect(page.getByTestId('zone-Main')).toContainText('42 cards')

  // The copy limit is enforced in the UI.
  await page.getByLabel('Search cards').fill('Raigeki')
  await page.getByRole('button', { name: 'Add Raigeki', exact: true }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'already have 3 copies' })).toBeVisible()

  // Assign roles.
  await setRole(page, 'Ash Blossom & Joyous Spring', 'Starter')
  await setRole(page, 'Effect Veiler', 'Starter')
  await setRole(page, 'Infinite Impermanence', 'Extender')
  await setRole(page, 'Dark Magician', 'Brick')

  // Unsaved work survives navigation.
  await expect(page.getByText('● Unsaved changes')).toBeVisible()
  await page.getByRole('link', { name: 'Dashboard' }).click()
  await expect(page.getByText('You have unsaved deck changes.')).toBeVisible()
  await page.getByRole('link', { name: 'E2E Test Deck · v1' }).click()
  await expect(page.getByTestId('zone-Main')).toContainText('42 cards')

  await page.getByTestId('save-deck').click()
  await expect(page.getByText('All changes saved')).toBeVisible()
  await expect(page.getByText('Ready to simulate.')).toBeVisible()

  // Simulate with a fixed seed.
  await page.getByTestId('simulate-deck').click()
  await page.getByLabel('Simulated hands').selectOption('10000')
  await page.getByLabel('Seed').fill('12345')
  await page.getByRole('button', { name: 'Reset to defaults' }).click()
  await page.getByTestId('run-simulation').click()

  // Exact values: 6 starters in 42 cards → 1 − C(36,5)/C(42,5) = 55.68%; 3 bricks → 32.32%.
  await expect(page.getByRole('heading', { name: 'How often each scenario holds' })).toBeVisible()
  const results = page.getByRole('table', { name: 'Scenario results' })
  await expect(
    results.getByRole('row', { name: /^Starter Hand contains at least 1 starter/ }),
  ).toContainText('55.68%')
  await expect(results.getByRole('row', { name: /^Brick/ })).toContainText('32.32%')
  await expect(page.getByTestId('sample-hand-0')).toBeVisible()
  await expect(page.getByText(/seed\s+12345/)).toBeVisible()

  // Reproduce from the stored configuration.
  await page.getByTestId('reproduce').click()
  await expect(page.getByTestId('reproduction-result')).toContainText('identical results')

  // Duplicate the version and replace the bricks.
  await page.getByRole('link', { name: '← E2E Test Deck' }).click()
  page.once('dialog', (dialog) => void dialog.accept('v2 no bricks'))
  await page.getByTestId('version-v1').getByRole('button', { name: 'Duplicate' }).click()
  await expect(page.getByRole('heading', { name: /^v2 no bricks/ })).toBeVisible()
  for (let i = 0; i < 3; i++)
    await page.getByRole('button', { name: 'Remove one Dark Magician', exact: true }).click()
  await addCopies(page, 'Pot of Prosperity', 1)
  await page.getByLabel('Search cards').fill('Monster Reborn')
  await page.getByRole('button', { name: 'Add Monster Reborn', exact: true }).click()
  await page.getByLabel('Search cards').fill('Skill Drain')
  await page.getByRole('button', { name: 'Add Skill Drain', exact: true }).click()
  await expect(page.getByTestId('zone-Main')).toContainText('42 cards')
  await page.getByTestId('save-deck').click()
  await expect(page.getByText('All changes saved')).toBeVisible()

  // The original version is untouched.
  await page.getByRole('link', { name: '← E2E Test Deck' }).click()
  await expect(page.getByTestId('version-v1')).toContainText('Main 42')

  // Compare the two versions.
  await page.getByRole('link', { name: 'Compare latest two' }).click()
  await page.getByTestId('run-compare').click()
  const table = page.getByTestId('comparison-table')
  await expect(table.getByRole('row', { name: /^Brick/ })).toContainText('−32.32 pp')
  await expect(table.getByRole('row', { name: /^Starter/ }).first()).toContainText('0.00 pp')
  const changes = page.getByTestId('card-changes')
  await expect(changes).toContainText('Dark Magician')
  await expect(changes).toContainText('−3')
  await expect(changes).toContainText('Pot of Prosperity')
})

test('invalid decks cannot be simulated', async ({ page }) => {
  await page.goto('/decks')
  await page.getByLabel('Create an empty deck').fill('Too small')
  await page.getByRole('button', { name: 'Create deck' }).click()
  await addCopies(page, 'Raigeki', 3)
  await page.getByTestId('save-deck').click()
  await expect(page.getByText(/needs at least 40/).first()).toBeVisible()

  await page.getByRole('link', { name: 'Simulator' }).click()
  await page.getByLabel('Deck', { exact: true }).selectOption({ label: 'Too small' })
  await expect(page.getByText('This version cannot be simulated yet:')).toBeVisible()
  await expect(page.getByTestId('run-simulation')).toBeDisabled()
})
