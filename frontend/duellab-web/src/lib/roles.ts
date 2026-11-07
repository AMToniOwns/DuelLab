/** A built-in analytical role. Keys match the API (DuelLab.Domain.Decks.CardRoleKind). */
export interface RoleDefinition {
  key: string
  label: string
  hint: string
}

export const BUILT_IN_ROLES: readonly RoleDefinition[] = [
  { key: 'Starter', label: 'Starter', hint: 'Starts your main play on its own' },
  { key: 'Extender', label: 'Extender', hint: 'Extends a play once it has started' },
  { key: 'Searcher', label: 'Searcher', hint: 'Adds a card from the deck to the hand' },
  { key: 'HandTrap', label: 'Hand trap', hint: 'Used from the hand on the opponent’s turn' },
  { key: 'BoardBreaker', label: 'Board breaker', hint: 'Answers an established board' },
  { key: 'Brick', label: 'Brick', hint: 'A card you would rather not open' },
]

const CUSTOM_PREFIX = 'Custom:'

/** True for keys like "Custom:Fusion line". */
export function isCustomRole(key: string): boolean {
  return key.startsWith(CUSTOM_PREFIX)
}

/** The key for a custom role name. */
export function customRoleKey(name: string): string {
  return `${CUSTOM_PREFIX}${name.trim()}`
}

/** A readable label for any role key. */
export function roleLabel(key: string): string {
  if (isCustomRole(key)) return key.slice(CUSTOM_PREFIX.length)
  return BUILT_IN_ROLES.find((r) => r.key === key)?.label ?? key
}

/** All role keys used in a deck plus the built-in ones, built-ins first. */
export function availableRoles(usedKeys: Iterable<string>): string[] {
  const keys = new Set(BUILT_IN_ROLES.map((r) => r.key))
  const custom = [...new Set(usedKeys)].filter(isCustomRole).sort((a, b) => a.localeCompare(b))
  return [...keys, ...custom]
}
