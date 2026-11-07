<script setup lang="ts">
import type { ValidationIssue } from '@/api/types'
import { LIMITS } from '@/lib/deckDraft'

/** Zone counts against their limits, plus errors and warnings. */
defineProps<{
  main: number
  extra: number
  side: number
  issues: ValidationIssue[]
  stale?: boolean
}>()
</script>

<template>
  <section aria-labelledby="validation-heading" class="space-y-2">
    <h2 id="validation-heading" class="text-sm font-semibold">Deck check</h2>
    <dl class="num grid grid-cols-3 gap-2 text-center">
      <div class="rounded-lg bg-surface-2 p-2">
        <dt class="text-[11px] text-ink-2">Main</dt>
        <dd
          class="text-lg font-semibold"
          :class="{ 'text-critical': main < LIMITS.mainMin || main > LIMITS.mainMax }"
        >
          {{ main }}
        </dd>
        <dd class="text-[11px] text-ink-muted">40–60</dd>
      </div>
      <div class="rounded-lg bg-surface-2 p-2">
        <dt class="text-[11px] text-ink-2">Extra</dt>
        <dd class="text-lg font-semibold" :class="{ 'text-critical': extra > LIMITS.extraMax }">
          {{ extra }}
        </dd>
        <dd class="text-[11px] text-ink-muted">0–15</dd>
      </div>
      <div class="rounded-lg bg-surface-2 p-2">
        <dt class="text-[11px] text-ink-2">Side</dt>
        <dd class="text-lg font-semibold" :class="{ 'text-critical': side > LIMITS.sideMax }">
          {{ side }}
        </dd>
        <dd class="text-[11px] text-ink-muted">0–15</dd>
      </div>
    </dl>
    <p v-if="stale" class="text-xs text-ink-muted">Save to re-check against the card database.</p>
    <ul v-if="issues.length" class="space-y-1.5 text-xs">
      <li
        v-for="(issue, i) in issues"
        :key="i"
        class="flex gap-1.5 rounded-md p-2"
        :class="issue.severity === 'Error' ? 'bg-critical-soft' : 'bg-warning-soft'"
      >
        <span
          aria-hidden="true"
          :class="issue.severity === 'Error' ? 'text-critical' : 'text-warning'"
          >{{ issue.severity === 'Error' ? '✕' : '!' }}</span
        >
        <span
          ><span class="sr-only">{{ issue.severity }}: </span>{{ issue.message }}</span
        >
      </li>
    </ul>
    <p v-else-if="!stale" class="flex items-center gap-1.5 text-xs text-good">
      <span aria-hidden="true">✓</span> Ready to simulate.
    </p>
  </section>
</template>
