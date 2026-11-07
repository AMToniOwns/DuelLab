import type { BanStatus, Card, ComboPiece, DeckZone, VersionDetail } from '@/api/types'

/**
 * The client-side working copy of a deck version. The editor changes the draft freely; nothing
 * reaches the server until the user saves. The server stays the authority on validation; these
 * checks only give instant feedback and block obviously impossible edits.
 */
export interface DeckDraft {
  versionId: string
  entries: DraftEntry[]
  roles: Record<number, string[]>
  cards: Record<number, Card>
  comboLines: DraftComboLine[]
}

/** A combo line being edited. */
export interface DraftComboLine {
  name: string
  notes: string | null
  pieces: ComboPiece[]
}

export interface DraftEntry {
  cardId: number
  zone: DeckZone
  quantity: number
}

export const LIMITS = {
  mainMin: 40,
  mainMax: 60,
  extraMax: 15,
  sideMax: 15,
  copies: 3,
} as const

export function zoneMax(zone: DeckZone): number {
  return zone === 'Main' ? LIMITS.mainMax : zone === 'Extra' ? LIMITS.extraMax : LIMITS.sideMax
}

/** Builds a draft from a saved version. */
export function draftFromVersion(version: VersionDetail): DeckDraft {
  const roles: Record<number, string[]> = {}
  const cards: Record<number, Card> = {}
  for (const entry of version.cards) {
    cards[entry.card.id] = entry.card
    roles[entry.card.id] = [...entry.roles]
  }
  return {
    versionId: version.id,
    entries: version.cards.map((c) => ({ cardId: c.card.id, zone: c.zone, quantity: c.quantity })),
    roles,
    cards,
    comboLines: (version.comboLines ?? []).map((l) => ({
      name: l.name,
      notes: l.notes,
      pieces: l.pieces.map((p) => ({
        cardIds: [...p.cardIds],
        roles: [...p.roles],
        count: p.count,
      })),
    })),
  }
}

export function zoneCount(draft: DeckDraft, zone: DeckZone): number {
  return draft.entries.filter((e) => e.zone === zone).reduce((sum, e) => sum + e.quantity, 0)
}

export function copiesOf(draft: DeckDraft, cardId: number): number {
  return draft.entries.filter((e) => e.cardId === cardId).reduce((sum, e) => sum + e.quantity, 0)
}

/** Where a card goes by default: Extra Deck monsters to the Extra Deck, everything else to Main. */
export function defaultZone(card: Card): DeckZone {
  return card.isExtraDeckCard ? 'Extra' : 'Main'
}

/**
 * The Forbidden & Limited list that applies to a deck: off (Casual), or a status per card with a
 * label naming the list.
 */
export interface BanRules {
  enforced: boolean
  statusOf: (card: Card) => BanStatus
  label: string
}

/** No list (Casual): only the 3-copy rule applies. */
export const NO_BAN_RULES: BanRules = {
  enforced: false,
  statusOf: () => 'Unlimited',
  label: 'no list',
}

const BAN_LIMIT: Record<BanStatus, number> = {
  Unlimited: 3,
  SemiLimited: 2,
  Limited: 1,
  Forbidden: 0,
}
const BAN_NAME: Record<BanStatus, string> = {
  Unlimited: 'Unlimited',
  SemiLimited: 'Semi-Limited',
  Limited: 'Limited',
  Forbidden: 'Forbidden',
}

/** The most copies a card may have under the rules. */
export function copyLimit(card: Card, rules: BanRules = NO_BAN_RULES): number {
  return rules.enforced ? Math.min(LIMITS.copies, BAN_LIMIT[rules.statusOf(card)]) : LIMITS.copies
}

/** Why a card cannot be added to a zone, or null if it can. */
export function cannotAdd(
  draft: DeckDraft,
  card: Card,
  zone: DeckZone,
  rules: BanRules = NO_BAN_RULES,
): string | null {
  const limit = copyLimit(card, rules)
  if (limit < LIMITS.copies && copiesOf(draft, card.id) >= limit) {
    const status = BAN_NAME[rules.statusOf(card)]
    return limit === 0
      ? `${card.name} is Forbidden (${rules.label}). Switch the deck to Casual to use it anyway.`
      : `${card.name} is ${status} (${rules.label}): at most ${limit} ${limit === 1 ? 'copy' : 'copies'}.`
  }
  if (copiesOf(draft, card.id) >= LIMITS.copies)
    return `You already have ${LIMITS.copies} copies of ${card.name}.`
  if (zoneCount(draft, zone) >= zoneMax(zone))
    return `The ${zone} Deck is full (${zoneMax(zone)} cards).`
  if (zone === 'Main' && card.isExtraDeckCard) return `${card.name} is an Extra Deck monster.`
  if (zone === 'Extra' && !card.isExtraDeckCard) return `${card.name} cannot go in the Extra Deck.`
  return null
}

/** Adds one copy of a card to a zone. Returns an error message instead of changing the draft if not allowed. */
export function addCard(
  draft: DeckDraft,
  card: Card,
  zone: DeckZone = defaultZone(card),
  rules: BanRules = NO_BAN_RULES,
): string | null {
  const problem = cannotAdd(draft, card, zone, rules)
  if (problem) return problem

  draft.cards[card.id] = card
  draft.roles[card.id] ??= []
  const entry = draft.entries.find((e) => e.cardId === card.id && e.zone === zone)
  if (entry) entry.quantity++
  else draft.entries.push({ cardId: card.id, zone, quantity: 1 })
  return null
}

/** Removes one copy; removes the entry (and the card's roles if it has left the deck) at zero. */
export function removeOne(draft: DeckDraft, cardId: number, zone: DeckZone): void {
  const index = draft.entries.findIndex((e) => e.cardId === cardId && e.zone === zone)
  const entry = draft.entries[index]
  if (!entry) return
  entry.quantity--
  if (entry.quantity <= 0) draft.entries.splice(index, 1)
  if (copiesOf(draft, cardId) === 0) delete draft.roles[cardId]
}

/** Moves one copy between zones (e.g. Main → Side) if the target allows it. */
export function moveOne(
  draft: DeckDraft,
  cardId: number,
  from: DeckZone,
  to: DeckZone,
): string | null {
  const card = draft.cards[cardId]
  if (!card || from === to) return null
  if (zoneCount(draft, to) >= zoneMax(to)) return `The ${to} Deck is full (${zoneMax(to)} cards).`
  if (to === 'Main' && card.isExtraDeckCard) return `${card.name} is an Extra Deck monster.`
  if (to === 'Extra' && !card.isExtraDeckCard) return `${card.name} cannot go in the Extra Deck.`
  const roles = draft.roles[cardId] ?? []
  removeOne(draft, cardId, from)
  draft.roles[cardId] = roles
  const target = draft.entries.find((e) => e.cardId === cardId && e.zone === to)
  if (target) target.quantity++
  else draft.entries.push({ cardId, zone: to, quantity: 1 })
  return null
}

/** Adds or removes a role on a card. */
export function toggleRole(draft: DeckDraft, cardId: number, role: string): void {
  const roles = (draft.roles[cardId] ??= [])
  const index = roles.findIndex((r) => r.toLowerCase() === role.toLowerCase())
  if (index >= 0) roles.splice(index, 1)
  else roles.push(role)
}

/** Copies in the Main Deck that carry a role. */
export function mainCopiesWithRole(draft: DeckDraft, role: string): number {
  return draft.entries
    .filter((e) => e.zone === 'Main' && (draft.roles[e.cardId] ?? []).includes(role))
    .reduce((sum, e) => sum + e.quantity, 0)
}

/** The card list in the shape the API expects. */
export function cardsPayload(draft: DeckDraft) {
  return draft.entries.map(({ cardId, zone, quantity }) => ({ cardId, zone, quantity }))
}

/** The role list in the shape the API expects (only cards still in the deck). */
export function rolesPayload(draft: DeckDraft) {
  const inDeck = new Set(draft.entries.map((e) => e.cardId))
  return Object.entries(draft.roles)
    .map(([id, roles]) => ({ cardId: Number(id), roles }))
    .filter((r) => inDeck.has(r.cardId) && r.roles.length > 0)
}

/** The combo lines in the shape the API expects. */
export function comboLinesPayload(draft: DeckDraft) {
  return (draft.comboLines ?? []).map((l) => ({
    name: l.name.trim(),
    notes: l.notes?.trim() ? l.notes.trim() : null,
    pieces: l.pieces.map((p) => ({ cardIds: [...p.cardIds], roles: [...p.roles], count: p.count })),
  }))
}

/** A problem that would make the server reject the draft's combo lines, or null. */
export function comboLinesProblem(draft: DeckDraft): string | null {
  const names = new Set<string>()
  for (const line of draft.comboLines ?? []) {
    const name = line.name.trim()
    if (!name) return 'Every combo line needs a name.'
    if (names.has(name.toLowerCase())) return `There is more than one combo line called "${name}".`
    names.add(name.toLowerCase())
    if (line.pieces.length === 0) return `Combo line "${name}" needs at least one piece.`
    if (line.pieces.some((p) => p.cardIds.length + p.roles.length === 0))
      return `Every piece of "${name}" needs at least one card or role.`
  }
  return null
}

/** A stable fingerprint of the draft's contents, used to detect unsaved changes. */
export function fingerprint(draft: DeckDraft): string {
  const entries = [...cardsPayload(draft)].sort(
    (a, b) => a.cardId - b.cardId || a.zone.localeCompare(b.zone),
  )
  const roles = rolesPayload(draft)
    .map((r) => ({ cardId: r.cardId, roles: [...r.roles].sort() }))
    .sort((a, b) => a.cardId - b.cardId)
  return JSON.stringify({ entries, roles, combos: comboLinesPayload(draft) })
}
