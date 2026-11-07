import { expect, test } from '@playwright/test'

/** Importing and exporting .ydk deck files. */

const MAIN = [
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

test('import a .ydk file as a deck, export it, and import it again as a version', async ({
  page,
  request,
}) => {
  await expect
    .poll(async () => (await (await request.get('/api/cards/status')).json()).cardCount, {
      timeout: 30_000,
    })
    .toBeGreaterThan(0)

  const passcode: Record<string, string> = {}
  for (const name of [...MAIN, 'Accesscode Talker', 'Called by the Grave']) {
    const found = await (await request.get(`/api/cards?search=${encodeURIComponent(name)}`)).json()
    passcode[name] = String(found.items.find((c: { name: string }) => c.name === name).externalId)
  }

  // Quirks from real exports: CRLF, padded headers, alternate artwork (14558128 = Ash Blossom),
  // an unknown passcode, and a 4th copy of Raigeki in the Side Deck.
  const lines = [
    '#main ',
    '14558128',
    '14558128',
    '14558128',
    ...MAIN.flatMap((n) => [passcode[n]!, passcode[n]!, passcode[n]!]),
  ]
  lines.push(
    passcode['Called by the Grave']!,
    '12345678',
    '#extra ',
    passcode['Accesscode Talker']!,
    '!side ',
    passcode['Raigeki']!,
  )
  const file = {
    name: 'My_Test_Deck.ydk',
    mimeType: 'text/plain',
    buffer: Buffer.from(lines.join('\r\n')),
  }

  await page.goto('/decks')
  await page.getByTestId('ydk-file').setInputFiles(file)
  await expect(page.getByLabel('Deck name')).toHaveValue('My Test Deck')
  await page.getByLabel('Format').selectOption('Casual')
  await page.getByTestId('import-ydk').click()

  const report = page.getByTestId('import-report')
  await expect(report).toContainText('Imported My_Test_Deck.ydk: 40 Main, 1 Extra, 0 Side.')
  await expect(report).toContainText('3 alternate-artwork copies were matched')
  await expect(report).toContainText('12345678 ×1 (Main)')
  await expect(report).toContainText('Raigeki (listed 4)')
  await expect(page.getByTestId('zone-Main')).toContainText('40 cards')
  await expect(page.getByTestId('deck-format')).toContainText('Casual')
  await expect(page.getByText('Ready to simulate.')).toBeVisible()
  await page.getByRole('button', { name: 'Dismiss import report' }).click()
  await expect(report).toBeHidden()

  // Export from the deck page, then import that file again as a new version.
  await page.getByRole('link', { name: '← My Test Deck' }).click()
  const download = page.waitForEvent('download')
  await page.getByTestId('export-v1').click()
  const exported = await download
  expect(exported.suggestedFilename()).toBe('My Test Deck - v1.ydk')
  const text = await (
    await exported.createReadStream()
  )
    .toArray()
    .then((chunks) => Buffer.concat(chunks).toString())
  expect(text).toContain('#main\n14558127\n') // written with Ash's main passcode

  page.once('dialog', (dialog) => void dialog.accept('re-imported'))
  await page
    .getByTestId('ydk-version-file')
    .setInputFiles({ name: 'export.ydk', mimeType: 'text/plain', buffer: Buffer.from(text) })
  await expect(page.getByTestId('import-report')).toContainText('Every card was recognised.')
  await expect(page.getByRole('heading', { name: /^re-imported/ })).toBeVisible()
  await expect(page.getByTestId('zone-Main')).toContainText('40 cards')
})

test('load a .ydk file into an existing version from the editor, review, discard or save', async ({
  page,
  request,
}) => {
  const passcode: Record<string, string> = {}
  const id: Record<string, number> = {}
  for (const name of [...MAIN, 'Pot of Prosperity']) {
    const found = await (await request.get(`/api/cards?search=${encodeURIComponent(name)}`)).json()
    const c = found.items.find((x: { name: string }) => x.name === name)
    passcode[name] = String(c.externalId)
    id[name] = c.id
  }

  // An existing deck: 12 cards × 3 + 3 Ash + 1 Pot of Prosperity = 40, with Effect Veiler as a starter.
  const ashId = (await (await request.get('/api/cards?search=Ash%20Blossom')).json()).items[0].id
  const deck = await (await request.post('/api/decks', { data: { name: 'Editor import' } })).json()
  const versionId: string = deck.versions[0].id
  await request.put(`/api/versions/${versionId}/cards`, {
    data: {
      cards: [
        ...MAIN.map((n) => ({ cardId: id[n], zone: 'Main', quantity: 3 })),
        { cardId: ashId, zone: 'Main', quantity: 3 },
        { cardId: id['Pot of Prosperity'], zone: 'Main', quantity: 1 },
      ],
    },
  })
  await request.put(`/api/versions/${versionId}/roles`, {
    data: { cards: [{ cardId: id['Effect Veiler'], roles: ['Starter'] }] },
  })

  // The file drops Raigeki and Pot of Prosperity and has 2 more Ash (alternate artwork) instead…
  const file = [
    '#main',
    ...MAIN.filter((n) => n !== 'Raigeki').flatMap((n) => [
      passcode[n]!,
      passcode[n]!,
      passcode[n]!,
    ]),
    '14558128',
    '14558128',
    '14558128',
  ].join('\n')

  await page.goto(`/versions/${versionId}/edit`)
  await expect(page.getByTestId('zone-Main')).toContainText('40 cards')
  page.once('dialog', (dialog) => void dialog.accept())
  await page
    .getByTestId('editor-ydk-file')
    .setInputFiles({ name: 'updated.ydk', mimeType: 'text/plain', buffer: Buffer.from(file) })

  // …loaded as an unsaved draft: 36 cards, Effect Veiler keeps its role, nothing saved yet.
  await expect(page.getByTestId('import-report')).toContainText('Imported updated.ydk: 36 Main')
  await expect(page.getByTestId('import-unsaved')).toBeVisible()
  await expect(page.getByTestId('zone-Main')).toContainText('36 cards')
  await expect(page.getByText('● Unsaved changes')).toBeVisible()
  await expect(
    page.getByTestId('entry-Main-Effect Veiler').getByText('Starter', { exact: true }),
  ).toBeVisible()
  await expect(page.getByTestId('entry-Main-Raigeki')).toHaveCount(0)
  expect(
    (await (await request.get(`/api/versions/${versionId}`)).json()).validation.mainCount,
  ).toBe(40)

  // Discard restores the saved deck.
  page.once('dialog', (dialog) => void dialog.accept())
  await page.getByRole('button', { name: 'Discard' }).click()
  await expect(page.getByTestId('zone-Main')).toContainText('40 cards')

  // Load again and save: now it is stored.
  page.once('dialog', (dialog) => void dialog.accept())
  await page
    .getByTestId('editor-ydk-file')
    .setInputFiles({ name: 'updated.ydk', mimeType: 'text/plain', buffer: Buffer.from(file) })
  await expect(page.getByTestId('zone-Main')).toContainText('36 cards')
  await page.getByTestId('save-deck').click()
  await expect(page.getByText('All changes saved')).toBeVisible()
  const saved = await (await request.get(`/api/versions/${versionId}`)).json()
  expect(saved.validation.mainCount).toBe(36)
  expect(
    saved.cards.find((c: { card: { name: string } }) => c.card.name === 'Effect Veiler').roles,
  ).toEqual(['Starter'])
})
