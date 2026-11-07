import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { MAX_YDK_BYTES, nameFromFileName, readYdkFile } from '../ydkFile'
import ImportReportBanner from '@/components/ImportReportBanner.vue'

describe('ydk files', () => {
  it('derives a readable name from the file name', () => {
    expect(nameFromFileName('Sky_Striker.ydk')).toBe('Sky Striker')
    expect(nameFromFileName('  my__deck.YDK')).toBe('my deck')
    expect(nameFromFileName('.ydk')).toBe('Imported deck')
  })

  it('reads small files and rejects huge ones', async () => {
    expect(await readYdkFile(new File(['#main\n1\n'], 'a.ydk'))).toBe('#main\n1\n')
    await expect(readYdkFile(new File(['x'.repeat(MAX_YDK_BYTES + 1)], 'big.ydk'))).rejects.toThrow(
      /too large/,
    )
  })
})

describe('ImportReportBanner', () => {
  const base = {
    fileName: 'Sky_Striker.ydk',
    imported: { main: 40, extra: 15, side: 15 },
    alternateArtworks: 3,
    warnings: [],
  }

  it('confirms a clean import', () => {
    const wrapper = mount(ImportReportBanner, {
      props: { report: { ...base, unknown: [], trimmed: [] } },
    })

    expect(wrapper.text()).toContain('Imported Sky_Striker.ydk: 40 Main, 15 Extra, 15 Side.')
    expect(wrapper.text()).toContain('3 alternate-artwork copies were matched')
    expect(wrapper.text()).toContain('Every card was recognised.')
  })

  it('lists skipped and trimmed cards', async () => {
    const wrapper = mount(ImportReportBanner, {
      props: {
        report: {
          ...base,
          unknown: [{ passcode: 99999999, zone: 'Main' as const, copies: 2 }],
          trimmed: [{ cardId: 1, name: 'Raigeki', listed: 4, kept: 3 }],
        },
      },
    })

    expect(wrapper.text()).toContain('99999999 ×2 (Main)')
    expect(wrapper.text()).toContain('Raigeki (listed 4)')
    await wrapper.get('button[aria-label="Dismiss import report"]').trigger('click')
    expect(wrapper.emitted('dismiss')).toHaveLength(1)
  })
})

describe('ImportReportBanner with deck-check errors', () => {
  it('counts them and points to the fix', () => {
    const wrapper = mount(ImportReportBanner, {
      props: {
        deckErrors: 5,
        report: {
          fileName: 'a.ydk',
          imported: { main: 40, extra: 15, side: 15 },
          alternateArtworks: 0,
          warnings: [],
          unknown: [],
          trimmed: [],
        },
      },
    })

    expect(wrapper.text()).toContain('Every card was recognised.')
    expect(wrapper.text()).toContain('5 deck-check problems to fix')
    expect(wrapper.get('[data-testid="import-report"]').classes()).toContain('bg-warning-soft!')
  })
})
