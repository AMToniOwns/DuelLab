<script setup lang="ts">
import { computed } from 'vue'
import type { Scenario } from '@/api/types'
import { roleLabel } from '@/lib/roles'

/**
 * Edits the conditions a simulation measures. Each scenario is a set of requirements that must all
 * hold (AND); "different cards" decides whether one card may count toward several requirements.
 */
const scenarios = defineModel<Scenario[]>({ required: true })
const props = defineProps<{ roles: string[]; roleCounts?: Record<string, number> }>()
defineEmits<{ reset: [] }>()

const roleOptions = computed(() =>
  props.roles.map((key) => ({ key, label: roleLabel(key), count: props.roleCounts?.[key] })),
)

function addScenario(): void {
  scenarios.value.push({
    name: `Scenario ${scenarios.value.length + 1}`,
    requirements: [{ role: 'Starter', minimumCount: 1, maximumCount: null }],
    requireDistinctCards: true,
  })
}

function addRequirement(scenario: Scenario): void {
  scenario.requirements.push({
    role: props.roles[0] ?? 'Starter',
    minimumCount: 1,
    maximumCount: null,
  })
}

function setMaximum(requirement: Scenario['requirements'][number], raw: string): void {
  requirement.maximumCount = raw === '' ? null : Math.max(0, Math.floor(Number(raw)))
}

const matters = (s: Scenario) =>
  s.requirements.filter((r) => r.minimumCount > 0).length > 1 ||
  s.requirements.some((r) => r.minimumCount > 1)
</script>

<template>
  <div class="space-y-3">
    <div
      v-for="(scenario, si) in scenarios"
      :key="si"
      class="rounded-lg border border-line p-3"
      :data-testid="`scenario-${si}`"
    >
      <div class="mb-2 flex items-center gap-2">
        <label class="sr-only" :for="`scenario-name-${si}`">Scenario name</label>
        <input
          :id="`scenario-name-${si}`"
          v-model="scenario.name"
          class="input font-medium"
          maxlength="60"
        />
        <button
          type="button"
          class="btn btn-sm btn-danger"
          :aria-label="`Remove scenario ${scenario.name}`"
          @click="scenarios.splice(si, 1)"
        >
          Remove
        </button>
      </div>

      <div class="space-y-1.5">
        <div
          v-for="(req, ri) in scenario.requirements"
          :key="ri"
          class="flex flex-wrap items-center gap-2 text-sm"
        >
          <span class="w-10 text-ink-muted">{{ ri === 0 ? 'Hand has' : 'and' }}</span>
          <label class="sr-only" :for="`min-${si}-${ri}`">Minimum</label>
          <span class="text-ink-2">at least</span>
          <input
            :id="`min-${si}-${ri}`"
            v-model.number="req.minimumCount"
            type="number"
            min="0"
            max="20"
            class="input num w-16!"
          />
          <label class="sr-only" :for="`role-${si}-${ri}`">Role</label>
          <select :id="`role-${si}-${ri}`" v-model="req.role" class="input w-auto!">
            <option v-for="o in roleOptions" :key="o.key" :value="o.key">
              {{ o.label }}{{ o.count !== undefined ? ` (${o.count} in deck)` : '' }}
            </option>
          </select>
          <span class="text-ink-2">at most</span>
          <label class="sr-only" :for="`max-${si}-${ri}`">Maximum (optional)</label>
          <input
            :id="`max-${si}-${ri}`"
            :value="req.maximumCount ?? ''"
            type="number"
            min="0"
            max="20"
            placeholder="any"
            class="input num w-16!"
            @input="setMaximum(req, ($event.target as HTMLInputElement).value)"
          />
          <button
            v-if="scenario.requirements.length > 1"
            type="button"
            class="text-ink-muted hover:text-critical"
            :aria-label="`Remove requirement ${ri + 1}`"
            @click="scenario.requirements.splice(ri, 1)"
          >
            ✕
          </button>
        </div>
      </div>

      <div class="mt-2 flex flex-wrap items-center justify-between gap-2">
        <button type="button" class="btn btn-sm" @click="addRequirement(scenario)">
          + Requirement
        </button>
        <label v-if="matters(scenario)" class="flex items-center gap-1.5 text-xs text-ink-2">
          <input v-model="scenario.requireDistinctCards" type="checkbox" />
          Each requirement needs <strong class="font-semibold">different cards</strong>
          <span class="text-ink-muted">(a card with two roles counts once)</span>
        </label>
      </div>
    </div>

    <div class="flex flex-wrap gap-2">
      <button
        type="button"
        class="btn btn-sm"
        :disabled="scenarios.length >= 20"
        @click="addScenario"
      >
        + Scenario
      </button>
      <button type="button" class="btn btn-sm" @click="$emit('reset')">Reset to defaults</button>
    </div>
  </div>
</template>
