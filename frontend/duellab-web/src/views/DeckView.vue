<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { api, errorMessage } from '@/api/client'
import type { BanlistSummary, DeckSummary, SimulationRunSummary, VersionSummary } from '@/api/types'
import { dateTime, percent, relativeTime } from '@/lib/format'
import AsyncState from '@/components/AsyncState.vue'
import { useDeckEditorStore } from '@/stores/deckEditor'
import { useToastStore } from '@/stores/toasts'

const props = defineProps<{ deckId: string }>()
const router = useRouter()
const toasts = useToastStore()
const editor = useDeckEditorStore()

const deck = ref<DeckSummary | null>(null)
const history = ref<Record<string, SimulationRunSummary[]>>({})
const loading = ref(true)
const error = ref<string | null>(null)
const renaming = ref(false)
const lists = ref<BanlistSummary[]>([])
const olderLists = computed(() => lists.value.filter((l) => !l.isCurrent))
const formatChoice = computed(() =>
  deck.value?.format === 'Casual' ? 'casual' : (deck.value?.banlistId ?? 'current'),
)

async function setFormat(choice: string): Promise<void> {
  if (!deck.value) return
  try {
    deck.value =
      choice === 'casual'
        ? await api.decks.setFormat(props.deckId, 'Casual', null)
        : await api.decks.setFormat(
            props.deckId,
            'TCG Advanced',
            choice === 'current' ? null : choice,
          )
    // Refresh loaded versions so the editor applies the new rules at once.
    for (const v of deck.value.versions) {
      if (editor.versions[v.id]) await editor.load(v.id)
    }
    toasts.success(choice === 'casual' ? 'Format: Casual.' : 'Format: TCG Advanced.')
  } catch (e) {
    toasts.error(errorMessage(e))
  }
}
const name = ref('')

const versions = computed(() => [...(deck.value?.versions ?? [])].reverse())
const versionName = (id: string | null) =>
  deck.value?.versions.find((v) => v.id === id)?.versionName

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    ;[deck.value, lists.value] = await Promise.all([
      api.decks.get(props.deckId),
      api.banlists.list(),
    ])
    name.value = deck.value.name
    const entries = await Promise.all(
      deck.value.versions.map(async (v) => [v.id, await api.versions.history(v.id, 5)] as const),
    )
    history.value = Object.fromEntries(entries)
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

async function rename(): Promise<void> {
  try {
    deck.value = await api.decks.rename(props.deckId, name.value)
    renaming.value = false
  } catch (e) {
    toasts.error(errorMessage(e))
  }
}

async function duplicate(version: VersionSummary): Promise<void> {
  const suggested = `${version.versionName} (copy)`
  const newName = window.prompt('Name for the new version:', suggested)
  if (!newName) return
  if (editor.isDirty(version.id)) {
    toasts.show(
      'The copy is made from the last saved state; your unsaved edits stay on the original.',
      'info',
    )
  }
  try {
    const created = await api.decks.createVersion(props.deckId, newName, version.id)
    toasts.success(`Created "${created.versionName}".`)
    await router.push({ name: 'editor', params: { versionId: created.id } })
  } catch (e) {
    toasts.error(errorMessage(e))
  }
}

async function addEmpty(): Promise<void> {
  const newName = window.prompt(
    'Name for the new, empty version:',
    `v${(deck.value?.versions.length ?? 0) + 1}`,
  )
  if (!newName) return
  try {
    const created = await api.decks.createVersion(props.deckId, newName, null)
    await router.push({ name: 'editor', params: { versionId: created.id } })
  } catch (e) {
    toasts.error(errorMessage(e))
  }
}

async function remove(version: VersionSummary): Promise<void> {
  if (!window.confirm(`Delete version "${version.versionName}" and its simulation history?`)) return
  try {
    await api.versions.remove(version.id)
    editor.discard(version.id)
    await load()
  } catch (e) {
    toasts.error(errorMessage(e))
  }
}

onMounted(load)
watch(() => props.deckId, load)
</script>

<template>
  <AsyncState :loading="loading" :error="error" @retry="load">
    <div v-if="deck" class="space-y-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div class="min-w-0">
          <RouterLink to="/decks" class="text-sm text-accent">← My decks</RouterLink>
          <form v-if="renaming" class="mt-1 flex gap-2" @submit.prevent="rename">
            <label class="sr-only" for="deck-name">Deck name</label>
            <input
              id="deck-name"
              v-model="name"
              class="input text-lg font-semibold"
              maxlength="100"
            />
            <button class="btn btn-primary" type="submit">Save</button>
            <button class="btn" type="button" @click="renaming = false">Cancel</button>
          </form>
          <h1 v-else class="mt-1 flex items-center gap-2 text-2xl font-semibold tracking-tight">
            {{ deck.name }}
            <button type="button" class="btn btn-sm" @click="renaming = true">Rename</button>
          </h1>
          <p class="text-xs text-ink-muted">created {{ dateTime(deck.createdAt) }}</p>
          <div class="mt-2 flex flex-wrap items-center gap-2 text-sm">
            <label for="deck-format" class="text-ink-2">Format</label>
            <select
              id="deck-format"
              class="input w-auto!"
              :value="formatChoice"
              data-testid="deck-format-select"
              @change="setFormat(($event.target as HTMLSelectElement).value)"
            >
              <option value="current">TCG Advanced: current Forbidden &amp; Limited list</option>
              <option v-for="list in olderLists" :key="list.id" :value="list.id">
                TCG Advanced: list first seen {{ new Date(list.firstSeenAt).toLocaleDateString() }}
              </option>
              <option value="casual">Casual: no Forbidden &amp; Limited list</option>
            </select>
            <RouterLink to="/banlist" class="text-xs text-accent underline"
              >View the list</RouterLink
            >
          </div>
          <p v-if="deck.format === 'Casual'" class="mt-1 text-xs text-ink-2">
            Forbidden and Limited cards are allowed. The 3-copy rule and deck sizes still apply.
          </p>
        </div>
        <div class="flex gap-2">
          <button type="button" class="btn" @click="addEmpty">New empty version</button>
          <RouterLink
            v-if="deck.versions.length >= 2"
            class="btn"
            :to="{
              name: 'compare',
              query: {
                base: deck.versions[deck.versions.length - 2]!.id,
                candidate: deck.versions[deck.versions.length - 1]!.id,
              },
            }"
          >
            Compare latest two
          </RouterLink>
        </div>
      </div>

      <ul class="space-y-3">
        <li
          v-for="version in versions"
          :key="version.id"
          class="card p-4"
          :data-testid="`version-${version.versionName}`"
        >
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div class="min-w-0">
              <h2 class="font-semibold">
                {{ version.versionName }}
                <span
                  v-if="editor.isDirty(version.id)"
                  class="ml-1 rounded bg-warning-soft px-1.5 py-0.5 text-[11px] font-medium text-warning"
                  >unsaved edits</span
                >
              </h2>
              <p class="num text-xs text-ink-muted">
                Main {{ version.mainCount }} · Extra {{ version.extraCount }} · Side
                {{ version.sideCount }} · updated {{ relativeTime(version.updatedAt) }}
                <template v-if="version.parentVersionId">
                  · copied from
                  {{ versionName(version.parentVersionId) ?? 'a deleted version' }}</template
                >
              </p>
              <p v-if="version.notes" class="mt-1 text-sm whitespace-pre-line text-ink-2">
                {{ version.notes }}
              </p>
            </div>
            <div class="flex flex-wrap gap-2">
              <RouterLink
                :to="{ name: 'editor', params: { versionId: version.id } }"
                class="btn btn-sm btn-primary"
                >Edit</RouterLink
              >
              <RouterLink
                :to="{ name: 'simulate', query: { versionId: version.id } }"
                class="btn btn-sm"
                >Simulate</RouterLink
              >
              <button type="button" class="btn btn-sm" @click="duplicate(version)">
                Duplicate
              </button>
              <button
                type="button"
                class="btn btn-sm btn-danger"
                :disabled="deck.versions.length === 1"
                @click="remove(version)"
              >
                Delete
              </button>
            </div>
          </div>

          <div v-if="history[version.id]?.length" class="mt-3 border-t border-line pt-3">
            <h3 class="mb-1 text-xs font-medium text-ink-2">Simulation history</h3>
            <ul class="space-y-1 text-sm">
              <li v-for="run in history[version.id]" :key="run.id">
                <RouterLink
                  :to="{ name: 'run', params: { runId: run.id } }"
                  class="flex flex-wrap justify-between gap-x-3 rounded px-1 hover:bg-surface-2"
                >
                  <span class="num text-ink-2"
                    >{{ dateTime(run.createdAt) }} · {{ run.sampleCount.toLocaleString() }} hands ·
                    seed {{ run.seed }}</span
                  >
                  <span class="num">
                    <template v-for="(s, i) in run.scenarios.slice(0, 3)" :key="s.name">
                      <span v-if="i" class="text-ink-muted"> · </span>{{ s.name }}
                      {{ percent(s.exact ?? s.estimate) }}
                    </template>
                  </span>
                </RouterLink>
              </li>
            </ul>
          </div>
        </li>
      </ul>
    </div>
  </AsyncState>
</template>
