<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { RoleSuggestion } from '@/lib/roleSuggestions'
import { roleLabel } from '@/lib/roles'
import ModalDialog from './ModalDialog.vue'

/** Lists role suggestions with their reasons; the user picks which to apply. */
const props = defineProps<{ open: boolean; suggestions: RoleSuggestion[] }>()
const emit = defineEmits<{ close: []; apply: [chosen: RoleSuggestion[]] }>()

const key = (s: RoleSuggestion) => `${s.cardId}:${s.role}`
const chosen = ref(new Set<string>())
watch(
  () => [props.open, props.suggestions] as const,
  () => {
    chosen.value = new Set(props.suggestions.filter((s) => s.recommended).map(key))
  },
  { immediate: true },
)

const groups = computed(() =>
  [
    {
      title: 'From DuelLab’s list of well-known cards',
      items: props.suggestions.filter((s) => s.source === 'known'),
    },
    { title: 'From card text', items: props.suggestions.filter((s) => s.source === 'text') },
    {
      title: 'Possible: check these yourself',
      items: props.suggestions.filter((s) => s.source === 'possible'),
    },
  ].filter((g) => g.items.length > 0),
)

function toggle(s: RoleSuggestion): void {
  const next = new Set(chosen.value)
  if (next.has(key(s))) next.delete(key(s))
  else next.add(key(s))
  chosen.value = next
}

function apply(): void {
  emit(
    'apply',
    props.suggestions.filter((s) => chosen.value.has(key(s))),
  )
}
</script>

<template>
  <ModalDialog :open="open" title="Suggest roles" @close="emit('close')">
    <p class="mb-3 text-sm text-ink-2">
      Hand traps and board breakers come from a list of well-known cards; searchers from card text
      that adds a card from the Deck to the hand. Starters, extenders and bricks depend on your
      deck, so mark those yourself. Suggestions only add roles; nothing is saved until you press
      Save.
    </p>
    <p v-if="suggestions.length === 0" class="text-sm text-ink-muted" data-testid="no-suggestions">
      No suggestions: these cards either already have the suggested roles or are not on the list.
    </p>
    <section v-for="group in groups" :key="group.title" class="mb-4">
      <h3 class="mb-1 text-xs font-semibold tracking-wide text-ink-2 uppercase">
        {{ group.title }}
      </h3>
      <ul class="space-y-1">
        <li v-for="s in group.items" :key="key(s)">
          <label class="flex items-start gap-2 rounded-md p-1.5 text-sm hover:bg-surface-2">
            <input
              type="checkbox"
              class="mt-0.5"
              :checked="chosen.has(key(s))"
              :data-testid="`suggest-${s.cardName}-${s.role}`"
              @change="toggle(s)"
            />
            <span>
              <span class="font-medium">{{ s.cardName }}</span> → {{ roleLabel(s.role) }}
              <span class="block text-xs text-ink-muted">{{ s.reason }}</span>
            </span>
          </label>
        </li>
      </ul>
    </section>
    <div class="flex justify-end gap-2 border-t border-line pt-3">
      <button type="button" class="btn" @click="emit('close')">Cancel</button>
      <button
        type="button"
        class="btn btn-primary"
        :disabled="chosen.size === 0"
        data-testid="apply-suggestions"
        @click="apply"
      >
        Add {{ chosen.size }} {{ chosen.size === 1 ? 'role' : 'roles' }}
      </button>
    </div>
  </ModalDialog>
</template>
