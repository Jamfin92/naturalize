import type {
  Applicant,
  CaseDetail,
  CaseStatus,
  DashboardMetrics,
  Decision,
  DecisionOutcome,
  NaturalizationCase,
  Paged,
} from './types'

const BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5099'

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  })

  if (!res.ok) {
    // The API returns RFC7807 problem details; surface `detail` when present.
    let message = `${res.status} ${res.statusText}`
    try {
      const problem = await res.json()
      if (problem?.detail) message = problem.detail
      else if (problem?.title) message = problem.title
    } catch {
      /* non-JSON error body — keep the status line */
    }
    throw new ApiError(res.status, message)
  }

  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

function query(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== '') search.set(k, String(v))
  }
  const s = search.toString()
  return s ? `?${s}` : ''
}

export const api = {
  health: () => request<{ status: string }>('/health'),

  metrics: () => request<DashboardMetrics>('/api/metrics'),

  applicants: {
    list: (params: { q?: string; page?: number; pageSize?: number } = {}) =>
      request<Paged<Applicant>>(`/api/applicants${query(params)}`),
    get: (id: number) => request<Applicant>(`/api/applicants/${id}`),
    create: (body: Omit<Applicant, 'id' | 'createdAt'>) =>
      request<Applicant>('/api/applicants', { method: 'POST', body: JSON.stringify(body) }),
    update: (id: number, body: Omit<Applicant, 'id' | 'createdAt'>) =>
      request<Applicant>(`/api/applicants/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
    remove: (id: number) => request<void>(`/api/applicants/${id}`, { method: 'DELETE' }),
    cases: (id: number) => request<NaturalizationCase[]>(`/api/applicants/${id}/cases`),
  },

  cases: {
    list: (params: { q?: string; status?: CaseStatus; page?: number; pageSize?: number } = {}) =>
      request<Paged<NaturalizationCase>>(`/api/cases${query(params)}`),
    get: (id: number) => request<CaseDetail>(`/api/cases/${id}`),
    create: (body: {
      applicantId: number
      receiptNumber: string
      filedOn: string
      fieldOffice: string
    }) => request<NaturalizationCase>('/api/cases', { method: 'POST', body: JSON.stringify(body) }),
    remove: (id: number) => request<void>(`/api/cases/${id}`, { method: 'DELETE' }),
    transition: (id: number, body: { status: CaseStatus; actor: string; notes?: string }) =>
      request<NaturalizationCase>(`/api/cases/${id}/status`, {
        method: 'POST',
        body: JSON.stringify(body),
      }),
  },

  decisions: {
    list: (params: { from?: string; to?: string; fieldOffice?: string } = {}) =>
      request<Decision[]>(`/api/decisions${query(params)}`),
    create: (body: {
      caseId: number
      outcome: DecisionOutcome
      decidedBy: string
      rationale: string
      denialReasonCode?: string
    }) => request<Decision>('/api/decisions', { method: 'POST', body: JSON.stringify(body) }),
    remove: (id: number) => request<void>(`/api/decisions/${id}`, { method: 'DELETE' }),
  },

  /** Report URLs are handed straight to the browser so the PDF streams as a download. */
  reports: {
    caseRecordUrl: (caseId: number) => `${BASE}/api/reports/case/${caseId}.pdf`,
    approvalsUrl: (params: { from?: string; to?: string; fieldOffice?: string } = {}) =>
      `${BASE}/api/reports/approvals.pdf${query(params)}`,
    pipelineUrl: () => `${BASE}/api/reports/pipeline.pdf`,
  },
}
