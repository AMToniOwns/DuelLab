<script setup lang="ts">
import { computed } from 'vue'
import type { SampleHand, SnapshotCard } from '@/api/types'
import CardImage from './CardImage.vue'

/** A few real simulated hands, with which scenarios each one met. */
const props = defineProps<{ hands: SampleHand[]; scenarioNames: string[]; cards: SnapshotCard[] }>()

const byKey = computed(() => new Map(props.cards.map((c) => [String(c.cardId), c])))
</script>

<template>
  <ol class="space-y-3">
    <li
      v-for="hand in hands"
      :key="hand.index"
      class="rounded-lg border border-line p-3"
      :data-testid="`sample-hand-${hand.index}`"
    >
      <div class="mb-2 flex items-baseline justify-between gap-2">
        <span class="num text-xs text-ink-muted">Hand #{{ hand.index + 1 }}</span>
      </div>
      <div class="flex flex-wrap gap-2">
        <figure v-for="(key, i) in hand.cardKeys" :key="i" class="w-16">
          <CardImage
            :src="`/api/cards/${key}/image?size=small`"
            :name="byKey.get(key)?.name ?? key"
            size="sm"
            class="w-16!"
          />
          <figcaption class="mt-0.5 line-clamp-2 text-[10px] leading-tight text-ink-2">
            {{ byKey.get(key)?.name ?? key }}
          </figcaption>
        </figure>
      </div>
      <ul class="mt-2 flex flex-wrap gap-1.5" aria-label="Scenarios">
        <li
          v-for="(name, s) in scenarioNames"
          :key="s"
          class="rounded px-1.5 py-0.5 text-[11px]"
          :class="
            hand.scenarioOutcomes[s]
              ? 'bg-accent-soft text-ink'
              : 'bg-surface-2 text-ink-muted line-through decoration-ink-muted/50'
          "
        >
          <span aria-hidden="true">{{ hand.scenarioOutcomes[s] ? '✓' : '✗' }}</span>
          {{ name
          }}<span class="sr-only">{{ hand.scenarioOutcomes[s] ? ': met' : ': not met' }}</span>
        </li>
      </ul>
    </li>
  </ol>
</template>
