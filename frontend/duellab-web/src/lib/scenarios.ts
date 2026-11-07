import type { Clause, Requirement, Scenario } from '@/api/types'

/**
 * Helpers for the two scenario shapes: a single list of requirements (AND), or alternatives (OR of
 * ANDs). The editor always works on clauses and writes back the simplest shape.
 */

/** A scenario's alternatives; a plain scenario is one alternative. */
export function clausesOf(scenario: Scenario): Clause[] {
  if (scenario.anyOf && scenario.anyOf.length > 0) return scenario.anyOf
  return [
    {
      requirements: scenario.requirements ?? [],
      requireDistinctCards: scenario.requireDistinctCards,
    },
  ]
}

/** Writes alternatives back: one becomes a plain scenario, several become `anyOf`. */
export function setClauses(scenario: Scenario, clauses: Clause[]): void {
  if (clauses.length === 1) {
    scenario.requirements = clauses[0]!.requirements
    scenario.requireDistinctCards = clauses[0]!.requireDistinctCards
    scenario.anyOf = null
  } else {
    scenario.anyOf = clauses
    scenario.requirements = null
  }
}

/** Adds an OR alternative with one starter requirement. */
export function addAlternative(scenario: Scenario): void {
  setClauses(scenario, [
    ...clausesOf(scenario),
    { requirements: [newRequirement()], requireDistinctCards: true },
  ])
}

/** Removes an alternative (the last one cannot be removed). */
export function removeAlternative(scenario: Scenario, index: number): void {
  const clauses = clausesOf(scenario)
  if (clauses.length <= 1) return
  setClauses(
    scenario,
    clauses.filter((_, i) => i !== index),
  )
}

export function newRequirement(role = 'Starter'): Requirement {
  return { role, roles: null, cardIds: null, minimumCount: 1, maximumCount: null }
}

/** What a requirement looks for: role keys and card ids. */
export function targetsOf(requirement: Requirement): { roles: string[]; cardIds: number[] } {
  const roles = [...(requirement.role ? [requirement.role] : []), ...(requirement.roles ?? [])]
  return { roles: [...new Set(roles)], cardIds: [...new Set(requirement.cardIds ?? [])] }
}

/** Sets what a requirement looks for, keeping the simple `role` form when it is a single role. */
export function setTargets(requirement: Requirement, roles: string[], cardIds: number[]): void {
  if (roles.length === 1 && cardIds.length === 0) {
    requirement.role = roles[0]
    requirement.roles = null
    requirement.cardIds = null
  } else {
    requirement.role = null
    requirement.roles = roles.length ? roles : null
    requirement.cardIds = cardIds.length ? cardIds : null
  }
}

/** True when the distinct-cards choice matters for an alternative (two or more minimums). */
export function distinctMatters(clause: Clause): boolean {
  return clause.requirements.filter((r) => r.minimumCount > 0).length > 1
}

/** A deep copy that is safe to edit. */
export function cloneScenario(scenario: Scenario): Scenario {
  return JSON.parse(JSON.stringify(scenario)) as Scenario
}
