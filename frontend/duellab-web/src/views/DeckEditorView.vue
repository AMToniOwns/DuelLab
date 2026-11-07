<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { api, errorMessage } from '@/api/client'
import { DECK_ZONES, type Card, type DeckZone, type ValidationIssue } from '@/api/types'
import {
  addCard,
  copiesOf,
  mainCopiesWithRole,
  moveOne,
  removeOne,
  toggleRole,
  zoneCount,
  LIMITS,
} from '@/lib/deckDraft'
import { availableRoles, BUILT_IN_ROLES, customRoleKey, roleLabel } from '@/lib/roles'
import AsyncState from '@/components/AsyncState.vue'
import BanBadge from '@/components/BanBadge.vue'
import CardDetail from '@/components/CardDetail.vue'
import CardImage from '@/components/CardImage.vue'
import CardSearch from '@/components/CardSearch.vue'
import ModalDialog from '@/components/ModalDialog.vue'
import ValidationPanel from '@/components/ValidationPanel.vue'
import { useDeckEditorStore } from '@/stores/deckEditor'
import { useToastStore } from '@/stores/toasts'

const props = defineProps<{ versionId: string }>()
const router = useRouter()
const editor = useDeckEditorStore()
const toasts = useToastStore()

const loading = ref(true)
const error = ref<string | null>(null)
const detailCard = ref<Card | null>(null)
const editingDetails = ref(false)
const versionName = ref('')
const notes = ref('')
const newRole = ref<Record<number, string>>({})

const version = computed(() => editor.versions[props.versionId])
const draft = computed(() => editor.draft(props.versionId))
const dirty = computed(() => editor.isDirty(props.versionId))

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const loaded = await editor.load(props.versionId)
    versionName.value = loaded.versionName
    notes.value = loaded.notes ?? ''
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}
watch(() => props.versionId, load, { immediate: true })

function entriesIn(zone: DeckZone) {
  const d = draft.value
  if (!d) return []
  const order = { Monster: 0, Spell: 1, Trap: 2 }
  return d.entries
    .filter((e) => e.zone === zone)
    .map((e) => ({ ...e, card: d.cards[e.cardId]! }))
    .sort(
      (a, b) => order[a.card.kind] - order[b.card.kind] || a.card.name.localeCompare(b.card.name),
    )
}

function add(card: Card, zone?: DeckZone): void {
  if (!draft.value) return
  const problem = addCard(draft.value, card, zone)
  if (problem) toasts.error(problem)
  editor.touched()
}

function remove(cardId: number, zone: DeckZone): void {
  if (!draft.value) return
  removeOne(draft.value, cardId, zone)
  editor.touched()
}

function move(cardId: number, from: DeckZone, to: DeckZone): void {
  if (!draft.value) return
  const problem = moveOne(draft.value, cardId, from, to)
  if (problem) toasts.error(problem)
  editor.touched()
}

function toggle(cardId: number, role: string): void {
  if (!draft.value) return
  toggleRole(draft.value, cardId, role)
  editor.touched()
}

function addCustomRole(cardId: number): void {
  const name = (newRole.value[cardId] ?? '').trim()
  if (!name) return
  if (name.length > 100) {
    toasts.error('A custom role name can be at most 100 characters.')
    return
  }
  const key = customRoleKey(name)
  if (!(draft.value?.roles[cardId] ?? []).some((r) => r.toLowerCase() === key.toLowerCase()))
    toggle(cardId, key)
  newRole.value[cardId] = ''
}

const roleKeys = computed(() => availableRoles(Object.values(draft.value?.roles ?? {}).flat()))
const roleSummary = computed(() =>
  roleKeys.value
    .map((key) => ({
      key,
      label: roleLabel(key),
      copies: draft.value ? mainCopiesWithRole(draft.value, key) : 0,
    }))
    .filter((r) => r.copies > 0),
)

// Instant local checks while editing; the server's full check applies after saving.
const localIssues = computed<ValidationIssue[]>(() => {
  const d = draft.value
  if (!d) return []
  const issues: ValidationIssue[] = []
  const main = zoneCount(d, 'Main')
  if (main < LIMITS.mainMin)
    issues.push({
      severity: 'Error',
      code: 'local',
      message: `The Main Deck has ${main} cards; it needs at least ${LIMITS.mainMin}.`,
      cardId: null,
    })
  return issues
})
const issues = computed(() =>
  dirty.value ? localIssues.value : (editor.validation(props.versionId)?.issues ?? []),
)

async function save(): Promise<void> {
  try {
    const saved = await editor.save(props.versionId)
    const errors = saved.validation.issues.filter((i) => i.severity === 'Error').length
    toasts.success(
      errors
        ? `Saved. ${errors} deck ${errors === 1 ? 'problem' : 'problems'} to fix before simulating.`
        : 'Saved.',
    )
  } catch (e) {
    toasts.error(errorMessage(e))
  }
}

function discard(): void {
  if (window.confirm('Discard all unsaved changes to this version?'))
    editor.discard(props.versionId)
}

async function saveDetails(): Promise<void> {
  try {
    const saved = await api.versions.update(props.versionId, versionName.value, notes.value || null)
    editor.versions[props.versionId] = { ...saved }
    editingDetails.value = false
  } catch (e) {
    toasts.error(errorMessage(e))
  }
}

async function simulate(): Promise<void> {
  if (dirty.value) {
    if (!window.confirm('Save your changes first? The simulator uses the saved version.')) return
    await save()
    if (dirty.value) return
  }
  await router.push({ name: 'simulate', query: { versionId: props.versionId } })
}
</script>

<template>
  <AsyncState :loading="loading && !draft" :error="error" @retry="load">
    <div v-if="version && draft" class="space-y-4">
      <!-- Header -->
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div class="min-w-0">
          <RouterLink
            :to="{ name: 'deck', params: { deckId: version.deckId } }"
            class="text-sm text-accent"
            >← {{ version.deckName }}</RouterLink
          >
          <form v-if="editingDetails" class="mt-1 space-y-2" @submit.prevent="saveDetails">
            <label class="label" for="version-name">Version name</label>
            <input id="version-name" v-model="versionName" class="input" maxlength="100" />
            <label class="label" for="version-notes">Notes</label>
            <textarea id="version-notes" v-model="notes" class="input" rows="2" maxlength="2000" />
            <div class="flex gap-2">
              <button type="submit" class="btn btn-primary btn-sm">Save details</button>
              <button type="button" class="btn btn-sm" @click="editingDetails = false">
                Cancel
              </button>
            </div>
          </form>
          <template v-else>
            <h1
              class="mt-1 flex flex-wrap items-center gap-2 text-2xl font-semibold tracking-tight"
            >
              {{ version.versionName }}
              <button type="button" class="btn btn-sm" @click="editingDetails = true">
                Edit details
              </button>
            </h1>
            <p v-if="version.notes" class="text-sm whitespace-pre-line text-ink-2">
              {{ version.notes }}
            </p>
          </template>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <span v-if="dirty" class="text-sm text-warning" role="status">● Unsaved changes</span>
          <span v-else class="text-sm text-ink-muted" role="status">All changes saved</span>
          <button type="button" class="btn" :disabled="!dirty" @click="discard">Discard</button>
          <button
            type="button"
            class="btn btn-primary"
            :disabled="!dirty || editor.saving"
            data-testid="save-deck"
            @click="save"
          >
            {{ editor.saving ? 'Saving…' : 'Save' }}
          </button>
          <button type="button" class="btn" data-testid="simulate-deck" @click="simulate">
            Simulate →
          </button>
        </div>
      </div>

      <div class="grid gap-4 lg:grid-cols-[minmax(0,22rem)_minmax(0,1fr)_16rem]">
        <!-- Search -->
        <section
          class="card p-3 lg:sticky lg:top-20 lg:max-h-[calc(100vh-6rem)] lg:overflow-y-auto"
          aria-label="Card search"
        >
          <h2 class="mb-2 text-sm font-semibold">Add cards</h2>
          <CardSearch
            compact
            :page-size="20"
            add-label="Add"
            @select="detailCard = $event"
            @add="add($event)"
          />
        </section>

        <!-- Zones -->
        <section class="space-y-4" aria-label="Deck contents">
          <div
            v-for="zone in DECK_ZONES"
            :key="zone"
            class="card p-3"
            :data-testid="`zone-${zone}`"
          >
            <h2 class="mb-2 flex items-baseline justify-between text-sm font-semibold">
              <span>{{ zone }} Deck</span>
              <span class="num font-normal text-ink-muted">{{ zoneCount(draft, zone) }} cards</span>
            </h2>
            <p v-if="entriesIn(zone).length === 0" class="text-sm text-ink-muted">
              {{ zone === 'Main' ? 'Search for cards on the left and add them here.' : 'Empty.' }}
            </p>
            <ul class="divide-y divide-line">
              <li
                v-for="entry in entriesIn(zone)"
                :key="entry.cardId"
                class="flex flex-wrap items-center gap-2 py-1.5"
                :data-testid="`entry-${zone}-${entry.card.name}`"
              >
                <button
                  type="button"
                  class="shrink-0"
                  :aria-label="`Show ${entry.card.name}`"
                  @click="detailCard = entry.card"
                >
                  <CardImage
                    :src="entry.card.smallImageUrl"
                    :name="entry.card.name"
                    size="sm"
                    class="w-9!"
                  />
                </button>
                <div class="min-w-0 flex-1">
                  <p class="flex items-center gap-1.5 truncate text-sm font-medium">
                    {{ entry.card.name }} <BanBadge :status="entry.card.tcgBanStatus" />
                  </p>
                  <div class="mt-0.5 flex flex-wrap items-center gap-1">
                    <span
                      v-for="role in draft.roles[entry.cardId] ?? []"
                      :key="role"
                      class="rounded bg-accent-soft px-1.5 py-0.5 text-[11px]"
                    >
                      {{ roleLabel(role) }}
                    </span>
                    <details class="relative">
                      <summary
                        class="cursor-pointer list-none rounded px-1.5 py-0.5 text-[11px] text-accent hover:bg-surface-2"
                      >
                        {{ (draft.roles[entry.cardId] ?? []).length ? 'Edit roles' : '+ Roles' }}
                      </summary>
                      <div
                        class="absolute z-20 mt-1 w-56 rounded-lg border border-line bg-surface p-2 shadow-lg"
                      >
                        <fieldset>
                          <legend class="label">Roles for {{ entry.card.name }}</legend>
                          <label
                            v-for="role in roleKeys"
                            :key="role"
                            class="flex items-center gap-2 py-0.5 text-sm"
                          >
                            <input
                              type="checkbox"
                              :checked="(draft.roles[entry.cardId] ?? []).includes(role)"
                              @change="toggle(entry.cardId, role)"
                            />
                            {{ roleLabel(role) }}
                            <span v-if="BUILT_IN_ROLES.find((r) => r.key === role)" class="sr-only"
                              >: {{ BUILT_IN_ROLES.find((r) => r.key === role)!.hint }}</span
                            >
                          </label>
                        </fieldset>
                        <form class="mt-1 flex gap-1" @submit.prevent="addCustomRole(entry.cardId)">
                          <label class="sr-only" :for="`custom-${entry.cardId}`"
                            >New custom role</label
                          >
                          <input
                            :id="`custom-${entry.cardId}`"
                            v-model="newRole[entry.cardId]"
                            class="input py-1! text-xs!"
                            placeholder="Custom role…"
                            maxlength="100"
                          />
                          <button type="submit" class="btn btn-sm">Add</button>
                        </form>
                      </div>
                    </details>
                  </div>
                </div>
                <div class="flex items-center gap-1">
                  <button
                    type="button"
                    class="btn btn-sm w-7"
                    :aria-label="`Remove one ${entry.card.name}`"
                    @click="remove(entry.cardId, zone)"
                  >
                    −
                  </button>
                  <span
                    class="num w-5 text-center text-sm font-semibold"
                    :aria-label="`${entry.quantity} copies`"
                    >{{ entry.quantity }}</span
                  >
                  <button
                    type="button"
                    class="btn btn-sm w-7"
                    :aria-label="`Add one ${entry.card.name}`"
                    :disabled="copiesOf(draft, entry.cardId) >= LIMITS.copies"
                    @click="add(entry.card, zone)"
                  >
                    +
                  </button>
                  <button
                    v-if="zone !== 'Side'"
                    type="button"
                    class="btn btn-sm"
                    :title="`Move one copy to the Side Deck`"
                    @click="move(entry.cardId, zone, 'Side')"
                  >
                    → Side
                  </button>
                  <button
                    v-else
                    type="button"
                    class="btn btn-sm"
                    :title="`Move one copy back to the ${entry.card.isExtraDeckCard ? 'Extra' : 'Main'} Deck`"
                    @click="
                      move(entry.cardId, 'Side', entry.card.isExtraDeckCard ? 'Extra' : 'Main')
                    "
                  >
                    ← {{ entry.card.isExtraDeckCard ? 'Extra' : 'Main' }}
                  </button>
                </div>
              </li>
            </ul>
          </div>
        </section>

        <!-- Summary -->
        <aside class="space-y-4">
          <div class="card p-3">
            <ValidationPanel
              :main="zoneCount(draft, 'Main')"
              :extra="zoneCount(draft, 'Extra')"
              :side="zoneCount(draft, 'Side')"
              :issues="issues"
              :stale="dirty"
            />
          </div>
          <div class="card p-3">
            <h2 class="mb-2 text-sm font-semibold">Roles in the Main Deck</h2>
            <p v-if="roleSummary.length === 0" class="text-xs text-ink-muted">
              Label cards as starters, extenders, bricks and so on. Simulations count these labels;
              they are your own classification, not game rules.
            </p>
            <ul class="num space-y-1 text-sm">
              <li v-for="r in roleSummary" :key="r.key" class="flex justify-between">
                <span>{{ r.label }}</span>
                <span class="text-ink-2"
                  >{{ r.copies }} {{ r.copies === 1 ? 'copy' : 'copies' }}</span
                >
              </li>
            </ul>
          </div>
        </aside>
      </div>

      <ModalDialog
        :open="detailCard !== null"
        :title="detailCard?.name ?? ''"
        @close="detailCard = null"
      >
        <template v-if="detailCard">
          <CardDetail :card="detailCard" />
          <div class="mt-4 flex flex-wrap gap-2 border-t border-line pt-3">
            <span class="num self-center text-sm text-ink-2"
              >In this deck: {{ copiesOf(draft, detailCard.id) }} / {{ LIMITS.copies }}</span
            >
            <button
              v-if="!detailCard.isExtraDeckCard"
              type="button"
              class="btn btn-primary btn-sm ml-auto"
              @click="add(detailCard, 'Main')"
            >
              + Main Deck
            </button>
            <button
              v-else
              type="button"
              class="btn btn-primary btn-sm ml-auto"
              @click="add(detailCard, 'Extra')"
            >
              + Extra Deck
            </button>
            <button type="button" class="btn btn-sm" @click="add(detailCard, 'Side')">
              + Side Deck
            </button>
          </div>
        </template>
      </ModalDialog>
    </div>
  </AsyncState>
</template>
