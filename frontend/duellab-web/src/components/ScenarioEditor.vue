<script setup lang="ts">
import type { Clause, Requirement, Scenario } from '@/api/types'
import {
  addAlternative,
  clausesOf,
  distinctMatters,
  newRequirement,
  removeAlternative,
  setTargets,
  targetsOf,
} from '@/lib/scenarios'
import TargetPicker, { type PickableCard } from './TargetPicker.vue'

/**
 * Edits the conditions a simulation measures. A scenario is met when ANY of its alternatives holds; an
 * alternative holds when ALL of its requirements do. A requirement counts cards that have any of the
 * chosen roles or are any of the chosen cards.
 */
const scenarios = defineModel<Scenario[]>({ required: true })
const props = withDefaults(
  defineProps<{ roles: string[]; roleCounts?: Record<string, number>; cards?: PickableCard[] }>(),
  {
    roleCounts: undefined,
    cards: () => [],
  },
)
defineEmits<{ reset: [] }>()

function addScenario(): void {
  scenarios.value.push({
    name: `Scenario ${scenarios.value.length + 1}`,
    requirements: [newRequirement(props.roles[0])],
    requireDistinctCards: true,
    anyOf: null,
  })
}

function setMaximum(requirement: Requirement, raw: string): void {
  requirement.maximumCount = raw === '' ? null : Math.max(0, Math.floor(Number(raw)))
}

function updateTargets(
  requirement: Requirement,
  roles: string[] | null,
  cardIds: number[] | null,
): void {
  const current = targetsOf(requirement)
  setTargets(requirement, roles ?? current.roles, cardIds ?? current.cardIds)
}

function removeRequirement(scenario: Scenario, clause: Clause, index: number): void {
  clause.requirements.splice(index, 1)
  if (clause.requirements.length === 0) {
    const clauses = clausesOf(scenario)
    if (clauses.length > 1) removeAlternative(scenario, clauses.indexOf(clause))
  }
}
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
          maxlength="100"
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

      <template v-for="(clause, ci) in clausesOf(scenario)" :key="ci">
        <p
          v-if="clausesOf(scenario).length > 1"
          class="my-1.5 text-xs font-semibold tracking-wide text-ink-2 uppercase"
        >
          {{ ci === 0 ? 'Either' : 'Or' }}
        </p>
        <div
          class="space-y-1.5"
          :class="{ 'rounded-md border-l-2 border-accent pl-2': clausesOf(scenario).length > 1 }"
        >
          <div
            v-for="(req, ri) in clause.requirements"
            :key="ri"
            class="flex flex-wrap items-center gap-2 text-sm"
          >
            <span class="w-10 text-ink-muted">{{ ri === 0 ? 'Hand has' : 'and' }}</span>
            <span class="text-ink-2">at least</span>
            <label class="sr-only" :for="`min-${si}-${ci}-${ri}`">Minimum</label>
            <input
              :id="`min-${si}-${ci}-${ri}`"
              v-model.number="req.minimumCount"
              type="number"
              min="0"
              max="20"
              class="input num w-16!"
            />
            <TargetPicker
              :roles="targetsOf(req).roles"
              :card-ids="targetsOf(req).cardIds"
              :available-roles="roles"
              :role-counts="roleCounts"
              :cards="cards"
              :label="`target-${si}-${ci}-${ri}`"
              @update:roles="updateTargets(req, $event, null)"
              @update:card-ids="updateTargets(req, null, $event)"
            />
            <span class="text-ink-2">at most</span>
            <label class="sr-only" :for="`max-${si}-${ci}-${ri}`">Maximum (optional)</label>
            <input
              :id="`max-${si}-${ci}-${ri}`"
              :value="req.maximumCount ?? ''"
              type="number"
              min="0"
              max="20"
              placeholder="any"
              class="input num w-16!"
              @input="setMaximum(req, ($event.target as HTMLInputElement).value)"
            />
            <button
              v-if="clause.requirements.length > 1 || clausesOf(scenario).length > 1"
              type="button"
              class="text-ink-muted hover:text-critical"
              :aria-label="`Remove requirement ${ri + 1}`"
              @click="removeRequirement(scenario, clause, ri)"
            >
              ✕
            </button>
          </div>
          <div class="flex flex-wrap items-center justify-between gap-2">
            <button
              type="button"
              class="btn btn-sm"
              @click="clause.requirements.push(newRequirement(roles[0]))"
            >
              + And…
            </button>
            <label
              v-if="distinctMatters(clause)"
              class="flex items-center gap-1.5 text-xs text-ink-2"
            >
              <input
                v-model="clause.requireDistinctCards"
                type="checkbox"
                @change="
                  clausesOf(scenario).length === 1 &&
                  (scenario.requireDistinctCards = clause.requireDistinctCards)
                "
              />
              Each requirement needs <strong class="font-semibold">different cards</strong>
              <span class="text-ink-muted">(a card with two roles counts once)</span>
            </label>
          </div>
        </div>
      </template>

      <button
        type="button"
        class="btn btn-sm mt-2"
        :disabled="clausesOf(scenario).length >= 16"
        @click="addAlternative(scenario)"
      >
        + Or…
      </button>
    </div>

    <div class="flex flex-wrap gap-2">
      <button
        type="button"
        class="btn btn-sm"
        :disabled="scenarios.length >= 40"
        @click="addScenario"
      >
        + Scenario
      </button>
      <button type="button" class="btn btn-sm" @click="$emit('reset')">Reset to defaults</button>
    </div>
  </div>
</template>
