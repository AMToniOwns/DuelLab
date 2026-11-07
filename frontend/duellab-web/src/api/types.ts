// TypeScript mirrors of the API contracts (DuelLab.Application DTOs). JSON uses camelCase and
// enums as strings.

export type CardKind = 'Monster' | 'Spell' | 'Trap'
export type BanStatus = 'Unlimited' | 'SemiLimited' | 'Limited' | 'Forbidden'
export type DeckZone = 'Main' | 'Extra' | 'Side'
export const DECK_ZONES: readonly DeckZone[] = ['Main', 'Extra', 'Side']

export interface Card {
  id: number
  externalId: number
  name: string
  description: string
  kind: CardKind
  typeLine: string
  isExtraDeckCard: boolean
  attribute: string | null
  race: string | null
  archetype: string | null
  level: number | null
  attack: number | null
  defense: number | null
  linkValue: number | null
  tcgBanStatus: BanStatus
  lastSyncedAt: string
  imageUrl: string
  smallImageUrl: string
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface CatalogStatus {
  cardCount: number
  lastSyncedAt: string | null
  provider: string
}

export interface VersionSummary {
  id: string
  versionName: string
  notes: string | null
  parentVersionId: string | null
  createdAt: string
  updatedAt: string
  mainCount: number
  extraCount: number
  sideCount: number
}

export interface DeckSummary {
  id: string
  name: string
  format: string
  createdAt: string
  updatedAt: string
  versions: VersionSummary[]
}

export interface ValidationIssue {
  severity: 'Error' | 'Warning'
  code: string
  message: string
  cardId: number | null
}

export interface Validation {
  isValid: boolean
  mainCount: number
  extraCount: number
  sideCount: number
  issues: ValidationIssue[]
}

export interface VersionCard {
  card: Card
  zone: DeckZone
  quantity: number
  roles: string[]
}

export interface VersionDetail {
  id: string
  deckId: string
  deckName: string
  versionName: string
  notes: string | null
  parentVersionId: string | null
  createdAt: string
  updatedAt: string
  cards: VersionCard[]
  validation: Validation
}

export interface Requirement {
  role: string
  minimumCount: number
  maximumCount?: number | null
}

export interface Scenario {
  name: string
  requirements: Requirement[]
  requireDistinctCards: boolean
}

export interface SimulationConfig {
  sampleCount: number
  openingHandSize: number
  seed?: number | null
  scenarios?: Scenario[] | null
  sampleHandCount?: number | null
}

export interface ConfidenceInterval {
  lower: number
  upper: number
  level: number
}

export interface ScenarioResult {
  name: string
  description: string
  successes: number
  samples: number
  estimate: number
  confidenceInterval: ConfidenceInterval
  exact: number | null
  estimateMinusExact: number | null
}

export interface CardFrequency {
  key: string
  name: string
  quantity: number
  handsWithCard: number
  estimate: number
  exact: number
}

export interface SampleHand {
  index: number
  cardKeys: string[]
  scenarioOutcomes: boolean[]
}

export interface AnalysisResult {
  engineVersion: string
  randomAlgorithm: string
  seed: number
  deckSize: number
  handSize: number
  sampleCount: number
  scenarios: ScenarioResult[]
  cardFrequencies: CardFrequency[]
  sampleHands: SampleHand[]
}

export interface SnapshotCard {
  cardId: number
  name: string
  quantity: number
  roles: string[]
}

export interface DeckSnapshot {
  deckId: string
  deckName: string
  versionId: string
  versionName: string
  mainDeck: SnapshotCard[]
}

export interface SimulationRun {
  id: string
  deckId: string
  deckVersionId: string
  deckName: string
  versionName: string
  createdAt: string
  engineVersion: string
  seed: number
  sampleCount: number
  openingHandSize: number
  configuration: SimulationConfig
  snapshot: DeckSnapshot
  result: AnalysisResult
  versionChangedSinceRun: boolean | null
}

export interface SimulationRunSummary {
  id: string
  deckId: string
  deckVersionId: string
  deckName: string
  versionName: string
  createdAt: string
  seed: number
  sampleCount: number
  openingHandSize: number
  scenarios: { name: string; estimate: number; exact: number | null }[]
}

export interface Reproduction {
  identical: boolean
  engineVersionMatches: boolean
  storedEngineVersion: string
  currentEngineVersion: string
  reproduced: AnalysisResult
}

export interface ScenarioComparison {
  name: string
  description: string
  baseExact: number | null
  candidateExact: number | null
  exactDifferencePp: number | null
  baseEstimate: number
  candidateEstimate: number
  estimateDifferencePp: number
}

export interface CardChange {
  cardId: number
  name: string
  zone: DeckZone
  baseQuantity: number
  candidateQuantity: number
  delta: number
}

export interface RoleChange {
  cardId: number
  name: string
  added: string[]
  removed: string[]
}

export interface Comparison {
  base: SimulationRun
  candidate: SimulationRun
  scenarios: ScenarioComparison[]
  cardChanges: CardChange[]
  roleChanges: RoleChange[]
}
