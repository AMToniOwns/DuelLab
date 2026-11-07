<script setup lang="ts">
import { computed } from 'vue'

/** Sample count, hand size and seed. */
const sampleCount = defineModel<number>('sampleCount', { required: true })
const handSize = defineModel<number>('handSize', { required: true })
const seed = defineModel<string>('seed', { required: true })

const MAX_SEED = 9_007_199_254_740_991
const seedError = computed(() => {
  if (seed.value.trim() === '') return null
  return /^\d+$/.test(seed.value.trim()) && Number(seed.value) <= MAX_SEED
    ? null
    : 'Use a whole number from 0 to 9,007,199,254,740,991.'
})
defineExpose({ seedError })

function randomSeed(): void {
  seed.value = String(Math.floor(Math.random() * 1_000_000_000))
}
</script>

<template>
  <div class="grid gap-3 sm:grid-cols-3">
    <div>
      <label class="label" for="sample-count">Simulated hands</label>
      <select id="sample-count" v-model.number="sampleCount" class="input">
        <option :value="100">100</option>
        <option :value="1000">1,000</option>
        <option :value="10000">10,000</option>
        <option :value="100000">100,000</option>
      </select>
    </div>
    <div>
      <label class="label" for="hand-size">Opening hand size</label>
      <input
        id="hand-size"
        v-model.number="handSize"
        type="number"
        min="1"
        max="20"
        class="input num"
      />
      <p class="mt-1 text-xs text-ink-muted">
        5 going first; 6 to include your first draw going second.
      </p>
    </div>
    <div>
      <label class="label" for="seed"
        >Seed <span class="font-normal text-ink-muted">(optional)</span></label
      >
      <div class="flex gap-1.5">
        <input
          id="seed"
          v-model="seed"
          inputmode="numeric"
          class="input num"
          placeholder="Random"
          :aria-invalid="seedError !== null"
          aria-describedby="seed-help"
        />
        <button type="button" class="btn btn-sm" title="Pick a random seed" @click="randomSeed">
          🎲
        </button>
      </div>
      <p
        id="seed-help"
        class="mt-1 text-xs"
        :class="seedError ? 'text-critical' : 'text-ink-muted'"
      >
        {{ seedError ?? 'The same seed and deck always give the same hands.' }}
      </p>
    </div>
  </div>
</template>
