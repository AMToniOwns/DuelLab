<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, errorMessage } from '@/api/client'
import type { Comparison, DeckSummary, VersionDetail } from '@/api/types'
import { percent, pointDelta } from '@/lib/format'
import { roleLabel } from '@/lib/roles'
import DumbbellChart from '@/components/DumbbellChart.vue'
import ScenarioEditor from '@/components/ScenarioEditor.vue'
import SimulationSettings from '@/components/SimulationSettings.vue'
import VersionPicker from '@/components/VersionPicker.vue'
import { useSimulationConfigStore } from '@/stores/simulationConfig'
import { useToastStore } from '@/stores/toasts'
import { availableRoles } from '@/lib/roles'

const route = useRoute()
const router = useRouter()
const config = useSimulationConfigStore()
const toasts = useToastStore()

const q = (key: string) =>
  typeof route.query[key] === 'string' ? (route.query[key] as string) : null
const base = ref<string | null>(q('base'))
const candidate = ref<string | null>(q('candidate'))
const decks = ref<DeckSummary[] | null>(null)
const comparison = ref<Comparison | null>(null)
const running = ref(false)
const settings = ref<InstanceType<typeof SimulationSettings> | null>(null)
const baseVersion = ref<VersionDetail | null>(null)
const baseCards = computed(() =>
  (baseVersion.value?.cards ?? [])
    .filter((c) => c.zone === 'Main')
    .map((c) => ({ id: c.card.id, name: c.card.name, inMain: true })),
)
watch(
  base,
  async (id) => {
    baseVersion.value = null
    if (id) baseVersion.value = await api.versions.get(id).catch(() => null)
  },
  { immediate: true },
)

onMounted(async () => {
  try {
    decks.value = await api.decks.list()
    await config.ensureScenarios()
  } catch (e) {
    toasts.error(errorMessage(e))
  }
})

watch([base, candidate], () => {
  comparison.value = null
  void router.replace({
    query: {
      ...(base.value ? { base: base.value } : {}),
      ...(candidate.value ? { candidate: candidate.value } : {}),
    },
  })
})

const canRun = computed(
  () =>
    !!base.value &&
    !!candidate.value &&
    base.value !== candidate.value &&
    !running.value &&
    !settings.value?.seedError,
)

async function compare(): Promise<void> {
  if (!base.value || !candidate.value) return
  running.value = true
  try {
    comparison.value = await api.versions.compare(base.value, candidate.value, config.toConfig())
  } catch (e) {
    toasts.error(errorMessage(e))
  } finally {
    running.value = false
  }
}

const label = (run: Comparison['base']) =>
  run.deckId === comparison.value?.candidate.deckId && run.deckId === comparison.value?.base.deckId
    ? run.versionName
    : `${run.deckName} · ${run.versionName}`

const rows = computed(() =>
  (comparison.value?.scenarios ?? []).map((s) => ({
    name: s.name,
    description: s.description,
    base: s.baseExact ?? s.baseEstimate,
    candidate: s.candidateExact ?? s.candidateEstimate,
    deltaPp: s.exactDifferencePp ?? s.estimateDifferencePp,
  })),
)
</script>

<template>
  <div class="space-y-4">
    <div>
      <h1 class="text-2xl font-semibold tracking-tight">Compare versions</h1>
      <p class="text-sm text-ink-2">
        Runs the same settings and the same seed on two versions, then shows what changed.
      </p>
    </div>

    <section class="card space-y-4 p-4">
      <div class="grid gap-4 md:grid-cols-2">
        <VersionPicker v-model="base" :decks="decks" label="Base version" id-prefix="base" />
        <VersionPicker
          v-model="candidate"
          :decks="decks"
          label="Candidate version"
          id-prefix="candidate"
        />
      </div>
      <p v-if="base && base === candidate" class="text-sm text-critical">
        Choose two different versions.
      </p>
      <SimulationSettings
        ref="settings"
        v-model:sample-count="config.sampleCount"
        v-model:hand-size="config.handSize"
        v-model:seed="config.seed"
      />
      <details>
        <summary class="cursor-pointer text-sm font-medium">
          Scenarios ({{ config.scenarios.length }})
        </summary>
        <div class="mt-3">
          <ScenarioEditor
            v-model="config.scenarios"
            :roles="availableRoles([])"
            :cards="baseCards"
            @reset="config.resetScenarios()"
          />
        </div>
      </details>
      <label class="flex items-center gap-2 text-sm">
        <input v-model="config.includeComboLines" type="checkbox" />
        Also measure combo lines
        <span class="text-ink-muted">(the base version's, plus any only the candidate has)</span>
      </label>
      <div class="flex justify-end">
        <button
          type="button"
          class="btn btn-primary"
          :disabled="!canRun"
          data-testid="run-compare"
          @click="compare"
        >
          {{ running ? 'Comparing…' : 'Compare' }}
        </button>
      </div>
    </section>

    <template v-if="comparison">
      <section class="card p-4" aria-labelledby="cmp-heading">
        <h2 id="cmp-heading" class="mb-1 font-semibold">Scenario probabilities</h2>
        <p class="mb-3 text-xs text-ink-2">
          Exact values where available. Changes are in percentage points (candidate − base).
        </p>
        <DumbbellChart
          :rows="rows"
          :base-label="label(comparison.base)"
          :candidate-label="label(comparison.candidate)"
        />

        <div class="mt-5 overflow-x-auto">
          <table class="num w-full min-w-[40rem] text-sm" data-testid="comparison-table">
            <caption class="sr-only">
              Comparison results
            </caption>
            <thead class="text-left text-xs text-ink-2">
              <tr class="border-b border-line">
                <th class="py-1.5 pr-3 font-medium">Scenario</th>
                <th class="py-1.5 pr-3 text-right font-medium">Base exact</th>
                <th class="py-1.5 pr-3 text-right font-medium">Candidate exact</th>
                <th class="py-1.5 pr-3 text-right font-medium">Change</th>
                <th class="py-1.5 pr-3 text-right font-medium">Simulated base → candidate</th>
                <th class="py-1.5 text-right font-medium">Simulated change</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="s in comparison.scenarios"
                :key="s.name"
                class="border-b border-line last:border-0"
              >
                <td class="py-1.5 pr-3 font-medium">{{ s.name }}</td>
                <td class="py-1.5 pr-3 text-right">{{ percent(s.baseExact, 2) }}</td>
                <td class="py-1.5 pr-3 text-right">{{ percent(s.candidateExact, 2) }}</td>
                <td class="py-1.5 pr-3 text-right font-semibold">
                  {{ pointDelta(s.exactDifferencePp, 2) }}
                </td>
                <td class="py-1.5 pr-3 text-right text-ink-2">
                  {{ percent(s.baseEstimate, 2) }} → {{ percent(s.candidateEstimate, 2) }}
                </td>
                <td class="py-1.5 text-right text-ink-2">
                  {{ pointDelta(s.estimateDifferencePp, 2) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <div class="grid gap-4 md:grid-cols-2">
        <section class="card p-4" aria-labelledby="changes-heading">
          <h2 id="changes-heading" class="mb-2 font-semibold">Card changes</h2>
          <p v-if="comparison.cardChanges.length === 0" class="text-sm text-ink-muted">
            Same cards in both versions.
          </p>
          <ul class="num space-y-1 text-sm" data-testid="card-changes">
            <li
              v-for="c in comparison.cardChanges"
              :key="`${c.cardId}-${c.zone}`"
              class="flex justify-between gap-2"
            >
              <span
                >{{ c.name }} <span class="text-xs text-ink-muted">({{ c.zone }})</span></span
              >
              <span
                ><span class="font-semibold"
                  >{{ c.delta > 0 ? '+' : '−' }}{{ Math.abs(c.delta) }}</span
                >
                <span class="text-ink-muted"
                  >{{ c.baseQuantity }} → {{ c.candidateQuantity }}</span
                ></span
              >
            </li>
          </ul>
          <template v-if="comparison.comboLineChanges.length">
            <h3 class="mt-3 mb-1 text-sm font-semibold">Combo lines</h3>
            <ul class="space-y-0.5 text-sm" data-testid="combo-changes">
              <li v-for="c in comparison.comboLineChanges" :key="c.name">
                {{ c.name }}: <span class="text-ink-2">{{ c.change }}</span>
              </li>
            </ul>
          </template>
        </section>
        <section class="card p-4" aria-labelledby="role-changes-heading">
          <h2 id="role-changes-heading" class="mb-2 font-semibold">Role and combo changes</h2>
          <p v-if="comparison.roleChanges.length === 0" class="text-sm text-ink-muted">
            Same roles in both versions.
          </p>
          <ul class="space-y-1 text-sm">
            <li v-for="c in comparison.roleChanges" :key="c.cardId">
              {{ c.name }}:
              <span v-for="r in c.added" :key="`a${r}`" class="ml-1">+{{ roleLabel(r) }}</span>
              <span v-for="r in c.removed" :key="`r${r}`" class="ml-1 text-ink-muted"
                >−{{ roleLabel(r) }}</span
              >
            </li>
          </ul>
        </section>
      </div>

      <p class="text-sm text-ink-2">
        Both runs are saved:
        <RouterLink
          :to="{ name: 'run', params: { runId: comparison.base.id } }"
          class="text-accent underline"
          >base run</RouterLink
        >
        ·
        <RouterLink
          :to="{ name: 'run', params: { runId: comparison.candidate.id } }"
          class="text-accent underline"
          >candidate run</RouterLink
        >
        (seed {{ comparison.base.seed }}).
      </p>
    </template>
  </div>
</template>
