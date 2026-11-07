import { describe, expect, it } from 'vitest'
import type { Scenario } from '@/api/types'
import {
  addAlternative,
  clausesOf,
  distinctMatters,
  removeAlternative,
  setTargets,
  targetsOf,
} from '../scenarios'
import { comboLinesPayload, comboLinesProblem, draftFromVersion, fingerprint } from '../deckDraft'
import { version } from './fixtures'

const plain = (): Scenario => ({
  name: 'S',
  requirements: [{ role: 'Starter', minimumCount: 1 }],
  requireDistinctCards: false,
})

describe('scenario shapes', () => {
  it('treats a plain scenario as one alternative', () => {
    expect(clausesOf(plain())).toEqual([
      { requirements: [{ role: 'Starter', minimumCount: 1 }], requireDistinctCards: false },
    ])
  })

  it('adds and removes OR alternatives, keeping the simplest shape', () => {
    const s = plain()

    addAlternative(s)
    expect(s.requirements).toBeNull()
    expect(s.anyOf).toHaveLength(2)
    expect(s.anyOf![0]!.requireDistinctCards).toBe(false) // carried over from the plain scenario

    removeAlternative(s, 1)
    expect(s.anyOf).toBeNull()
    expect(s.requirements).toEqual([{ role: 'Starter', minimumCount: 1 }])
    removeAlternative(s, 0) // the last alternative stays
    expect(s.requirements).toHaveLength(1)
  })

  it('reads and writes requirement targets', () => {
    const r = { role: 'Starter', minimumCount: 1 }
    setTargets(r, ['Starter', 'Searcher'], [7])
    expect(r).toEqual({ role: null, roles: ['Starter', 'Searcher'], cardIds: [7], minimumCount: 1 })
    expect(targetsOf(r)).toEqual({ roles: ['Starter', 'Searcher'], cardIds: [7] })

    setTargets(r, ['Brick'], [])
    expect(r).toEqual({ role: 'Brick', roles: null, cardIds: null, minimumCount: 1 })
  })

  it('only offers distinct-card semantics when two minimums compete', () => {
    expect(
      distinctMatters({
        requirements: [{ role: 'Starter', minimumCount: 2 }],
        requireDistinctCards: true,
      }),
    ).toBe(false)
    expect(
      distinctMatters({
        requirements: [
          { role: 'Starter', minimumCount: 1 },
          { role: 'Extender', minimumCount: 1 },
        ],
        requireDistinctCards: true,
      }),
    ).toBe(true)
  })
})

describe('combo lines in the draft', () => {
  it('are part of the unsaved-changes fingerprint', () => {
    const draft = draftFromVersion(version())
    const before = fingerprint(draft)

    draft.comboLines.push({
      name: 'Line',
      notes: null,
      pieces: [{ cardIds: [1], roles: [], count: 1 }],
    })

    expect(fingerprint(draft)).not.toBe(before)
  })

  it('are trimmed for the API and checked before saving', () => {
    const draft = draftFromVersion(version())
    draft.comboLines.push({
      name: ' Line ',
      notes: '  ',
      pieces: [{ cardIds: [1], roles: ['Starter'], count: 2 }],
    })
    expect(comboLinesPayload(draft)).toEqual([
      { name: 'Line', notes: null, pieces: [{ cardIds: [1], roles: ['Starter'], count: 2 }] },
    ])
    expect(comboLinesProblem(draft)).toBeNull()

    draft.comboLines.push({
      name: 'line',
      notes: null,
      pieces: [{ cardIds: [2], roles: [], count: 1 }],
    })
    expect(comboLinesProblem(draft)).toMatch(/more than one combo line/)
  })
})
