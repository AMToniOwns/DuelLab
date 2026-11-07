import { computed, reactive, ref } from 'vue'
import { defineStore } from 'pinia'
import { api } from '@/api/client'
import type { Validation, VersionDetail } from '@/api/types'
import {
  cardsPayload,
  draftFromVersion,
  fingerprint,
  rolesPayload,
  type DeckDraft,
} from '@/lib/deckDraft'

const STORAGE_KEY = 'duellab-drafts'

interface StoredDraft {
  draft: DeckDraft
  savedFingerprint: string
}

function readStorage(): Record<string, StoredDraft> {
  try {
    return JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? '{}') as Record<string, StoredDraft>
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
    if (!isDirty(versionId)) {
      const draft = draftFromVersion(version)
      drafts[versionId] = { draft, savedFingerprint: fingerprint(draft) }
    }
    return version
  }

  function draft(versionId: string): DeckDraft | undefined {
    return drafts[versionId]?.draft
  }

  /** The server's validation of the last saved state. */
  function validation(versionId: string): Validation | undefined {
    return versions[versionId]?.validation
  }

  /** Saves cards, then roles. Returns the saved version. */
  async function save(versionId: string): Promise<VersionDetail> {
    const stored = drafts[versionId]
    if (!stored) throw new Error('Nothing to save.')
    saving.value = true
    try {
      await api.versions.saveCards(versionId, cardsPayload(stored.draft))
      const saved = await api.versions.saveRoles(versionId, rolesPayload(stored.draft))
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
    save,
    discard,
    touched,
  }
})
