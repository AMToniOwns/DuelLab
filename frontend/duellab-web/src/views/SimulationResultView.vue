<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { api, errorMessage } from '@/api/client'
import type { Reproduction, SimulationRun } from '@/api/types'
import { count, dateTime, percent, pointDelta } from '@/lib/format'
import { roleLabel } from '@/lib/roles'
import AsyncState from '@/components/AsyncState.vue'
import FrequencyChart from '@/components/FrequencyChart.vue'
import ProbabilityChart from '@/components/ProbabilityChart.vue'
import SampleHands from '@/components/SampleHands.vue'
import { useToastStore } from '@/stores/toasts'

const props = defineProps<{ runId: string }>()
const toasts = useToastStore()
const run = ref<SimulationRun | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const reproduction = ref<Reproduction | null>(null)
const reproducing = ref(false)
const handsShown = ref(5)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  reproduction.value = null
  try {
    run.value = await api.simulations.get(props.runId)
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}
watch(() => props.runId, load, { immediate: true })

async function reproduce(): Promise<void> {
  reproducing.value = true
  try {
    reproduction.value = await api.simulations.reproduce(props.runId)
  } catch (e) {
    toasts.error(errorMessage(e))
  } finally {
    reproducing.value = false
  }
}

const rows = computed(() =>
  (run.value?.result.scenarios ?? []).map((s) => ({
    name: s.name,
    description: s.description,
    estimate: s.estimate,
    lower: s.confidenceInterval.lower,
    upper: s.confidenceInterval.upper,
    exact: s.exact,
    successes: s.successes,
    samples: s.samples,
  })),
)

const frequencies = computed(() =>
  (run.value?.result.cardFrequencies ?? []).map((f) => ({
    ...f,
    samples: run.value!.result.sampleCount,
  })),
)

const roleTotals = computed(() => {
  const totals: Record<string, number> = {}
  for (const card of run.value?.snapshot.mainDeck ?? [])
    for (const role of card.roles) totals[role] = (totals[role] ?? 0) + card.quantity
  return Object.entries(totals).sort((a, b) => b[1] - a[1])
})
</script>

<template>
  <AsyncState :loading="loading" :error="error" @retry="load">
    <div v-if="run" class="space-y-6">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <RouterLink
            :to="{ name: 'deck', params: { deckId: run.deckId } }"
            class="text-sm text-accent"
            >← {{ run.deckName }}</RouterLink
          >
          <h1 class="mt-1 text-2xl font-semibold tracking-tight">
            {{ run.deckName }} · {{ run.versionName }}
          </h1>
          <p class="num text-sm text-ink-2">
            {{ count(run.sampleCount) }} hands of {{ run.openingHandSize }} from
            {{ run.result.deckSize }} cards · seed
            <code class="rounded bg-surface-2 px-1">{{ run.seed }}</code> ·
            {{ dateTime(run.createdAt) }}
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <button
            type="button"
            class="btn"
            :disabled="reproducing"
            data-testid="reproduce"
            @click="reproduce"
          >
            {{ reproducing ? 'Re-running…' : 'Reproduce' }}
          </button>
          <RouterLink
            :to="{ name: 'simulate', query: { versionId: run.deckVersionId } }"
            class="btn"
            >Run again</RouterLink
          >
          <RouterLink :to="{ name: 'compare', query: { base: run.deckVersionId } }" class="btn"
            >Compare with…</RouterLink
          >
        </div>
      </div>

      <div
        v-if="run.versionChangedSinceRun"
        class="rounded-lg bg-warning-soft p-3 text-sm"
        role="status"
      >
        This version's Main Deck or roles have changed since this run. These results describe the
        deck as it was then (shown below under "Deck analysed").
      </div>
      <div
        v-if="reproduction"
        class="rounded-lg p-3 text-sm"
        :class="reproduction.identical ? 'bg-accent-soft' : 'bg-critical-soft'"
        role="status"
        data-testid="reproduction-result"
      >
        <template v-if="reproduction.identical"
          >✓ Reproduced: re-running the saved configuration gave identical results.</template
        >
        <template v-else-if="!reproduction.engineVersionMatches">
          ✕ Results differ because the simulation engine changed (run made with
          {{ reproduction.storedEngineVersion }}, now {{ reproduction.currentEngineVersion }}).
        </template>
        <template v-else
          >✕ Re-running gave different results. Please report this: it should not happen.</template
        >
      </div>

      <section class="card p-4" aria-labelledby="scenarios-heading">
        <h2 id="scenarios-heading" class="mb-3 font-semibold">How often each scenario holds</h2>
        <ProbabilityChart :rows="rows" />

        <div class="mt-5 overflow-x-auto">
          <table class="num w-full min-w-[40rem] text-sm">
            <caption class="sr-only">
              Scenario results
            </caption>
            <thead class="text-left text-xs text-ink-2">
              <tr class="border-b border-line">
                <th class="py-1.5 pr-3 font-medium">Scenario</th>
                <th class="py-1.5 pr-3 text-right font-medium">Hands</th>
                <th class="py-1.5 pr-3 text-right font-medium">Simulated</th>
                <th class="py-1.5 pr-3 text-right font-medium">95% interval</th>
                <th class="py-1.5 pr-3 text-right font-medium">Exact</th>
                <th class="py-1.5 text-right font-medium">Simulated − exact</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="s in run.result.scenarios"
                :key="s.name"
                class="border-b border-line align-top last:border-0"
              >
                <td class="py-1.5 pr-3">
                  <span class="font-medium">{{ s.name }}</span>
                  <span class="block text-xs text-ink-muted">{{ s.description }}</span>
                </td>
                <td class="py-1.5 pr-3 text-right">
                  {{ count(s.successes) }} / {{ count(s.samples) }}
                </td>
                <td class="py-1.5 pr-3 text-right">{{ percent(s.estimate, 2) }}</td>
                <td class="py-1.5 pr-3 text-right text-ink-2">
                  {{ percent(s.confidenceInterval.lower, 2) }} –
                  {{ percent(s.confidenceInterval.upper, 2) }}
                </td>
                <td class="py-1.5 pr-3 text-right font-semibold">
                  {{ s.exact === null ? 'not computed' : percent(s.exact, 2) }}
                </td>
                <td class="py-1.5 text-right text-ink-2">
                  {{
                    s.estimateMinusExact === null ? '—' : pointDelta(s.estimateMinusExact * 100, 2)
                  }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <div class="grid gap-6 xl:grid-cols-2">
        <section class="card p-4" aria-labelledby="freq-heading">
          <h2 id="freq-heading" class="font-semibold">Chance to open each card</h2>
          <p class="mb-3 text-xs text-ink-2">
            Exact probability of at least one copy in the opening hand. The 15 most likely cards are
            shown.
          </p>
          <FrequencyChart :rows="frequencies" />
        </section>

        <section class="card p-4" aria-labelledby="hands-heading">
          <h2 id="hands-heading" class="mb-3 font-semibold">Sample hands</h2>
          <SampleHands
            :hands="run.result.sampleHands.slice(0, handsShown)"
            :scenario-names="run.result.scenarios.map((s) => s.name)"
            :cards="run.snapshot.mainDeck"
          />
          <button
            v-if="handsShown < run.result.sampleHands.length"
            type="button"
            class="btn btn-sm mt-3"
            @click="handsShown += 5"
          >
            Show more hands
          </button>
        </section>
      </div>

      <details class="card p-4">
        <summary class="cursor-pointer font-semibold">
          Deck analysed and reproducibility details
        </summary>
        <div class="mt-3 grid gap-4 md:grid-cols-2">
          <div>
            <h3 class="mb-1 text-sm font-medium">Main Deck at the time of the run</h3>
            <ul class="num text-sm">
              <li
                v-for="c in run.snapshot.mainDeck"
                :key="c.cardId"
                class="flex justify-between gap-2 border-b border-line py-0.5 last:border-0"
              >
                <span>{{ c.quantity }}× {{ c.name }}</span>
                <span class="text-xs text-ink-muted">{{ c.roles.map(roleLabel).join(', ') }}</span>
              </li>
            </ul>
            <p class="mt-2 text-xs text-ink-2">
              Role totals:
              <span v-for="([role, n], i) in roleTotals" :key="role"
                ><span v-if="i">, </span>{{ roleLabel(role) }} {{ n }}</span
              >
            </p>
          </div>
          <dl class="num grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
            <dt class="text-ink-2">Format</dt>
            <dd>
              {{ run.snapshot.format ?? 'TCG Advanced'
              }}<template v-if="run.snapshot.banlist"> ({{ run.snapshot.banlist }})</template>
            </dd>
            <dt class="text-ink-2">Engine</dt>
            <dd>{{ run.engineVersion }}</dd>
            <dt class="text-ink-2">Random generator</dt>
            <dd>{{ run.result.randomAlgorithm }}</dd>
            <dt class="text-ink-2">Seed</dt>
            <dd>{{ run.seed }}</dd>
            <dt class="text-ink-2">Hands / hand size</dt>
            <dd>{{ count(run.sampleCount) }} / {{ run.openingHandSize }}</dd>
            <dt class="text-ink-2">Run id</dt>
            <dd class="break-all">{{ run.id }}</dd>
          </dl>
        </div>
      </details>

      <details class="card p-4">
        <summary class="cursor-pointer font-semibold">How to read these numbers</summary>
        <div class="mt-3 space-y-2 text-sm text-ink-2">
          <p>
            <strong class="text-ink">Exact</strong> is the true probability, calculated from the
            deck list with the hypergeometric distribution (draws without replacement). It does not
            depend on the seed.
          </p>
          <p>
            <strong class="text-ink">Simulated</strong> is the share of random hands that met the
            scenario. It varies slightly from run to run; the 95% interval shows how far it can
            plausibly be from the true value. More hands give a narrower interval.
          </p>
          <p>
            <strong class="text-ink">Different cards:</strong> unless a scenario says otherwise,
            each requirement must be met by a different card. A card labelled both Starter and
            Extender counts as one or the other, not both.
          </p>
          <p>
            Roles are your own labels. A hand that meets "starter + extender" contains those labels;
            it does not prove the hand can complete a combo.
          </p>
        </div>
      </details>
    </div>
  </AsyncState>
</template>
