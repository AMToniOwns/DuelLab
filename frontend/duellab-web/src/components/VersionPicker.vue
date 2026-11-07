<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { api, errorMessage } from '@/api/client'
import type { DeckSummary } from '@/api/types'

/** Chooses a deck and one of its versions. */
const versionId = defineModel<string | null>({ required: true })
const props = withDefaults(
  defineProps<{ label?: string; idPrefix?: string; decks?: DeckSummary[] | null }>(),
  {
    label: 'Deck version',
    idPrefix: 'picker',
    decks: null,
  },
)

const loaded = ref<DeckSummary[]>([])
const error = ref<string | null>(null)
const all = computed(() => props.decks ?? loaded.value)
const deckId = ref<string | null>(null)
const deck = computed(() => all.value.find((d) => d.id === deckId.value) ?? null)

function syncDeckFromVersion(): void {
  const owner = all.value.find((d) => d.versions.some((v) => v.id === versionId.value))
  if (owner) deckId.value = owner.id
}

onMounted(async () => {
  if (!props.decks) {
    try {
      loaded.value = await api.decks.list()
    } catch (e) {
      error.value = errorMessage(e)
    }
  }
  syncDeckFromVersion()
})
watch(() => [versionId.value, all.value] as const, syncDeckFromVersion)

function chooseDeck(id: string): void {
  deckId.value = id
  const versions = all.value.find((d) => d.id === id)?.versions ?? []
  versionId.value = versions[versions.length - 1]?.id ?? null
}
</script>

<template>
  <fieldset class="grid gap-2 sm:grid-cols-2">
    <legend class="label">{{ label }}</legend>
    <p v-if="error" class="text-sm text-critical sm:col-span-2">{{ error }}</p>
    <div>
      <label class="sr-only" :for="`${idPrefix}-deck`">Deck</label>
      <select
        :id="`${idPrefix}-deck`"
        class="input"
        :value="deckId ?? ''"
        @change="chooseDeck(($event.target as HTMLSelectElement).value)"
      >
        <option value="" disabled>Choose a deck…</option>
        <option v-for="d in all" :key="d.id" :value="d.id">{{ d.name }}</option>
      </select>
    </div>
    <div>
      <label class="sr-only" :for="`${idPrefix}-version`">Version</label>
      <select :id="`${idPrefix}-version`" v-model="versionId" class="input" :disabled="!deck">
        <option :value="null" disabled>Choose a version…</option>
        <option v-for="v in deck?.versions ?? []" :key="v.id" :value="v.id">
          {{ v.versionName }} · {{ v.mainCount }} main
        </option>
      </select>
    </div>
  </fieldset>
</template>
