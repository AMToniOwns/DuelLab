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
