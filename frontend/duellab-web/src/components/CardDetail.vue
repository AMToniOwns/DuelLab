<script setup lang="ts">
import { computed } from 'vue'
import type { Card } from '@/api/types'
import CardImage from './CardImage.vue'
import BanBadge from './BanBadge.vue'

const props = defineProps<{ card: Card }>()

const stats = computed(() => {
  const c = props.card
  if (c.kind !== 'Monster') return null
  const parts: string[] = []
  if (c.linkValue !== null) parts.push(`LINK-${c.linkValue}`)
  else if (c.level !== null)
    parts.push(`${c.typeLine.includes('XYZ') ? 'Rank' : 'Level'} ${c.level}`)
  parts.push(`ATK ${c.attack ?? '?'}`)
  if (c.linkValue === null) parts.push(`DEF ${c.defense ?? '?'}`)
  return parts.join(' · ')
})
</script>

<template>
  <article class="flex flex-col gap-4 sm:flex-row">
    <CardImage :src="card.imageUrl" :name="card.name" size="lg" class="mx-auto sm:mx-0" />
    <div class="min-w-0 flex-1 space-y-2">
      <h3 class="text-lg font-semibold">{{ card.name }}</h3>
      <div class="flex flex-wrap items-center gap-2 text-sm text-ink-2">
        <span>{{ card.typeLine }}</span>
        <span v-if="card.attribute">· {{ card.attribute }}</span>
        <span v-if="card.race">· {{ card.race }}</span>
        <BanBadge :status="card.tcgBanStatus" />
      </div>
      <p v-if="stats" class="num text-sm text-ink-2">{{ stats }}</p>
      <p v-if="card.archetype" class="text-xs text-ink-muted">Archetype: {{ card.archetype }}</p>
      <p class="whitespace-pre-line text-sm leading-relaxed">{{ card.description }}</p>
      <p class="text-xs text-ink-muted">
        Passcode {{ card.externalId }} · data synced
        {{ new Date(card.lastSyncedAt).toLocaleDateString() }}
      </p>
    </div>
  </article>
</template>
