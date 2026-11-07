import type { Card, VersionDetail } from '@/api/types'

export function card(id: number, name: string, extra = false): Card {
  return {
    id,
    externalId: 10_000_000 + id,
    name,
    description: '',
    kind: extra ? 'Monster' : 'Spell',
    typeLine: extra ? 'Link Monster' : 'Normal Spell',
    isExtraDeckCard: extra,
    attribute: null,
    race: null,
    archetype: null,
    level: null,
    attack: null,
    defense: null,
    linkValue: null,
    tcgBanStatus: 'Unlimited',
    lastSyncedAt: '2026-11-07T00:00:00Z',
    imageUrl: `/api/cards/${id}/image?size=full`,
    smallImageUrl: `/api/cards/${id}/image?size=small`,
  }
}

export function version(cards: VersionDetail['cards'] = []): VersionDetail {
  return {
    id: 'v1',
    deckId: 'd1',
    deckName: 'Deck',
    versionName: 'v1',
    notes: null,
    parentVersionId: null,
    createdAt: '2026-11-07T00:00:00Z',
    updatedAt: '2026-11-07T00:00:00Z',
    cards,
    comboLines: [],
    validation: { isValid: false, mainCount: 0, extraCount: 0, sideCount: 0, issues: [] },
  }
}
