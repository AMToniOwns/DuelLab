<script setup lang="ts">
import { percent } from '@/lib/format'
import ChartAxis from './ChartAxis.vue'

/**
 * One horizontal bar per scenario: the simulated estimate (bar), its 95% confidence interval
 * (whisker) and the exact probability (tick). Single hue; text uses ink tokens, never the bar colour.
 */
export interface ProbabilityRow {
  name: string
  description: string
  estimate: number
  lower: number
  upper: number
  exact: number | null
  successes: number
  samples: number
}

defineProps<{ rows: ProbabilityRow[] }>()

const pct = (v: number) => `${Math.min(100, Math.max(0, v * 100))}%`
</script>

<template>
  <figure class="space-y-3">
    <figcaption class="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-ink-2">
      <span class="inline-flex items-center gap-1.5">
        <span
          class="inline-block h-2.5 w-4 rounded-r-[3px]"
          style="background: var(--series-1)"
          aria-hidden="true"
        />
        Simulated estimate
      </span>
      <span class="inline-flex items-center gap-1.5">
        <span class="inline-block h-3.5 w-0.5 bg-ink" aria-hidden="true" />
        Exact probability
      </span>
      <span class="inline-flex items-center gap-1.5">
        <span class="relative inline-block h-2.5 w-5" aria-hidden="true">
          <span class="absolute top-1/2 right-0 left-0 h-px bg-ink-2" />
          <span class="absolute top-0 bottom-0 left-0 w-px bg-ink-2" />
          <span class="absolute top-0 right-0 bottom-0 w-px bg-ink-2" />
        </span>
        95% confidence interval
      </span>
    </figcaption>

    <div class="space-y-1">
      <div
        v-for="row in rows"
        :key="row.name"
        class="group relative grid grid-cols-[minmax(6rem,11rem)_1fr_3.5rem] items-center gap-3 rounded-md px-1 py-1 outline-none hover:bg-surface-2 focus-visible:bg-surface-2"
        tabindex="0"
        :aria-label="`${row.name}: estimate ${percent(row.estimate)}, exact ${percent(row.exact, 2)}, 95% interval ${percent(row.lower)} to ${percent(row.upper)}`"
      >
        <span class="truncate text-sm" :title="row.description">{{ row.name }}</span>
        <div class="relative h-6">
          <ChartAxis />
          <div
            class="absolute top-1/2 left-0 h-3 -translate-y-1/2 rounded-r-[4px] transition-[filter] group-hover:brightness-110"
            :style="{ width: pct(row.estimate), background: 'var(--series-1)' }"
          />
          <div
            class="absolute top-1/2 h-px -translate-y-1/2 bg-ink-2"
            :style="{ left: pct(row.lower), width: `calc(${pct(row.upper)} - ${pct(row.lower)})` }"
          />
          <div
            class="absolute top-1/2 h-2.5 w-px -translate-y-1/2 bg-ink-2"
            :style="{ left: pct(row.lower) }"
          />
          <div
            class="absolute top-1/2 h-2.5 w-px -translate-y-1/2 bg-ink-2"
            :style="{ left: pct(row.upper) }"
          />
          <div
            v-if="row.exact !== null"
            class="absolute top-0 bottom-0 w-0.5 -translate-x-1/2 bg-ink"
            :style="{ left: pct(row.exact) }"
          />
        </div>
        <span class="num text-right text-sm font-medium">{{ percent(row.estimate) }}</span>

        <div
          class="pointer-events-none absolute bottom-full left-1/3 z-10 mb-1 hidden w-64 rounded-lg border border-line bg-surface p-2.5 text-xs shadow-lg group-hover:block group-focus-visible:block"
          role="presentation"
        >
          <p class="num text-base font-semibold">{{ percent(row.estimate, 2) }}</p>
          <p class="text-ink-2">
            simulated ({{ row.successes.toLocaleString() }} of
            {{ row.samples.toLocaleString() }} hands)
          </p>
          <p class="num mt-1">
            <strong class="font-semibold">{{ percent(row.exact, 2) }}</strong>
            <span class="text-ink-2">exact</span>
          </p>
          <p class="num">
            <span class="text-ink-2">95% interval</span> {{ percent(row.lower, 2) }} –
            {{ percent(row.upper, 2) }}
          </p>
          <p class="mt-1 text-ink-muted">{{ row.description }}</p>
        </div>
      </div>
    </div>

    <div class="grid grid-cols-[minmax(6rem,11rem)_1fr_3.5rem] gap-3 px-1">
      <span />
      <ChartAxis labels />
      <span />
    </div>
  </figure>
</template>
