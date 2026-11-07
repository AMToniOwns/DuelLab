<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, errorMessage } from '@/api/client'
import type { DeckFormat, DeckSummary } from '@/api/types'
import { nameFromFileName, readYdkFile } from '@/lib/ydkFile'
import { useImportReportStore } from '@/stores/importReports'
import { relativeTime } from '@/lib/format'
import AsyncState from '@/components/AsyncState.vue'
import { useToastStore } from '@/stores/toasts'

const router = useRouter()
const toasts = useToastStore()
const reports = useImportReportStore()
const decks = ref<DeckSummary[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const newName = ref('')
const creating = ref(false)

const importFileName = ref('')
const importContent = ref<string | null>(null)
const importName = ref('')
const importFormat = ref<DeckFormat>('TCG Advanced')
const importing = ref(false)

async function chooseFile(event: Event): Promise<void> {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  try {
    importContent.value = await readYdkFile(file)
    importFileName.value = file.name
    importName.value = nameFromFileName(file.name)
  } catch (e) {
    importContent.value = null
    toasts.error(errorMessage(e))
  }
}

async function importFile(): Promise<void> {
  if (importContent.value === null) return
  importing.value = true
  try {
    const result = await api.decks.importYdk(
      importName.value,
      importContent.value,
      importFormat.value,
    )
    reports.set(result.version.id, importFileName.value, result.report)
    await router.push({ name: 'editor', params: { versionId: result.version.id } })
  } catch (e) {
    toasts.error(errorMessage(e))
  } finally {
    importing.value = false
  }
}

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    decks.value = await api.decks.list()
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

async function create(): Promise<void> {
  if (!newName.value.trim()) return
  creating.value = true
  try {
    const deck = await api.decks.create(newName.value)
    newName.value = ''
    await router.push({ name: 'editor', params: { versionId: deck.versions[0]!.id } })
  } catch (e) {
    toasts.error(errorMessage(e))
  } finally {
    creating.value = false
  }
}

async function remove(deck: DeckSummary): Promise<void> {
  if (
    !window.confirm(
      `Delete "${deck.name}" with all its versions and simulation history? This cannot be undone.`,
    )
  )
    return
  try {
    await api.decks.remove(deck.id)
    decks.value = decks.value.filter((d) => d.id !== deck.id)
    toasts.success(`Deleted "${deck.name}".`)
  } catch (e) {
    toasts.error(errorMessage(e))
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <h1 class="text-2xl font-semibold tracking-tight">My decks</h1>

    <div class="grid items-start gap-4 lg:grid-cols-2">
      <form class="card flex flex-wrap items-end gap-2 p-4" @submit.prevent="create">
        <div class="min-w-48 flex-1">
          <label class="label" for="new-deck">Create an empty deck</label>
          <input
            id="new-deck"
            v-model="newName"
            class="input"
            maxlength="100"
            placeholder="e.g. Snake-Eye Fire King"
          />
        </div>
        <button type="submit" class="btn btn-primary" :disabled="creating || !newName.trim()">
          Create deck
        </button>
      </form>

      <form
        class="card space-y-2 p-4"
        aria-labelledby="import-heading"
        @submit.prevent="importFile"
      >
        <h2 id="import-heading" class="label">Import a .ydk file</h2>
        <div class="flex flex-wrap items-center gap-2">
          <label class="btn cursor-pointer" for="ydk-file">Choose file…</label>
          <input
            id="ydk-file"
            type="file"
            accept=".ydk,text/plain"
            class="sr-only"
            data-testid="ydk-file"
            @change="chooseFile"
          />
          <span class="truncate text-sm text-ink-2">{{ importFileName || 'No file chosen' }}</span>
        </div>
        <div v-if="importContent !== null" class="flex flex-wrap items-end gap-2">
          <div class="min-w-40 flex-1">
            <label class="label" for="import-name">Deck name</label>
            <input id="import-name" v-model="importName" class="input" maxlength="100" />
          </div>
          <div>
            <label class="label" for="import-format">Format</label>
            <select id="import-format" v-model="importFormat" class="input w-auto!">
              <option value="TCG Advanced">TCG Advanced</option>
              <option value="Casual">Casual (no Forbidden &amp; Limited list)</option>
            </select>
          </div>
          <button
            type="submit"
            class="btn btn-primary"
            :disabled="importing || !importName.trim()"
            data-testid="import-ydk"
          >
            {{ importing ? 'Importing…' : 'Import deck' }}
          </button>
        </div>
        <p class="text-xs text-ink-muted">
          Deck files from YGOPRODeck, EDOPro, YGO Omega and similar tools.
        </p>
      </form>
    </div>

    <AsyncState :loading="loading" :error="error" @retry="load">
      <p v-if="decks.length === 0" class="text-sm text-ink-2">You have no decks yet.</p>
      <ul class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <li
          v-for="deck in decks"
          :key="deck.id"
          class="card flex flex-col gap-3 p-4"
          :data-testid="`deck-${deck.name}`"
        >
          <RouterLink :to="{ name: 'deck', params: { deckId: deck.id } }" class="min-w-0">
            <h2 class="truncate font-semibold">{{ deck.name }}</h2>
            <p class="text-xs text-ink-muted">
              {{ deck.format }} · updated {{ relativeTime(deck.updatedAt) }}
            </p>
          </RouterLink>
          <ul class="num space-y-0.5 text-xs text-ink-2">
            <li v-for="v in deck.versions" :key="v.id" class="flex justify-between gap-2">
              <span class="truncate">{{ v.versionName }}</span>
              <span>{{ v.mainCount }} / {{ v.extraCount }} / {{ v.sideCount }}</span>
            </li>
          </ul>
          <div class="mt-auto flex gap-2">
            <RouterLink :to="{ name: 'deck', params: { deckId: deck.id } }" class="btn btn-sm"
              >Open</RouterLink
            >
            <button type="button" class="btn btn-sm btn-danger ml-auto" @click="remove(deck)">
              Delete
            </button>
          </div>
        </li>
      </ul>
    </AsyncState>
  </div>
</template>
