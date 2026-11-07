import { describe, expect, it } from 'vitest'
import { suggestRoles } from '../roleSuggestions'
import { card } from './fixtures'

const withText = (
  id: number,
  name: string,
  description: string,
  kind: 'Monster' | 'Spell' | 'Trap' = 'Monster',
) => ({
  ...card(id, name),
  kind,
  description,
})

describe('role suggestions', () => {
  it('suggests well-known hand traps and board breakers by name, ticked by default', () => {
    const result = suggestRoles(
      [card(1, 'Ash Blossom & Joyous Spring'), card(2, 'Evenly Matched'), card(3, 'Dark Magician')],
      {},
    )

    expect(result).toEqual([
      expect.objectContaining({
        cardName: 'Ash Blossom & Joyous Spring',
        role: 'HandTrap',
        source: 'known',
        recommended: true,
      }),
      expect.objectContaining({
        cardName: 'Evenly Matched',
        role: 'BoardBreaker',
        source: 'known',
        recommended: true,
      }),
    ])
  })

  it('suggests searchers from card text and quotes it', () => {
    const [s] = suggestRoles(
      [
        withText(
          1,
          'Searcher',
          'You can add 1 Level 4 monster from your Deck to your hand. You can only use this effect once per turn.',
        ),
      ],
      {},
    )

    expect(s).toMatchObject({ role: 'Searcher', source: 'text', recommended: true })
    expect(s!.reason).toContain('add 1 Level 4 monster from your Deck to your hand')
  })

  it('offers text-based hand traps unticked, and never for Spells', () => {
    const result = suggestRoles(
      [
        withText(
          1,
          'Maybe',
          'During the Main Phase (Quick Effect): You can discard this card; draw 1 card.',
        ),
        withText(2, 'A spell', 'You can activate this card from your hand.', 'Spell'),
      ],
      {},
    )

    expect(result).toEqual([
      expect.objectContaining({
        cardName: 'Maybe',
        role: 'HandTrap',
        source: 'possible',
        recommended: false,
      }),
    ])
  })

  it('does not guess "possible hand trap" for cards on the curated lists', () => {
    const result = suggestRoles(
      [
        withText(
          1,
          'Evenly Matched',
          'If you control no cards, you can activate this card from your hand.',
          'Trap',
        ),
      ],
      {},
    )

    expect(result).toEqual([expect.objectContaining({ role: 'BoardBreaker', source: 'known' })])
  })

  it('skips roles a card already has and never suggests starters, extenders or bricks', () => {
    const result = suggestRoles(
      [card(1, 'Ash Blossom & Joyous Spring'), card(2, 'Snake-Eye Ash')],
      { 1: ['HandTrap'] },
    )

    expect(result).toEqual([])
  })

  it('suggests once per card even if it appears in two zones', () => {
    expect(suggestRoles([card(1, 'Raigeki'), card(1, 'Raigeki')], {})).toHaveLength(1)
  })
})
