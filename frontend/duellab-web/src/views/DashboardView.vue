<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, errorMessage } from '@/api/client'
import type { CatalogStatus, DeckSummary, SimulationRunSummary } from '@/api/types'
import { percent, relativeTime } from '@/lib/format'
import AsyncState from '@/components/AsyncState.vue'

const status = ref<CatalogStatus | null>(null)
const decks = ref<DeckSummary[]>([])
const runs = ref<SimulationRunSummary[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    ;[status.value, decks.value, runs.value] = await Promise.all([
      api.cards.status(),
      api.decks.list(),
      api.simulations.recent(6),
    ])
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}
onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 class="text-2xl font-semibold tracking-tight">Dashboard</h1>
        <p class="text-sm text-ink-2">
          Build a deck, simulate thousands of opening hands, and compare versions.
        </p>
      </div>
      <div class="flex gap-2">
        <RouterLink to="/decks" class="btn btn-primary">New deck</RouterLink>
        <RouterLink to="/simulate" class="btn">Run a simulation</RouterLink>
      </div>
    </div>

    <AsyncState :loading="loading" :error="error" @retry="load">
      <div
        v-if="status && status.cardCount === 0"
        class="card bg-warning-soft! p-4 text-sm"
        role="status"
      >
        The card database is still being imported from {{ status.provider }}. This takes a few
        seconds on first start.
        <button type="button" class="ml-1 font-medium text-accent underline" @click="load">
          Refresh
        </button>
      </div>

      <div class="grid gap-3 sm:grid-cols-3">
        <div class="card p-4">
          <p class="text-xs text-ink-2">Decks</p>
          <p class="text-3xl font-semibold">{{ decks.length }}</p>
        </div>
        <div class="card p-4">
          <p class="text-xs text-ink-2">Cards in database</p>
          <p class="text-3xl font-semibold">{{ status?.cardCount.toLocaleString() ?? '—' }}</p>
          <p v-if="status?.lastSyncedAt" class="text-xs text-ink-muted">
            Synced {{ relativeTime(status.lastSyncedAt) }} from {{ status.provider }}
          </p>
        </div>
        <div class="card p-4">
          <p class="text-xs text-ink-2">Recent simulations</p>
          <p class="text-3xl font-semibold">{{ runs.length }}</p>
        </div>
      </div>

      <div class="grid gap-6 lg:grid-cols-2">
        <section class="card p-4" aria-labelledby="recent-decks">
          <div class="mb-3 flex items-center justify-between">
            <h2 id="recent-decks" class="font-semibold">Recent decks</h2>
            <RouterLink to="/decks" class="text-sm text-accent">All decks →</RouterLink>
          </div>
          <p v-if="decks.length === 0" class="text-sm text-ink-2">
            No decks yet.
            <RouterLink to="/decks" class="text-accent underline">Create your first deck</RouterLink
            >.
          </p>
          <ul class="divide-y divide-line">
            <li
              v-for="deck in decks.slice(0, 5)"
              :key="deck.id"
              class="flex items-center justify-between gap-2 py-2"
            >
              <RouterLink :to="{ name: 'deck', params: { deckId: deck.id } }" class="min-w-0">
                <span class="block truncate font-medium">{{ deck.name }}</span>
                <span class="text-xs text-ink-muted">
                  {{ deck.versions.length }}
                  {{ deck.versions.length === 1 ? 'version' : 'versions' }} · updated
                  {{ relativeTime(deck.updatedAt) }}
                </span>
              </RouterLink>
              <RouterLink
                v-if="deck.versions.length"
                :to="{
                  name: 'editor',
                  params: { versionId: deck.versions[deck.versions.length - 1]!.id },
                }"
                class="btn btn-sm"
              >
                Edit
              </RouterLink>
            </li>
          </ul>
        </section>

        <section class="card p-4" aria-labelledby="recent-runs">
          <h2 id="recent-runs" class="mb-3 font-semibold">Recent simulations</h2>
          <p v-if="runs.length === 0" class="text-sm text-ink-2">No simulations yet.</p>
          <ul class="divide-y divide-line">
            <li v-for="run in runs" :key="run.id" class="py-2">
              <RouterLink
                :to="{ name: 'run', params: { runId: run.id } }"
                class="flex items-center justify-between gap-3"
              >
                <span class="min-w-0">
                  <span class="block truncate font-medium"
                    >{{ run.deckName }} · {{ run.versionName }}</span
                  >
                  <span class="num text-xs text-ink-muted"
                    >{{ run.sampleCount.toLocaleString() }} hands · seed {{ run.seed }} ·
                    {{ relativeTime(run.createdAt) }}</span
                  >
                </span>
                <span v-if="run.scenarios[0]" class="shrink-0 text-right">
                  <span class="num block font-semibold">{{
                    percent(run.scenarios[0].exact ?? run.scenarios[0].estimate)
                  }}</span>
                  <span class="block text-xs text-ink-muted">{{ run.scenarios[0].name }}</span>
                </span>
              </RouterLink>
            </li>
          </ul>
        </section>
      </div>
    </AsyncState>
  </div>
</template>
