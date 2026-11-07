<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'

/** An accessible modal built on the native <dialog> element (focus trap and Esc come for free). */
const props = defineProps<{ open: boolean; title: string }>()
const emit = defineEmits<{ close: [] }>()
const dialog = ref<HTMLDialogElement | null>(null)

function sync(): void {
  const el = dialog.value
  if (!el) return
  if (props.open && !el.open) el.showModal()
  if (!props.open && el.open) el.close()
}
watch(() => props.open, sync)
onMounted(sync)
onBeforeUnmount(() => dialog.value?.close())
</script>

<template>
  <dialog
    ref="dialog"
    class="m-auto w-[min(48rem,calc(100vw-2rem))] rounded-xl border border-line bg-surface p-0 text-ink shadow-2xl backdrop:bg-black/50"
    :aria-label="title"
    @close="emit('close')"
    @click.self="emit('close')"
  >
    <div class="flex items-center justify-between border-b border-line px-4 py-3">
      <h2 class="font-semibold">{{ title }}</h2>
      <button type="button" class="btn btn-sm" aria-label="Close" @click="emit('close')">✕</button>
    </div>
    <div class="max-h-[75vh] overflow-y-auto p-4">
      <slot />
    </div>
  </dialog>
</template>
