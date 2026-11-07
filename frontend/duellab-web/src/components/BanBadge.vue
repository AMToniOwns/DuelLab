<script setup lang="ts">
import type { BanStatus } from '@/api/types'

defineProps<{ status: BanStatus }>()

const labels: Record<BanStatus, string> = {
  Unlimited: '',
  SemiLimited: 'Semi-Limited',
  Limited: 'Limited',
  Forbidden: 'Forbidden',
}
</script>

<template>
  <span
    v-if="status !== 'Unlimited'"
    class="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-[11px] font-medium"
    :class="
      status === 'Forbidden' ? 'bg-critical-soft text-critical' : 'bg-warning-soft text-warning'
    "
    :title="`TCG status in the synced card data: ${labels[status]}`"
  >
    <span aria-hidden="true">{{
      status === 'Forbidden' ? '⊘' : status === 'Limited' ? '①' : '②'
    }}</span>
    {{ labels[status] }}
  </span>
</template>
