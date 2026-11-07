<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { api, errorMessage } from '@/api/client'
import type { BanStatus, Card, CardKind, PagedResult } from '@/api/types'
import CardImage from './CardImage.vue'
import BanBadge from './BanBadge.vue'

/**
 * Debounced search over the local card catalogue. Emits `select` when a card is chosen and `add`
 * when its quick-add button is used.
 */
const props = withDefaults(
  defineProps<{
    pageSize?: number
    addLabel?: string
    compact?: boolean
    /** The list status to show for a card (e.g. a deck's pinned list); defaults to the current list. */
    statusOf?: (card: Card) => BanStatus
  }>(),
  {
    pageSize: 24,
    addLabel: '',
    compact: false,
    statusOf: (card: Card) => card.tcgBanStatus,
  },
)
const emit = defineEmits<{ select: [card: Card]; add: [card: Card] }>()

const query = ref('')
const kind = ref<CardKind | ''>('')
const page = ref(1)
const result = ref<PagedResult<Card> | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
let timer: ReturnType<typeof setTimeout> | undefined
let requestId = 0

async function run(): Promise<void> {
  const id = ++requestId
  loading.value = true
  error.value = null
  try {
    const data = await api.cards.search({
      search: query.value.trim(),
      kind: kind.value || null,
      page: page.value,
      pageSize: props.pageSize,
    })
    if (id === requestId) result.value = data
  } catch (e) {
    if (id === requestId) error.value = errorMessage(e)
  } finally {
    if (id === requestId) loading.value = false
  }
}

watch([query, kind], () => {
  page.value = 1
  clearTimeout(timer)
  timer = setTimeout(run, 250)
})
watch(page, run)
onBeforeUnmount(() => clearTimeout(timer))
void run()

const totalPages = () =>
  result.value ? Math.max(1, Math.ceil(result.value.total / result.value.pageSize)) : 1
</script>

<template>
  <div class="flex flex-col gap-3">
    <div class="flex gap-2">
      <label class="sr-only" for="card-search">Search cards</label>
      <input
        id="card-search"
        v-model="query"
        type="search"
        class="input"
        placeholder="Search card names…"
        autocomplete="off"
      />
      <label class="sr-only" for="card-kind">Card type</label>
      <select id="card-kind" v-model="kind" class="input w-auto!">
        <option value="">All types</option>
        <option value="Monster">Monsters</option>
        <option value="Spell">Spells</option>
        <option value="Trap">Traps</option>
      </select>
    </div>

    <p v-if="error" class="text-sm text-critical" role="alert">{{ error }}</p>
    <p v-else-if="result" class="text-xs text-ink-muted" aria-live="polite">
      {{ result.total.toLocaleString() }} {{ result.total === 1 ? 'card' : 'cards'
      }}<span v-if="loading"> · searching…</span>
    </p>

    <ul
      class="grid gap-1.5"
      :class="compact ? 'grid-cols-1' : 'grid-cols-1 sm:grid-cols-2'"
      :aria-busy="loading"
    >
      <li v-for="card in result?.items ?? []" :key="card.id">
        <div
          class="flex items-center gap-2 rounded-lg border border-transparent p-1.5 hover:border-line hover:bg-surface-2"
        >
          <button
            type="button"
            class="flex min-w-0 flex-1 items-center gap-2 text-left"
            :data-testid="`card-result-${card.id}`"
            @click="emit('select', card)"
          >
            <CardImage :src="card.smallImageUrl" :name="card.name" size="sm" />
            <span class="min-w-0">
              <span class="block truncate text-sm font-medium">{{ card.name }}</span>
              <span class="flex items-center gap-1.5 truncate text-xs text-ink-muted">
                {{ card.typeLine }} <BanBadge :status="props.statusOf(card)" />
              </span>
            </span>
          </button>
          <button
            v-if="addLabel"
            type="button"
            class="btn btn-sm shrink-0"
            :aria-label="`${addLabel} ${card.name}`"
            :data-testid="`add-card-${card.id}`"
            @click="emit('add', card)"
          >
            + {{ addLabel }}
          </button>
        </div>
      </li>
    </ul>

    <div v-if="result && totalPages() > 1" class="flex items-center justify-between text-sm">
      <button type="button" class="btn btn-sm" :disabled="page <= 1" @click="page--">
        ← Previous
      </button>
      <span class="num text-ink-muted">Page {{ page }} of {{ totalPages() }}</span>
      <button type="button" class="btn btn-sm" :disabled="page >= totalPages()" @click="page++">
        Next →
      </button>
    </div>
  </div>
</template>
