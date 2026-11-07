<script setup lang="ts">
import { computed } from 'vue'
import type { ImportReport } from '@/api/types'

/** What happened when a .ydk file was imported: counts, plus anything skipped or changed. */
const props = withDefaults(
  defineProps<{
    report: ImportReport & { fileName: string }
    deckErrors?: number
    unsaved?: boolean
  }>(),
  {
    deckErrors: 0,
    unsaved: false,
  },
)
defineEmits<{ dismiss: [] }>()

const skipped = computed(
  () =>
    props.report.unknown.length + props.report.trimmed.length + props.report.warnings.length > 0,
)
const attention = computed(() => skipped.value || props.deckErrors > 0)
</script>

<template>
  <section
    class="card p-3 text-sm"
    :class="attention ? 'bg-warning-soft!' : 'bg-accent-soft!'"
    role="status"
    data-testid="import-report"
  >
    <div class="flex items-start justify-between gap-3">
      <div class="space-y-1">
        <p class="font-medium">
          Imported {{ report.fileName }}: {{ report.imported.main }} Main,
          {{ report.imported.extra }} Extra, {{ report.imported.side }} Side.
          <span v-if="report.alternateArtworks" class="font-normal text-ink-2">
            {{ report.alternateArtworks }} alternate-artwork
            {{ report.alternateArtworks === 1 ? 'copy was' : 'copies were' }} matched to their
            cards.
          </span>
        </p>
        <p v-if="report.unknown.length">
          <strong class="font-semibold">Not in the card database, skipped:</strong>
          <template v-for="(u, i) in report.unknown" :key="`${u.passcode}-${u.zone}`">
            <span v-if="i">, </span><code class="num">{{ u.passcode }}</code> ×{{ u.copies }} ({{
              u.zone
            }})
          </template>
          <span class="text-ink-2">
            (OCG-only or very new cards; try again after the next card import)</span
          >
        </p>
        <p v-if="report.trimmed.length">
          <strong class="font-semibold">More than 3 copies, cut to 3:</strong>
          {{ report.trimmed.map((t) => `${t.name} (listed ${t.listed})`).join(', ') }}
        </p>
        <p v-for="(w, i) in report.warnings" :key="i" class="text-ink-2">{{ w }}</p>
        <p v-if="!skipped" class="text-ink-2">Every card was recognised.</p>
        <p v-if="unsaved" class="text-ink-2" data-testid="import-unsaved">
          Loaded into your draft: review it, then
          <strong class="font-semibold text-ink">Save</strong> to keep it and run the deck check (or
          Discard).
        </p>
        <p v-if="deckErrors > 0" data-testid="import-deck-errors">
          <strong class="font-semibold"
            >{{ deckErrors }} deck-check {{ deckErrors === 1 ? 'problem' : 'problems' }} to
            fix</strong
          >
          (see the panel on the right), or switch the deck to Casual if they are Forbidden &amp;
          Limited issues.
        </p>
      </div>
      <button
        type="button"
        class="btn btn-sm"
        aria-label="Dismiss import report"
        @click="$emit('dismiss')"
      >
        ✕
      </button>
    </div>
  </section>
</template>
