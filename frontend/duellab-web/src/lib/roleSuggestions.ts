import type { Card } from '@/api/types'

/**
 * Role suggestions from information that does not depend on the deck.
 *
 * - Hand traps and board breakers: a curated list of well-known cards, matched by exact name
 *   (names were checked against the TCG card data in November 2026). These are ticked by default.
 * - Searchers: card text that adds a card "from your Deck to your hand". This is close to the
 *   definition of a searcher, so these are ticked by default and quote the text.
 * - Possible hand traps: text such as "(Quick Effect): You can discard this card". Many such cards
 *   are not hand traps (they discard themselves on your own turn), so these start unticked.
 *
 * Starter, extender and brick depend on the deck (a card can start one deck and brick another), so
 * they are never suggested.
 */

export type SuggestionSource = 'known' | 'text' | 'possible'

export interface RoleSuggestion {
  cardId: number
  cardName: string
  role: string
  source: SuggestionSource
  reason: string
  recommended: boolean
}

export const KNOWN_HAND_TRAPS: readonly string[] = [
  'Ash Blossom & Joyous Spring',
  'Ghost Ogre & Snow Rabbit',
  'Ghost Belle & Haunted Mansion',
  'Ghost Mourner & Moonlit Chill',
  'Ghost Sister & Spooky Dogwood',
  'Ghost Reaper & Winter Cherries',
  'Droll & Lock Bird',
  'Effect Veiler',
  'Infinite Impermanence',
  'Nibiru, the Primal Being',
  'Maxx "C"',
  'Mulcharmy Fuwalos',
  'Mulcharmy Purulia',
  'Mulcharmy Meowls',
  'D.D. Crow',
  'PSY-Framegear Gamma',
  'Dimension Shifter',
  'Skull Meister',
  'Artifact Lancea',
  'Bystial Magnamhut',
  'Dominus Impulse',
  'Dominus Purge',
  'Dominus Spark',
  'Contact "C"',
  'Retaliating "C"',
]

export const KNOWN_BOARD_BREAKERS: readonly string[] = [
  'Dark Ruler No More',
  'Evenly Matched',
  'Lightning Storm',
  "Harpie's Feather Duster",
  'Raigeki',
  'Forbidden Droplet',
  'Triple Tactics Talent',
  'Book of Eclipse',
  'Cosmic Cyclone',
  'Twin Twisters',
  'Mystical Space Typhoon',
  'Heavy Storm',
  'Interrupted Kaiju Slumber',
  'The Winged Dragon of Ra - Sphere Mode',
  'Lava Golem',
  'Gameciel, the Sea Turtle Kaiju',
  'Dogoran, the Mad Flame Kaiju',
  'Gadarla, the Mystery Dust Kaiju',
  'Kumongous, the Sticky String Kaiju',
  'Jizukiru, the Star Destroying Kaiju',
  'Radian, the Multidimensional Kaiju',
  'Thunder King, the Lightningstrike Kaiju',
  'Super Anti-Kaiju War Machine Mecha-Dogoran',
  "Ice Dragon's Prison",
  'Super Polymerization',
]

const SEARCH =
  /add (?:1|one|2|two|up to \w+) [^.]{0,120}? from your Deck(?:,? or (?:GY|Graveyard))? to your hand/i
const POSSIBLE_HAND_TRAP = [
  /\(Quick Effect\): You can discard this card/i,
  /\(Quick Effect\): You can send this card from your hand to the GY/i,
  /you can activate this card from your hand/i,
]

const knownHandTraps = new Set(KNOWN_HAND_TRAPS)
const knownBreakers = new Set(KNOWN_BOARD_BREAKERS)

function quote(text: string, pattern: RegExp): string {
  const match = text.replace(/\s+/g, ' ').match(pattern)?.[0] ?? ''
  return match.length > 90 ? `${match.slice(0, 87)}…` : match
}

/** Suggestions for the given cards, skipping roles a card already has. */
export function suggestRoles(cards: Card[], existing: Record<number, string[]>): RoleSuggestion[] {
  const suggestions: RoleSuggestion[] = []
  const seen = new Set<number>()

  for (const card of [...cards].sort((a, b) => a.name.localeCompare(b.name))) {
    if (seen.has(card.id)) continue
    seen.add(card.id)
    const has = new Set(existing[card.id] ?? [])
    const add = (role: string, source: SuggestionSource, reason: string) => {
      if (has.has(role) || suggestions.some((s) => s.cardId === card.id && s.role === role)) return
      suggestions.push({
        cardId: card.id,
        cardName: card.name,
        role,
        source,
        reason,
        recommended: source !== 'possible',
      })
    }

    if (knownHandTraps.has(card.name)) add('HandTrap', 'known', 'Well-known hand trap')
    if (knownBreakers.has(card.name)) add('BoardBreaker', 'known', 'Well-known board breaker')

    const text = card.description ?? ''
    if (SEARCH.test(text)) add('Searcher', 'text', `Card text: “${quote(text, SEARCH)}”`)

    // Cards on either curated list are already classified; a text guess would only add noise.
    if (!knownHandTraps.has(card.name) && !knownBreakers.has(card.name) && card.kind !== 'Spell') {
      const pattern = POSSIBLE_HAND_TRAP.find((p) => p.test(text))
      if (pattern)
        add(
          'HandTrap',
          'possible',
          `Possible: card text “${quote(text, pattern)}”. Check when it can be used.`,
        )
    }
  }

  return suggestions
}
