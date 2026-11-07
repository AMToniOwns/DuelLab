<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { api, errorMessage } from '@/api/client'
import type { Banlist, BanlistSummary, BanStatus } from '@/api/types'
import AsyncState from '@/components/AsyncState.vue'
import BanBadge from '@/components/BanBadge.vue'

const lists = ref<BanlistSummary[]>([])
const selected = ref<string | null>(null)
const list = ref<Banlist | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    lists.value = await api.banlists.list()
    selected.value ??= lists.value[0]?.id ?? null
    list.value = selected.value ? await api.banlists.get(selected.value) : null
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}
onMounted(load)
watch(selected, async (id, old) => {
  if (id && old && id !== old) list.value = await api.banlists.get(id)
})

const groups: { status: BanStatus; title: string; hint: string }[] = [
  { status: 'Forbidden', title: 'Forbidden', hint: '0 copies' },
  { status: 'Limited', title: 'Limited', hint: '1 copy' },
  { status: 'SemiLimited', title: 'Semi-Limited', hint: '2 copies' },
]
const entriesBy = computed(
  () => (status: BanStatus) => list.value?.entries.filter((e) => e.status === status) ?? [],
)
const dateOf = (iso: string | null) =>
  iso ? new Date(iso).toLocaleDateString(undefined, { dateStyle: 'long' }) : '—'
</script>

<template>
  <div class="space-y-4">
    <div>
      <h1 class="text-2xl font-semibold tracking-tight">Forbidden &amp; Limited list</h1>
      <p class="text-sm text-ink-2">
        TCG Advanced decks are checked against this list: over-limit decks cannot be simulated until
        fixed (or switched to Casual). The list comes from YGOPRODeck with each card import.
        YGOPRODeck does not publish effective dates, so lists are dated by when DuelLab first saw
        them. Check the official list before a tournament.
      </p>
    </div>

    <AsyncState :loading="loading" :error="error" @retry="load">
      <p v-if="lists.length === 0" class="card p-4 text-sm text-ink-2">
        No list recorded yet; it is saved with the first card import.
      </p>
      <template v-else>
        <div class="card flex flex-wrap items-end gap-4 p-4">
          <div>
            <label class="label" for="banlist-pick">List</label>
            <select id="banlist-pick" v-model="selected" class="input w-auto!">
              <option v-for="l in lists" :key="l.id" :value="l.id">
                {{ l.isCurrent ? 'Current: ' : '' }}first seen {{ dateOf(l.firstSeenAt) }}
              </option>
            </select>
          </div>
          <dl v-if="list" class="num grid grid-cols-2 gap-x-4 gap-y-0.5 text-sm sm:grid-cols-4">
            <dt class="text-ink-2">Forbidden</dt>
            <dd class="font-semibold">{{ list.summary.forbidden }}</dd>
            <dt class="text-ink-2">Limited</dt>
            <dd class="font-semibold">{{ list.summary.limited }}</dd>
            <dt class="text-ink-2">Semi-Limited</dt>
            <dd class="font-semibold">{{ list.summary.semiLimited }}</dd>
            <dt class="text-ink-2">Provider data</dt>
            <dd>
              {{
                list.summary.providerVersion
                  ? `v${list.summary.providerVersion}, ${dateOf(list.summary.providerUpdatedAt)}`
                  : '—'
              }}
            </dd>
          </dl>
        </div>

        <div v-if="list" class="grid gap-4 md:grid-cols-3">
          <section
            v-for="g in groups"
            :key="g.status"
            class="card p-4"
            :aria-labelledby="`list-${g.status}`"
          >
            <h2
              :id="`list-${g.status}`"
              class="mb-2 flex items-baseline justify-between font-semibold"
            >
              <span
                >{{ g.title }}
                <span class="text-xs font-normal text-ink-muted">({{ g.hint }})</span></span
              >
              <span class="num text-sm font-normal text-ink-2">{{
                entriesBy(g.status).length
              }}</span>
            </h2>
            <p v-if="entriesBy(g.status).length === 0" class="text-sm text-ink-muted">None.</p>
            <ul class="space-y-0.5 text-sm">
              <li
                v-for="e in entriesBy(g.status)"
                :key="e.cardId"
                class="flex items-center justify-between gap-2"
              >
                <span class="truncate">{{ e.name }}</span>
                <BanBadge :status="e.status" />
              </li>
            </ul>
          </section>
        </div>
      </template>
    </AsyncState>
  </div>
</template>
