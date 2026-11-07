import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useDeckEditorStore } from '../deckEditor'
import { addCard } from '@/lib/deckDraft'
import { card, version } from '@/lib/__tests__/fixtures'
import type { VersionDetail } from '@/api/types'

function mockFetch(handler: (url: string, init?: RequestInit) => unknown) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const body = handler(String(input), init)
    return new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    })
  })
}

describe('deckEditor store', () => {
  let saved: VersionDetail

  beforeEach(() => {
    sessionStorage.clear()
    setActivePinia(createPinia())
    saved = version([{ card: card(1, 'Ash'), zone: 'Main', quantity: 1, roles: [] }])
    vi.restoreAllMocks()
  })

  it('tracks unsaved changes and keeps a dirty draft when the version is reloaded', async () => {
    mockFetch(() => saved)
    const store = useDeckEditorStore()
    await store.load('v1')
    expect(store.isDirty('v1')).toBe(false)

    addCard(store.draft('v1')!, card(2, 'Veiler'))
    store.touched()
    expect(store.isDirty('v1')).toBe(true)

    await store.load('v1') // e.g. navigating back to the editor
    expect(store.draft('v1')!.entries).toHaveLength(2)
    expect(store.dirtyVersionIds).toEqual(['v1'])
  })

  it('survives a page reload through sessionStorage', async () => {
    mockFetch(() => saved)
    const store = useDeckEditorStore()
    await store.load('v1')
    addCard(store.draft('v1')!, card(2, 'Veiler'))
    store.touched()

    setActivePinia(createPinia())
    const reloaded = useDeckEditorStore()

    expect(reloaded.isDirty('v1')).toBe(true)
    expect(reloaded.draft('v1')!.entries).toHaveLength(2)
  })

  it('saves cards then roles and becomes clean', async () => {
    const calls: string[] = []
    mockFetch((url, init) => {
      calls.push(`${init?.method ?? 'GET'} ${url}`)
      if (url.endsWith('/roles')) {
        const body = JSON.parse(String(init?.body)) as {
          cards: { cardId: number; roles: string[] }[]
        }
        return version([
          { card: card(1, 'Ash'), zone: 'Main', quantity: 1, roles: body.cards[0]?.roles ?? [] },
        ])
      }
      return saved
    })
    const store = useDeckEditorStore()
    await store.load('v1')
    store.draft('v1')!.roles[1] = ['Starter']

    await store.save('v1')

    expect(calls).toEqual([
      'GET /api/versions/v1',
      'PUT /api/versions/v1/cards',
      'PUT /api/versions/v1/roles',
    ])
    expect(store.isDirty('v1')).toBe(false)
    expect(store.draft('v1')!.roles[1]).toEqual(['Starter'])
  })

  it('discards back to the saved state', async () => {
    mockFetch(() => saved)
    const store = useDeckEditorStore()
    await store.load('v1')
    addCard(store.draft('v1')!, card(2, 'Veiler'))

    store.discard('v1')

    expect(store.isDirty('v1')).toBe(false)
    expect(store.draft('v1')!.entries).toHaveLength(1)
  })
})
