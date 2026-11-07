import { ref } from 'vue'
import { defineStore } from 'pinia'

export interface Toast {
  id: number
  kind: 'info' | 'success' | 'error'
  message: string
}

/** Short-lived notifications shown in the corner of the screen. */
export const useToastStore = defineStore('toasts', () => {
  const toasts = ref<Toast[]>([])
  let next = 1

  function show(message: string, kind: Toast['kind'] = 'info', durationMs = 4500): void {
    const id = next++
    toasts.value.push({ id, kind, message })
    if (durationMs > 0) setTimeout(() => dismiss(id), durationMs)
  }

  function dismiss(id: number): void {
    toasts.value = toasts.value.filter((t) => t.id !== id)
  }

  return {
    toasts,
    show,
    dismiss,
    success: (m: string) => show(m, 'success'),
    error: (m: string) => show(m, 'error', 8000),
  }
})
