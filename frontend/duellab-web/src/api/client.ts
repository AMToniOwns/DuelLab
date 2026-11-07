import type {
  Card,
  CardKind,
  CatalogStatus,
  Comparison,
  DeckSummary,
  DeckZone,
  PagedResult,
  Reproduction,
  Scenario,
  SimulationConfig,
  SimulationRun,
  SimulationRunSummary,
  VersionDetail,
} from './types'

/** An error response from the API, carrying its stable problem code. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code: string | null,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  let response: Response
  try {
    response = await fetch(url, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError('Could not reach the DuelLab server. Is it running?', 0, 'network')
  }

  if (!response.ok) {
    let message = `Request failed (${response.status}).`
    let code: string | null = null
    try {
      const problem = (await response.json()) as { detail?: string; title?: string; code?: string }
      message = problem.detail ?? problem.title ?? message
      code = problem.code ?? null
    } catch {
      // Not a problem document; keep the generic message.
    }
    throw new ApiError(message, response.status, code)
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

const get = <T>(url: string) => request<T>('GET', url)
const post = <T>(url: string, body?: unknown) => request<T>('POST', url, body ?? {})
const put = <T>(url: string, body: unknown) => request<T>('PUT', url, body)
const del = (url: string) => request<void>('DELETE', url)

export interface CardSearch {
  search?: string
  kind?: CardKind | null
  page?: number
  pageSize?: number
}

export const api = {
  cards: {
    search: ({ search, kind, page = 1, pageSize = 24 }: CardSearch) => {
      const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
      if (search) query.set('search', search)
      if (kind) query.set('kind', kind)
      return get<PagedResult<Card>>(`/api/cards?${query}`)
    },
    get: (id: number) => get<Card>(`/api/cards/${id}`),
    status: () => get<CatalogStatus>('/api/cards/status'),
    sync: () => post<{ added: number; updated: number; total: number }>('/api/cards/sync'),
  },
  decks: {
    list: () => get<DeckSummary[]>('/api/decks'),
    get: (id: string) => get<DeckSummary>(`/api/decks/${id}`),
    create: (name: string) => post<DeckSummary>('/api/decks', { name }),
    rename: (id: string, name: string) => put<DeckSummary>(`/api/decks/${id}`, { name }),
    remove: (id: string) => del(`/api/decks/${id}`),
    createVersion: (deckId: string, versionName: string, sourceVersionId?: string | null) =>
      post<VersionDetail>(`/api/decks/${deckId}/versions`, {
        versionName,
        sourceVersionId: sourceVersionId ?? null,
      }),
  },
  versions: {
    get: (id: string) => get<VersionDetail>(`/api/versions/${id}`),
    update: (id: string, versionName: string, notes: string | null) =>
      put<VersionDetail>(`/api/versions/${id}`, { versionName, notes }),
    remove: (id: string) => del(`/api/versions/${id}`),
    saveCards: (id: string, cards: { cardId: number; zone: DeckZone; quantity: number }[]) =>
      put<VersionDetail>(`/api/versions/${id}/cards`, { cards }),
    saveRoles: (id: string, cards: { cardId: number; roles: string[] }[]) =>
      put<VersionDetail>(`/api/versions/${id}/roles`, { cards }),
    simulate: (id: string, config: SimulationConfig) =>
      post<SimulationRun>(`/api/versions/${id}/simulate`, config),
    history: (id: string, take = 20) =>
      get<SimulationRunSummary[]>(`/api/versions/${id}/simulations?take=${take}`),
    compare: (baseVersionId: string, candidateVersionId: string, configuration: SimulationConfig) =>
      post<Comparison>('/api/versions/compare', {
        baseVersionId,
        candidateVersionId,
        configuration,
      }),
  },
  simulations: {
    recent: (take = 10) => get<SimulationRunSummary[]>(`/api/simulations?take=${take}`),
    get: (id: string) => get<SimulationRun>(`/api/simulations/${id}`),
    reproduce: (id: string) => post<Reproduction>(`/api/simulations/${id}/reproduce`),
    defaultScenarios: () => get<Scenario[]>('/api/simulations/default-scenarios'),
  },
}

/** A user-facing message for any thrown value. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof Error) return error.message
  return 'Something went wrong.'
}
