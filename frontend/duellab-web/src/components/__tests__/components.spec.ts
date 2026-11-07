import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ProbabilityChart from '../ProbabilityChart.vue'
import DumbbellChart from '../DumbbellChart.vue'
import ScenarioEditor from '../ScenarioEditor.vue'
import SimulationSettings from '../SimulationSettings.vue'
import ComboLineEditor from '../ComboLineEditor.vue'
import type { DraftComboLine } from '@/lib/deckDraft'
import type { Scenario } from '@/api/types'

describe('ProbabilityChart', () => {
  it('renders an accessible row per scenario with estimate, exact and interval', () => {
    const wrapper = mount(ProbabilityChart, {
      props: {
        rows: [
          {
            name: 'Starter',
            description: 'Hand contains at least 1 starter',
            estimate: 0.9044,
            lower: 0.9026,
            upper: 0.9062,
            exact: 0.9051,
            successes: 90_441,
            samples: 100_000,
          },
        ],
      },
    })

    const row = wrapper.get('[tabindex="0"]')
    expect(row.attributes('aria-label')).toBe(
      'Starter: estimate 90.4%, exact 90.51%, 95% interval 90.3% to 90.6%',
    )
    expect(wrapper.text()).toContain('90,441 of 100,000 hands')
  })
})

describe('DumbbellChart', () => {
  it('labels both versions and the signed change', () => {
    const wrapper = mount(DumbbellChart, {
      props: {
        baseLabel: 'v1',
        candidateLabel: 'v2',
        rows: [{ name: 'Brick', description: '', base: 0.3232, candidate: 0, deltaPp: -32.32 }],
      },
    })

    expect(wrapper.text()).toContain('v1 (base)')
    expect(wrapper.text()).toContain('v2 (candidate)')
    expect(wrapper.text()).toContain('−32.3 pp')
  })
})

describe('ScenarioEditor', () => {
  it('adds and removes scenarios and requirements', async () => {
    const scenarios: Scenario[] = [
      {
        name: 'Starter',
        requirements: [{ role: 'Starter', minimumCount: 1 }],
        requireDistinctCards: true,
      },
    ]
    const wrapper = mount(ScenarioEditor, {
      props: {
        modelValue: scenarios,
        roles: ['Starter', 'Extender'],
        'onUpdate:modelValue': () => {},
      },
    })

    await wrapper
      .findAll('button')
      .find((b) => b.text() === '+ And…')!
      .trigger('click')
    expect(scenarios[0]!.requirements).toHaveLength(2)
    // With two requirements the "different cards" option appears.
    expect(wrapper.text()).toContain('different cards')

    await wrapper
      .findAll('button')
      .find((b) => b.text() === '+ Scenario')!
      .trigger('click')
    expect(scenarios).toHaveLength(2)

    await wrapper.get('button[aria-label="Remove scenario Starter"]').trigger('click')
    expect(scenarios.map((s) => s.name)).toEqual(['Scenario 2'])
  })
})

describe('SimulationSettings', () => {
  it('rejects seeds that a JavaScript number cannot hold exactly', async () => {
    const wrapper = mount(SimulationSettings, {
      props: { sampleCount: 1000, handSize: 5, seed: '9007199254740992' },
    })

    expect(wrapper.text()).toContain('Use a whole number from 0 to 9,007,199,254,740,991.')

    await wrapper.setProps({ seed: '12345' })
    expect(wrapper.text()).toContain('The same seed and deck always give the same hands.')
  })
})

describe('ScenarioEditor alternatives and card targets', () => {
  it('turns a scenario into OR alternatives and back', async () => {
    const scenarios: Scenario[] = [
      {
        name: 'S',
        requirements: [{ role: 'Starter', minimumCount: 1 }],
        requireDistinctCards: true,
      },
    ]
    const wrapper = mount(ScenarioEditor, {
      props: {
        modelValue: scenarios,
        roles: ['Starter', 'Extender'],
        'onUpdate:modelValue': () => {},
      },
    })

    await wrapper
      .findAll('button')
      .find((b) => b.text() === '+ Or…')!
      .trigger('click')
    expect(scenarios[0]!.anyOf).toHaveLength(2)
    expect(scenarios[0]!.requirements).toBeNull()
    expect(wrapper.text()).toContain('Either')

    await wrapper.findAll('button[aria-label="Remove requirement 1"]')[1]!.trigger('click')
    expect(scenarios[0]!.anyOf).toBeNull()
    expect(scenarios[0]!.requirements).toEqual([{ role: 'Starter', minimumCount: 1 }])
  })

  it('adds a specific card to a requirement', async () => {
    const scenarios: Scenario[] = [
      {
        name: 'S',
        requirements: [{ role: 'Starter', minimumCount: 1 }],
        requireDistinctCards: true,
      },
    ]
    const wrapper = mount(ScenarioEditor, {
      props: {
        modelValue: scenarios,
        roles: ['Starter'],
        cards: [{ id: 7, name: 'Snake-Eye Ash', inMain: true }],
        'onUpdate:modelValue': () => {},
      },
    })

    await wrapper.get('[data-testid="add-target-target-0-0-0"]').setValue('card:7')

    expect(scenarios[0]!.requirements![0]).toMatchObject({
      role: null,
      roles: ['Starter'],
      cardIds: [7],
    })
    expect(wrapper.text()).toContain('Snake-Eye Ash')
  })
})

describe('ComboLineEditor', () => {
  it('adds lines and pieces and flags empty pieces', async () => {
    const lines: DraftComboLine[] = []
    const wrapper = mount(ComboLineEditor, {
      props: {
        modelValue: lines,
        roles: ['Starter'],
        cards: [{ id: 1, name: 'Ash', inMain: false }],
        'onUpdate:modelValue': () => {},
      },
    })

    await wrapper.get('[data-testid="add-combo-line"]').trigger('click')
    expect(lines).toHaveLength(1)
    expect(wrapper.text()).toContain('Choose at least one card or role.')

    await wrapper.get('[data-testid="add-target-piece-0-0"]').setValue('card:1')
    expect(lines[0]!.pieces[0]!.cardIds).toEqual([1])
    // Not in the Main Deck: marked so the user knows it can never be drawn.
    expect(wrapper.find('[aria-label="not in the Main Deck"]').exists()).toBe(true)
    expect(wrapper.emitted('changed')!.length).toBeGreaterThan(0)
  })
})
