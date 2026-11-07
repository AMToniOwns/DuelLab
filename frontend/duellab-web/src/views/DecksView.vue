<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, errorMessage } from '@/api/client'
import type { DeckSummary } from '@/api/types'
import { relativeTime } from '@/lib/format'
import AsyncState from '@/components/AsyncState.vue'
import { useToastStore } from '@/stores/toasts'

const router = useRouter()
const toasts = useToastStore()
const decks = ref<DeckSummary[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const newName = ref('')
const creating = ref(false)

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

    <form class="card flex flex-wrap items-end gap-2 p-4" @submit.prevent="create">
      <div class="min-w-48 flex-1">
        <label class="label" for="new-deck">New deck</label>
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
