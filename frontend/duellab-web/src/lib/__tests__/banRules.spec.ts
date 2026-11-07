import { describe, expect, it } from 'vitest'
import type { BanStatus } from '@/api/types'
import {
  addCard,
  cannotAdd,
  copyLimit,
  draftFromVersion,
  NO_BAN_RULES,
  type BanRules,
} from '../deckDraft'
import { card, version } from './fixtures'

const listed = (status: BanStatus) => ({ ...card(1, 'Pot of Prosperity'), tcgBanStatus: status })
const current: BanRules = {
  enforced: true,
  statusOf: (c) => c.tcgBanStatus,
  label: 'current TCG list',
}

describe('Forbidden & Limited limits in the editor', () => {
  it('caps copies by status when the list is enforced', () => {
    expect(copyLimit(listed('Limited'), current)).toBe(1)
    expect(copyLimit(listed('SemiLimited'), current)).toBe(2)
    expect(copyLimit(listed('Forbidden'), current)).toBe(0)
    expect(copyLimit(listed('Unlimited'), current)).toBe(3)
    expect(copyLimit(listed('Forbidden'), NO_BAN_RULES)).toBe(3)
  })

  it('refuses a second copy of a Limited card, counting the Side Deck', () => {
    const draft = draftFromVersion(version())
    const pot = listed('Limited')

    expect(addCard(draft, pot, 'Side', current)).toBeNull()
    expect(cannotAdd(draft, pot, 'Main', current)).toBe(
      'Pot of Prosperity is Limited (current TCG list): at most 1 copy.',
    )
  })

  it('refuses Forbidden cards and suggests Casual', () => {
    expect(cannotAdd(draftFromVersion(version()), listed('Forbidden'), 'Main', current)).toMatch(
      /Forbidden.*Casual/,
    )
  })

  it('allows listed cards up to 3 copies in Casual', () => {
    const draft = draftFromVersion(version())
    const pot = listed('Forbidden')
    for (let i = 0; i < 3; i++) expect(addCard(draft, pot, 'Main', NO_BAN_RULES)).toBeNull()
    expect(addCard(draft, pot, 'Main', NO_BAN_RULES)).toMatch(/already have 3 copies/)
  })

  it('uses a pinned list instead of the card’s current status', () => {
    const pinned: BanRules = {
      enforced: true,
      statusOf: () => 'Unlimited',
      label: 'TCG list first seen 2026-05-01',
    }

    expect(copyLimit(listed('Forbidden'), pinned)).toBe(3)
  })
})
