import { describe, expect, it } from 'vitest'
import {
  addCard,
  cardsPayload,
  copiesOf,
  draftFromVersion,
  fingerprint,
  mainCopiesWithRole,
  moveOne,
  removeOne,
  rolesPayload,
  toggleRole,
  zoneCount,
} from '../deckDraft'
import { card, version } from './fixtures'

const empty = () => draftFromVersion(version())

describe('deckDraft', () => {
  it('adds cards to their default zone and stacks copies', () => {
    const draft = empty()
    const spell = card(1, 'Pot')
    const link = card(2, 'Link', true)

    expect(addCard(draft, spell)).toBeNull()
    expect(addCard(draft, spell)).toBeNull()
    expect(addCard(draft, link)).toBeNull()

    expect(zoneCount(draft, 'Main')).toBe(2)
    expect(zoneCount(draft, 'Extra')).toBe(1)
    expect(draft.entries).toHaveLength(2)
  })

  it('enforces three copies across all zones', () => {
    const draft = empty()
    const spell = card(1, 'Pot')
    addCard(draft, spell, 'Main')
    addCard(draft, spell, 'Main')
    addCard(draft, spell, 'Side')

    expect(addCard(draft, spell, 'Side')).toMatch(/already have 3 copies/)
    expect(copiesOf(draft, 1)).toBe(3)
  })

  it('keeps Extra Deck monsters out of the Main Deck and vice versa', () => {
    const draft = empty()

    expect(addCard(draft, card(2, 'Link', true), 'Main')).toMatch(/Extra Deck monster/)
    expect(addCard(draft, card(1, 'Pot'), 'Extra')).toMatch(/cannot go in the Extra Deck/)
    expect(addCard(draft, card(2, 'Link', true), 'Side')).toBeNull()
  })

  it('stops at a full zone', () => {
    const draft = empty()
    for (let i = 1; i <= 15; i++) addCard(draft, card(100 + i, `L${i}`, true))

    expect(addCard(draft, card(200, 'One more', true))).toMatch(/Extra Deck is full/)
  })

  it('removes entries and their roles when the last copy goes', () => {
    const draft = empty()
    addCard(draft, card(1, 'Pot'))
    toggleRole(draft, 1, 'Starter')

    removeOne(draft, 1, 'Main')

    expect(draft.entries).toHaveLength(0)
    expect(draft.roles[1]).toBeUndefined()
  })

  it('keeps roles when moving a copy to the Side Deck', () => {
    const draft = empty()
    addCard(draft, card(1, 'Pot'))
    toggleRole(draft, 1, 'Brick')

    expect(moveOne(draft, 1, 'Main', 'Side')).toBeNull()

    expect(zoneCount(draft, 'Main')).toBe(0)
    expect(zoneCount(draft, 'Side')).toBe(1)
    expect(draft.roles[1]).toEqual(['Brick'])
  })

  it('toggles roles case-insensitively and counts Main Deck copies per role', () => {
    const draft = empty()
    addCard(draft, card(1, 'Ash'))
    addCard(draft, card(1, 'Ash'))
    addCard(draft, card(1, 'Ash'), 'Side')
    toggleRole(draft, 1, 'Custom:Combo')
    toggleRole(draft, 1, 'custom:combo')
    toggleRole(draft, 1, 'HandTrap')

    expect(draft.roles[1]).toEqual(['HandTrap'])
    expect(mainCopiesWithRole(draft, 'HandTrap')).toBe(2)
  })

  it('builds API payloads and only sends roles for cards still in the deck', () => {
    const draft = empty()
    addCard(draft, card(1, 'Ash'))
    toggleRole(draft, 1, 'Starter')
    draft.roles[99] = ['Brick'] // stale entry

    expect(cardsPayload(draft)).toEqual([{ cardId: 1, zone: 'Main', quantity: 1 }])
    expect(rolesPayload(draft)).toEqual([{ cardId: 1, roles: ['Starter'] }])
  })

  it('fingerprints ignore order but notice real changes', () => {
    const a = empty()
    const b = empty()
    addCard(a, card(1, 'A'))
    addCard(a, card(2, 'B'))
    addCard(b, card(2, 'B'))
    addCard(b, card(1, 'A'))
    expect(fingerprint(a)).toBe(fingerprint(b))

    toggleRole(b, 1, 'Starter')
    expect(fingerprint(a)).not.toBe(fingerprint(b))
  })

  it('round-trips a saved version', () => {
    const saved = version([
      { card: card(1, 'Ash'), zone: 'Main', quantity: 3, roles: ['Starter', 'HandTrap'] },
    ])

    const draft = draftFromVersion(saved)

    expect(copiesOf(draft, 1)).toBe(3)
    expect(draft.roles[1]).toEqual(['Starter', 'HandTrap'])
  })
})
