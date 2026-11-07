import { describe, expect, it } from 'vitest'
import { percent, pointDelta, relativeTime } from '../format'
import { availableRoles, customRoleKey, isCustomRole, roleLabel } from '../roles'

describe('format', () => {
  it('formats probabilities as percentages', () => {
    expect(percent(0.90514)).toBe('90.5%')
    expect(percent(0.90514, 2)).toBe('90.51%')
    expect(percent(null)).toBe('—')
  })

  it('formats signed percentage-point changes', () => {
    expect(pointDelta(4.49)).toBe('+4.5 pp')
    expect(pointDelta(-32.32, 2)).toBe('−32.32 pp')
    expect(pointDelta(0.004)).toBe('0.0 pp')
    expect(pointDelta(-0.004, 2)).toBe('0.00 pp')
    expect(pointDelta(null)).toBe('—')
  })

  it('formats relative times', () => {
    const now = new Date('2026-11-07T12:00:00Z')
    expect(relativeTime('2026-11-07T11:57:00Z', now)).toMatch(/3 minutes ago/)
  })
})

describe('roles', () => {
  it('labels built-in and custom roles', () => {
    expect(roleLabel('HandTrap')).toBe('Hand trap')
    expect(roleLabel(customRoleKey('  Fusion line '))).toBe('Fusion line')
    expect(isCustomRole('Custom:X')).toBe(true)
    expect(isCustomRole('Starter')).toBe(false)
  })

  it('lists built-ins first, then custom roles in use', () => {
    const roles = availableRoles(['Custom:Zeta', 'Starter', 'Custom:Alpha'])

    expect(roles.slice(0, 6)).toEqual([
      'Starter',
      'Extender',
      'Searcher',
      'HandTrap',
      'BoardBreaker',
      'Brick',
    ])
    expect(roles.slice(6)).toEqual(['Custom:Alpha', 'Custom:Zeta'])
  })
})
