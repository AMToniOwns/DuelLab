import { ref, watch } from 'vue'
import { defineStore } from 'pinia'
import { api } from '@/api/client'
import type { Scenario, SimulationConfig } from '@/api/types'

const STORAGE_KEY = 'duellab-simulation-config'

interface Saved {
  sampleCount: number
  handSize: number
  seed: string
  scenarios: Scenario[] | null
}

function read(): Saved | null {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY) ?? 'null') as Saved | null
  } catch {
    return null
  }
}

/** The simulator's settings, remembered between visits (per browser). */
export const useSimulationConfigStore = defineStore('simulationConfig', () => {
  const saved = read()
  const sampleCount = ref(saved?.sampleCount ?? 10_000)
  const handSize = ref(saved?.handSize ?? 5)
  const seed = ref(saved?.seed ?? '')
  const scenarios = ref<Scenario[]>(saved?.scenarios ?? [])
  const defaults = ref<Scenario[]>([])

  async function ensureScenarios(): Promise<void> {
    if (defaults.value.length === 0) defaults.value = await api.simulations.defaultScenarios()
    if (scenarios.value.length === 0) resetScenarios()
  }

  function resetScenarios(): void {
    scenarios.value = structuredClone(
      defaults.value.map((s) => ({ ...s, requirements: s.requirements.map((r) => ({ ...r })) })),
    )
  }

  /** The request body for the API. An empty seed lets the server choose (and record) one. */
  function toConfig(): SimulationConfig {
    return {
      sampleCount: sampleCount.value,
      openingHandSize: handSize.value,
      seed: seed.value.trim() === '' ? null : Number(seed.value.trim()),
      scenarios: scenarios.value.map((s) => ({
        ...s,
        requirements: s.requirements.map((r) => ({ ...r, maximumCount: r.maximumCount ?? null })),
      })),
    }
  }

  watch(
    [sampleCount, handSize, seed, scenarios],
    () => {
      try {
        const value: Saved = {
          sampleCount: sampleCount.value,
          handSize: handSize.value,
          seed: seed.value,
          scenarios: scenarios.value,
        }
        localStorage.setItem(STORAGE_KEY, JSON.stringify(value))
      } catch {
        // Not critical: settings just won't be remembered.
      }
    },
    { deep: true },
  )

  return {
    sampleCount,
    handSize,
    seed,
    scenarios,
    defaults,
    ensureScenarios,
    resetScenarios,
    toConfig,
  }
})
