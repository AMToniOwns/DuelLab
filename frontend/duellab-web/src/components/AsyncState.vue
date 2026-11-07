<script setup lang="ts">
/** Shows a loading line or an error with a retry button; renders the slot otherwise. */
defineProps<{ loading: boolean; error: string | null }>()
defineEmits<{ retry: [] }>()
</script>

<template>
  <div
    v-if="error"
    class="card flex flex-wrap items-center gap-3 border-critical! p-4 text-sm"
    role="alert"
  >
    <span class="text-critical" aria-hidden="true">✕</span>
    <span class="flex-1">{{ error }}</span>
    <button type="button" class="btn btn-sm" @click="$emit('retry')">Try again</button>
  </div>
  <div v-else-if="loading" class="p-4 text-sm text-ink-muted" role="status">Loading…</div>
  <slot v-else />
</template>
