<script setup lang="ts">
import { ref, watch } from 'vue'
import type { Card } from '@/api/types'
import { addCard, defaultZone } from '@/lib/deckDraft'
import CardDetail from '@/components/CardDetail.vue'
import CardSearch from '@/components/CardSearch.vue'
import VersionPicker from '@/components/VersionPicker.vue'
import { useDeckEditorStore } from '@/stores/deckEditor'
import { useToastStore } from '@/stores/toasts'
import { errorMessage } from '@/api/client'

const editor = useDeckEditorStore()
const toasts = useToastStore()
const selected = ref<Card | null>(null)
const target = ref<string | null>(null)

// Adding goes into the target version's draft; the user reviews and saves it in the editor.
watch(target, async (id) => {
  if (id && !editor.draft(id)) {
    try {
      await editor.load(id)
    } catch (e) {
      toasts.error(errorMessage(e))
    }
  }
})

function add(card: Card): void {
  const draft = target.value ? editor.draft(target.value) : undefined
  if (!draft) {
    toasts.error('Choose a deck version to add cards to.')
    return
  }
  const problem = addCard(draft, card, defaultZone(card), editor.banRules(target.value!))
  editor.touched()
  if (problem) toasts.error(problem)
  else toasts.success(`Added ${card.name}. Review and save it in the deck editor.`)
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <h1 class="text-2xl font-semibold tracking-tight">Card database</h1>
      <div class="w-full max-w-xl">
        <VersionPicker v-model="target" label="Add cards to" id-prefix="target" />
        <RouterLink
          v-if="target"
          :to="{ name: 'editor', params: { versionId: target } }"
          class="mt-1 inline-block text-xs text-accent"
        >
          Open this version in the editor →
        </RouterLink>
      </div>
    </div>

    <div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,28rem)]">
      <section class="card p-4" aria-label="Search">
        <CardSearch
          :page-size="30"
          :add-label="target ? 'Add' : ''"
          @select="selected = $event"
          @add="add"
        />
      </section>
      <section
        class="card p-4 lg:sticky lg:top-20 lg:self-start"
        aria-label="Card details"
        aria-live="polite"
      >
        <CardDetail v-if="selected" :card="selected" />
        <p v-else class="text-sm text-ink-muted">Choose a card to see its details.</p>
        <button
          v-if="selected && target"
          type="button"
          class="btn btn-primary mt-4"
          @click="add(selected)"
        >
          Add to {{ editor.versions[target]?.versionName ?? 'deck' }}
        </button>
      </section>
    </div>
  </div>
</template>
