import { computed, reactive, ref } from 'vue'
import { defineStore } from 'pinia'
import { api } from '@/api/client'
import type { BanStatus, Validation, VersionDetail } from '@/api/types'
import {
  cardsPayload,
  comboLinesPayload,
  comboLinesProblem,
  draftFromVersion,
  fingerprint,
  rolesPayload,
  NO_BAN_RULES,
  type BanRules,
  type DeckDraft,
} from '@/lib/deckDraft'

const STORAGE_KEY = 'duellab-drafts'

interface StoredDraft {
  draft: DeckDraft
  savedFingerprint: string
}

function readStorage(): Record<string, StoredDraft> {
  try {
    const stored = JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? '{}') as Record<
      string,
      StoredDraft
    >
    // Drafts saved before combo lines existed have no list.
    for (const value of Object.values(stored)) value.draft.comboLines ??= []
    return stored
  } catch {
    return {}
  }
}

function writeStorage(value: Record<string, StoredDraft>): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(value))
  } catch {
    // Storage can be unavailable (private mode, quota); drafts then live only in memory.
  }
}

/**
 * Working copies of deck versions. A draft survives navigation (and page reloads, via
 * sessionStorage) until it is saved or discarded, so moving between the editor and the simulator
 * never loses work.
 */
export const useDeckEditorStore = defineStore('deckEditor', () => {
  const drafts = reactive<Record<string, StoredDraft>>(readStorage())
  const versions = reactive<Record<string, VersionDetail>>({})
  const saving = ref(false)
  // Entries of pinned Forbidden & Limited lists, by list id (card id → status).
  const pinnedLists = reactive<Record<string, Record<number, BanStatus>>>({})

  function persist(): void {
    // Only unsaved drafts are worth keeping across reloads.
    const dirty = Object.fromEntries(Object.entries(drafts).filter(([id]) => isDirty(id)))
    writeStorage(JSON.parse(JSON.stringify(dirty)) as Record<string, StoredDraft>)
  }

  function isDirty(versionId: string): boolean {
    const stored = drafts[versionId]
    return stored !== undefined && fingerprint(stored.draft) !== stored.savedFingerprint
  }

  const dirtyVersionIds = computed(() => Object.keys(drafts).filter(isDirty))

  /** Loads a version. An unsaved draft for it is kept rather than overwritten. */
  async function load(versionId: string): Promise<VersionDetail> {
    const version = await api.versions.get(versionId)
    versions[versionId] = version
    if (version.banlistId && !pinnedLists[version.banlistId]) {
      const list = await api.banlists.get(version.banlistId)
      pinnedLists[version.banlistId] = Object.fromEntries(
        list.entries.map((e) => [e.cardId, e.status]),
      )
    }
    if (!isDirty(versionId)) {
      const draft = draftFromVersion(version)
      drafts[versionId] = { draft, savedFingerprint: fingerprint(draft) }
    }
    return version
  }

  function draft(versionId: string): DeckDraft | undefined {
    return drafts[versionId]?.draft
  }

  /**
   * The Forbidden & Limited rules for a version's deck: none for Casual; the pinned list's entries;
   * or the current list, which is each card's own synced status.
   */
  function banRules(versionId: string): BanRules {
    const version = versions[versionId]
    if (!version || version.format === 'Casual') return NO_BAN_RULES
    const pinned = version.banlistId ? pinnedLists[version.banlistId] : undefined
    return {
      enforced: true,
      statusOf: (card) =>
        version.banlistId ? (pinned?.[card.id] ?? 'Unlimited') : card.tcgBanStatus,
      label: version.banlistLabel,
    }
  }

  /** The server's validation of the last saved state. */
  function validation(versionId: string): Validation | undefined {
    return versions[versionId]?.validation
  }

  /** Saves cards, then roles, then combo lines. Returns the saved version. */
  async function save(versionId: string): Promise<VersionDetail> {
    const stored = drafts[versionId]
    if (!stored) throw new Error('Nothing to save.')
    const problem = comboLinesProblem(stored.draft)
    if (problem) throw new Error(problem)
    saving.value = true
    try {
      await api.versions.saveCards(versionId, cardsPayload(stored.draft))
      await api.versions.saveRoles(versionId, rolesPayload(stored.draft))
      const saved = await api.versions.saveComboLines(versionId, comboLinesPayload(stored.draft))
      versions[versionId] = saved
      const fresh = draftFromVersion(saved)
      drafts[versionId] = { draft: fresh, savedFingerprint: fingerprint(fresh) }
      persist()
      return saved
    } finally {
      saving.value = false
    }
  }

  /** Throws away unsaved changes. */
  function discard(versionId: string): void {
    const version = versions[versionId]
    if (version) {
      const fresh = draftFromVersion(version)
      drafts[versionId] = { draft: fresh, savedFingerprint: fingerprint(fresh) }
    } else {
      delete drafts[versionId]
    }
    persist()
  }

  /** Call after every edit so the draft is kept across reloads. */
  function touched(): void {
    persist()
  }

  return {
    drafts,
    versions,
    saving,
    isDirty,
    dirtyVersionIds,
    load,
    draft,
    validation,
    banRules,
    save,
    discard,
    touched,
  }
})
