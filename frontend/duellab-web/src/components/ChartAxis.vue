<script setup lang="ts">
/** Percentage gridlines (0–100%) behind a chart's tracks, and their labels underneath. */
defineProps<{ labels?: boolean }>()
const ticks = [0, 25, 50, 75, 100]
</script>

<template>
  <div v-if="!labels" class="pointer-events-none absolute inset-0" aria-hidden="true">
    <div
      v-for="t in ticks"
      :key="t"
      class="absolute top-0 bottom-0 w-px"
      :style="{ left: `${t}%`, background: t === 0 ? 'var(--axis)' : 'var(--grid)' }"
    />
  </div>
  <div v-else class="relative h-4 text-[11px] text-ink-muted" aria-hidden="true">
    <span
      v-for="t in ticks"
      :key="t"
      class="num absolute -translate-x-1/2"
      :class="{ 'translate-x-0!': t === 0, '-translate-x-full!': t === 100 }"
      :style="{ left: `${t}%` }"
      >{{ t }}%</span
    >
  </div>
</template>
