<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, errorMessage } from '@/api/client'
import type { VersionDetail } from '@/api/types'
import { availableRoles } from '@/lib/roles'
import ScenarioEditor from '@/components/ScenarioEditor.vue'
import SimulationSettings from '@/components/SimulationSettings.vue'
import VersionPicker from '@/components/VersionPicker.vue'
import { useDeckEditorStore } from '@/stores/deckEditor'
import { useSimulationConfigStore } from '@/stores/simulationConfig'
import { useToastStore } from '@/stores/toasts'

const route = useRoute()
const router = useRouter()
const config = useSimulationConfigStore()
const editor = useDeckEditorStore()
const toasts = useToastStore()

const versionId = ref<string | null>(
  typeof route.query.versionId === 'string' ? route.query.versionId : null,
)
const version = ref<VersionDetail | null>(null)
const running = ref(false)
const settings = ref<InstanceType<typeof SimulationSettings> | null>(null)

watch(
  versionId,
  async (id) => {
    version.value = null
    if (!id) return
    await router.replace({ query: { versionId: id } })
    try {
      version.value = await api.versions.get(id)
    } catch (e) {
      toasts.error(errorMessage(e))
    }
  },
  { immediate: true },
)

onMounted(() => config.ensureScenarios().catch((e: unknown) => toasts.error(errorMessage(e))))

const roleCounts = computed(() => {
  const counts: Record<string, number> = {}
  for (const entry of version.value?.cards ?? []) {
    if (entry.zone !== 'Main') continue
    for (const role of entry.roles) counts[role] = (counts[role] ?? 0) + entry.quantity
  }
  return counts
})
const roles = computed(() => availableRoles(Object.keys(roleCounts.value)))
const errors = computed(
  () => version.value?.validation.issues.filter((i) => i.severity === 'Error') ?? [],
)
const canRun = computed(
  () =>
    !!version.value &&
    version.value.validation.isValid &&
    !running.value &&
    !settings.value?.seedError,
)

async function run(): Promise<void> {
  if (!versionId.value) return
  running.value = true
  try {
    const result = await api.versions.simulate(versionId.value, config.toConfig())
    await router.push({ name: 'run', params: { runId: result.id } })
  } catch (e) {
    toasts.error(errorMessage(e))
  } finally {
    running.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <div>
      <h1 class="text-2xl font-semibold tracking-tight">Simulator</h1>
      <p class="text-sm text-ink-2">
        Draws opening hands at random from the Main Deck, without replacement, and counts how often
        each scenario holds. Exact probabilities are calculated alongside the simulation.
      </p>
    </div>

    <section class="card space-y-4 p-4">
      <VersionPicker v-model="versionId" id-prefix="sim" />
      <div
        v-if="versionId && editor.isDirty(versionId)"
        class="rounded-lg bg-warning-soft p-2 text-sm"
        role="status"
      >
        This version has unsaved edits. The simulation uses the last saved version.
        <RouterLink
          :to="{ name: 'editor', params: { versionId } }"
          class="font-medium text-accent underline"
          >Open the editor to save</RouterLink
        >.
      </div>
      <div
        v-if="version && errors.length"
        class="rounded-lg bg-critical-soft p-2 text-sm"
        role="alert"
      >
        <p class="font-medium">This version cannot be simulated yet:</p>
        <ul class="list-inside list-disc">
          <li v-for="(e, i) in errors" :key="i">{{ e.message }}</li>
        </ul>
        <RouterLink
          :to="{ name: 'editor', params: { versionId: version.id } }"
          class="font-medium text-accent underline"
          >Fix it in the editor</RouterLink
        >
      </div>
      <p v-if="version && Object.keys(roleCounts).length === 0" class="text-sm text-ink-2">
        No cards in this version have roles yet, so role scenarios will all be 0%.
        <RouterLink
          :to="{ name: 'editor', params: { versionId: version.id } }"
          class="text-accent underline"
          >Assign roles</RouterLink
        >
        first.
      </p>
      <SimulationSettings
        ref="settings"
        v-model:sample-count="config.sampleCount"
        v-model:hand-size="config.handSize"
        v-model:seed="config.seed"
      />
    </section>

    <section class="card p-4" aria-labelledby="scenarios-heading">
      <h2 id="scenarios-heading" class="mb-1 font-semibold">Scenarios</h2>
      <p class="mb-3 text-sm text-ink-2">
        Each scenario is met when every one of its requirements holds.
      </p>
      <ScenarioEditor
        v-model="config.scenarios"
        :roles="roles"
        :role-counts="roleCounts"
        @reset="config.resetScenarios()"
      />
    </section>

    <div class="flex justify-end">
      <button
        type="button"
        class="btn btn-primary px-6 py-2!"
        :disabled="!canRun"
        data-testid="run-simulation"
        @click="run"
      >
        {{ running ? 'Simulating…' : `Simulate ${config.sampleCount.toLocaleString()} hands` }}
      </button>
    </div>
  </div>
</template>
