<script setup lang="ts">
import { ref, watch } from 'vue'

/**
 * Card artwork, served by DuelLab's own image endpoint (never hotlinked). Falls back to a labelled
 * placeholder when the image is not available, e.g. offline.
 */
const props = withDefaults(
  defineProps<{ src: string; name: string; size?: 'sm' | 'md' | 'lg' }>(),
  { size: 'md' },
)

const failed = ref(false)
watch(
  () => props.src,
  () => (failed.value = false),
)

const sizes = { sm: 'w-12', md: 'w-24', lg: 'w-60' }
</script>

<template>
  <div
    class="relative aspect-[59/86] shrink-0 overflow-hidden rounded-[3px] bg-surface-2"
    :class="sizes[size]"
  >
    <img
      v-if="!failed"
      :src="src"
      :alt="name"
      loading="lazy"
      decoding="async"
      class="h-full w-full object-cover"
      @error="failed = true"
    />
    <div
      v-else
      class="flex h-full w-full items-center justify-center p-1 text-center text-[10px] leading-tight text-ink-muted"
      role="img"
      :aria-label="name"
    >
      {{ size === 'sm' ? name.slice(0, 18) : name }}
    </div>
  </div>
</template>
