<script setup lang="ts">
import { useToastStore } from '@/stores/toasts'

const toasts = useToastStore()
</script>

<template>
  <div
    class="pointer-events-none fixed right-4 bottom-4 z-50 flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2"
    aria-live="polite"
  >
    <div
      v-for="toast in toasts.toasts"
      :key="toast.id"
      class="card pointer-events-auto flex items-start gap-2 p-3 text-sm shadow-lg"
      :class="{ 'border-critical!': toast.kind === 'error' }"
      :role="toast.kind === 'error' ? 'alert' : 'status'"
    >
      <span
        aria-hidden="true"
        :class="
          toast.kind === 'error'
            ? 'text-critical'
            : toast.kind === 'success'
              ? 'text-good'
              : 'text-accent'
        "
      >
        {{ toast.kind === 'error' ? '✕' : toast.kind === 'success' ? '✓' : 'ℹ' }}
      </span>
      <span class="flex-1">{{ toast.message }}</span>
      <button
        type="button"
        class="text-ink-muted hover:text-ink"
        aria-label="Dismiss"
        @click="toasts.dismiss(toast.id)"
      >
        ×
      </button>
    </div>
  </div>
</template>
