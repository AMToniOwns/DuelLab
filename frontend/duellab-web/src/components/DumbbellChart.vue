<script setup lang="ts">
import { percent, pointDelta } from '@/lib/format'
import ChartAxis from './ChartAxis.vue'

/**
 * Before → after per scenario: a dot for each version on a shared 0–100% axis, joined by a line.
 * Two shades of one hue (validated as an ordinal pair); the change is printed as signed text, not
 * coloured good/bad, because "up" is not always better (e.g. bricks).
 */
export interface DumbbellRow {
  name: string
  description: string
  base: number
  candidate: number
  deltaPp: number
}

defineProps<{ rows: DumbbellRow[]; baseLabel: string; candidateLabel: string }>()

const pct = (v: number) => `${Math.min(100, Math.max(0, v * 100))}%`
</script>

<template>
  <figure class="space-y-3">
    <figcaption class="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-ink-2">
      <span class="inline-flex items-center gap-1.5">
        <span
          class="inline-block h-2.5 w-2.5 rounded-full"
          style="background: var(--compare-base)"
          aria-hidden="true"
        />
        {{ baseLabel }} (base)
      </span>
      <span class="inline-flex items-center gap-1.5">
        <span
          class="inline-block h-2.5 w-2.5 rounded-full"
          style="background: var(--compare-candidate)"
          aria-hidden="true"
        />
        {{ candidateLabel }} (candidate)
      </span>
    </figcaption>
    <div class="space-y-1">
      <div
        v-for="row in rows"
        :key="row.name"
        class="group relative grid grid-cols-[minmax(6rem,11rem)_1fr_4.5rem] items-center gap-3 rounded-md px-1 py-1 outline-none hover:bg-surface-2 focus-visible:bg-surface-2"
        tabindex="0"
        :aria-label="`${row.name}: ${percent(row.base)} in base, ${percent(row.candidate)} in candidate, change ${pointDelta(row.deltaPp)}`"
      >
        <span class="truncate text-sm" :title="row.description">{{ row.name }}</span>
        <div class="relative h-6">
          <ChartAxis />
          <div
            class="absolute top-1/2 h-0.5 -translate-y-1/2 bg-ink-muted"
            :style="{
              left: pct(Math.min(row.base, row.candidate)),
              width: `calc(${pct(Math.max(row.base, row.candidate))} - ${pct(Math.min(row.base, row.candidate))})`,
            }"
          />
          <div
            class="absolute top-1/2 h-3 w-3 -translate-1/2 rounded-full ring-2 ring-surface"
            :style="{ left: pct(row.base), background: 'var(--compare-base)' }"
          />
          <div
            class="absolute top-1/2 h-3 w-3 -translate-1/2 rounded-full ring-2 ring-surface"
            :style="{ left: pct(row.candidate), background: 'var(--compare-candidate)' }"
          />
        </div>
        <span class="num text-right text-sm font-medium">{{ pointDelta(row.deltaPp) }}</span>
        <div
          class="pointer-events-none absolute bottom-full left-1/3 z-10 mb-1 hidden w-60 rounded-lg border border-line bg-surface p-2.5 text-xs shadow-lg group-hover:block group-focus-visible:block"
        >
          <p class="num text-base font-semibold">{{ pointDelta(row.deltaPp, 2) }}</p>
          <p class="num">
            {{ percent(row.base, 2) }} <span class="text-ink-2">→</span>
            {{ percent(row.candidate, 2) }}
          </p>
          <p class="mt-1 text-ink-muted">{{ row.description }}</p>
        </div>
      </div>
    </div>
    <div class="grid grid-cols-[minmax(6rem,11rem)_1fr_4.5rem] gap-3 px-1">
      <span />
      <ChartAxis labels />
      <span />
    </div>
  </figure>
</template>
