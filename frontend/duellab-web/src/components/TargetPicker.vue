<script setup lang="ts">
import { computed } from 'vue'
import { roleLabel } from '@/lib/roles'

/**
 * Picks what a requirement or combo piece looks for: any of several roles and/or specific cards.
 * Shown as removable chips plus an "add" menu.
 */
export interface PickableCard {
  id: number
  name: string
  inMain?: boolean
}

const roles = defineModel<string[]>('roles', { required: true })
const cardIds = defineModel<number[]>('cardIds', { required: true })
const props = withDefaults(
  defineProps<{
    availableRoles: string[]
    cards: PickableCard[]
    label: string
    roleCounts?: Record<string, number>
  }>(),
  { roleCounts: undefined },
)

const cardName = (id: number) => props.cards.find((c) => c.id === id)?.name ?? `Card #${id}`
const notInMain = (id: number) => {
  const card = props.cards.find((c) => c.id === id)
  return !card || card.inMain === false
}
const unusedRoles = computed(() => props.availableRoles.filter((r) => !roles.value.includes(r)))
const unusedCards = computed(() =>
  [...props.cards]
    .filter((c) => !cardIds.value.includes(c.id))
    .sort((a, b) => a.name.localeCompare(b.name)),
)

function add(value: string): void {
  if (value.startsWith('role:')) roles.value = [...roles.value, value.slice(5)]
  else if (value.startsWith('card:')) cardIds.value = [...cardIds.value, Number(value.slice(5))]
}
</script>

<template>
  <div class="flex flex-wrap items-center gap-1" role="group" :aria-label="label">
    <span
      v-for="role in roles"
      :key="`r-${role}`"
      class="inline-flex items-center gap-1 rounded bg-accent-soft py-0.5 pr-1 pl-1.5 text-xs"
    >
      <span class="text-ink-muted">any</span> {{ roleLabel(role) }}
      <button
        type="button"
        class="text-ink-muted hover:text-critical"
        :aria-label="`Remove ${roleLabel(role)}`"
        @click="roles = roles.filter((r) => r !== role)"
      >
        ×
      </button>
    </span>
    <span
      v-for="id in cardIds"
      :key="`c-${id}`"
      class="inline-flex items-center gap-1 rounded border border-line bg-surface-2 py-0.5 pr-1 pl-1.5 text-xs"
      :title="
        notInMain(id) ? 'Not in the Main Deck: it can never be in the opening hand' : undefined
      "
    >
      {{ cardName(id)
      }}<span v-if="notInMain(id)" class="text-warning" aria-label="not in the Main Deck">!</span>
      <button
        type="button"
        class="text-ink-muted hover:text-critical"
        :aria-label="`Remove ${cardName(id)}`"
        @click="cardIds = cardIds.filter((c) => c !== id)"
      >
        ×
      </button>
    </span>
    <span v-if="roles.length + cardIds.length > 1" class="sr-only">(any of these)</span>
    <label class="sr-only" :for="`${label}-add`">Add a role or card to {{ label }}</label>
    <select
      :id="`${label}-add`"
      class="input w-auto! py-0.5! text-xs!"
      :value="''"
      :data-testid="`add-target-${label}`"
      @change="
        add(($event.target as HTMLSelectElement).value)
        ;($event.target as HTMLSelectElement).value = ''
      "
    >
      <option value="">
        {{ roles.length + cardIds.length ? '+ or…' : '+ Choose role or card…' }}
      </option>
      <optgroup label="Roles">
        <option v-for="role in unusedRoles" :key="role" :value="`role:${role}`">
          {{ roleLabel(role)
          }}{{ roleCounts?.[role] !== undefined ? ` (${roleCounts[role]} in deck)` : '' }}
        </option>
      </optgroup>
      <optgroup v-if="unusedCards.length" label="Cards">
        <option v-for="card in unusedCards" :key="card.id" :value="`card:${card.id}`">
          {{ card.name }}
        </option>
      </optgroup>
    </select>
  </div>
</template>
