<script setup lang="ts">
import { computed } from 'vue'
import { percent } from '@/lib/format'
import ChartAxis from './ChartAxis.vue'

/** Ranked bars: the exact chance of opening at least one copy of each card. One hue. */
export interface FrequencyRow {
  name: string
  quantity: number
  exact: number
  estimate: number
  handsWithCard: number
  samples: number
}

const props = withDefaults(defineProps<{ rows: FrequencyRow[]; limit?: number }>(), { limit: 15 })

const ranked = computed(() =>
  [...props.rows]
    .sort((a, b) => b.exact - a.exact || a.name.localeCompare(b.name))
    .slice(0, props.limit),
)
</script>

<template>
  <figure class="space-y-1">
    <div
      v-for="row in ranked"
      :key="row.name"
      class="group relative grid grid-cols-[minmax(6rem,14rem)_1fr_3.5rem] items-center gap-3 rounded-md px-1 py-0.5 outline-none hover:bg-surface-2 focus-visible:bg-surface-2"
      tabindex="0"
      :aria-label="`${row.name}, ${row.quantity} copies: ${percent(row.exact)} to open at least one`"
    >
      <span class="truncate text-sm">
        {{ row.name }} <span class="num text-ink-muted">×{{ row.quantity }}</span>
      </span>
      <div class="relative h-5">
        <ChartAxis />
        <div
          class="absolute top-1/2 left-0 h-2.5 -translate-y-1/2 rounded-r-[4px] group-hover:brightness-110"
          :style="{ width: `${row.exact * 100}%`, background: 'var(--series-1)' }"
        />
      </div>
      <span class="num text-right text-sm">{{ percent(row.exact) }}</span>
      <div
        class="pointer-events-none absolute bottom-full left-1/3 z-10 mb-1 hidden w-56 rounded-lg border border-line bg-surface p-2.5 text-xs shadow-lg group-hover:block group-focus-visible:block"
      >
        <p class="num text-base font-semibold">{{ percent(row.exact, 2) }}</p>
        <p class="text-ink-2">exact chance to open at least one</p>
        <p class="num mt-1">
          {{ percent(row.estimate, 2) }}
          <span class="text-ink-2"
            >simulated ({{ row.handsWithCard.toLocaleString() }} of
            {{ row.samples.toLocaleString() }})</span
          >
        </p>
      </div>
    </div>
    <div class="grid grid-cols-[minmax(6rem,14rem)_1fr_3.5rem] gap-3 px-1">
      <span />
      <ChartAxis labels />
      <span />
    </div>
  </figure>
</template>
