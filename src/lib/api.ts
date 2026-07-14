import { clearToken, getToken } from './token'
import type {
  Applicant,
  AuditEvent,
  CaseDetail,
  CaseStatus,
  DashboardMetrics,
  Decision,
  DecisionOutcome,
  NaturalizationCase,
  Officer,
  Paged,
} from './types'

const BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5099'

export class ApiError extends Error {
  status: number

  /**
   * Per-field messages from an RFC7807 ValidationProblem, keyed by field name
   * (`{ alienNumber: ["A-Number is required."] }`). Carried through so a form can
   * put each message under the input that caused it, instead of flattening a
   * dozen field errors into one toast that says "400".
   */
  fields: Record<string, string[]>

  constructor(status: number, message: string, fields: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fields = fields
  }
}

/*
 * What to do when the API says 401.
 *
 * A token expires mid-session — eight hours in, mid-form — and every subsequent
 * request fails. Without a handler here the user sees a wall of red panels and no
 * explanation. AuthProvider registers a callback that drops the token and returns
 * them to the sign-in page.
 *
 * It is a registered callback rather than a direct import because this module
 * must not depend on React or the router.
 */
let onUnauthorized: () => void = () => {}

export function setUnauthorizedHandler(fn: () => void): void {
  onUnauthorized = fn
}

/** Attach the bearer token. The single choke point every API call passes through. */
function authHeaders(init?: RequestInit): HeadersInit {
  const token = getToken()
  return {
    ...init?.headers,
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  }
}

async function failure(res: Response): Promise<ApiError> {
  // The API returns RFC7807 problem details; surface `detail` when present.
  let message = `${res.status} ${res.statusText}`
  let fields: Record<string, string[]> = {}

  try {
    const problem = await res.json()
    if (typeof problem === 'string') {
      // TypedResults.Conflict(string) — e.g. the withdrawn-A-Number message.
      message = problem
    } else {
      if (problem?.detail) message = problem.detail
      else if (problem?.title) message = problem.title
      if (problem?.errors && typeof problem.errors === 'object') fields = problem.errors
    }
  } catch {
    /* non-JSON error body — keep the status line */
  }

  if (res.status === 401) {
    clearToken()
    onUnauthorized()
  }

  return new ApiError(res.status, message, fields)
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...authHeaders(init),
    },
  })

  if (!res.ok) throw await failure(res)

  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

/*
 * Reports: fetch the bytes, then save them.
 *
 * These used to be plain <a href> links, which was the right call while the API
 * was open — the browser navigated, Content-Disposition did the rest, and no
 * megabytes were shovelled through JS to achieve it. Authentication ended that.
 * A browser NAVIGATION cannot carry an Authorization header, so the moment the
 * reports group required a token, every one of those links became a blank tab
 * containing 401 JSON.
 *
 * The alternative — putting the token in the query string to keep the <a href> —
 * would leak a full-lifetime bearer into browser history, the Referer header and
 * every proxy log between here and the API. Buffering a 60 KB PDF is cheaper than
 * that.
 */
async function download(path: string, fallbackName: string): Promise<void> {
  const res = await fetch(`${BASE}${path}`, { headers: authHeaders() })
  if (!res.ok) throw await failure(res)

  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filenameFrom(res) ?? fallbackName
  document.body.appendChild(a)
  a.click()
  a.remove()

  // Revoking immediately can cancel the download in some browsers; a tick is enough.
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

/**
 * The server names each report (`pipeline-20260714.pdf`). Reading that name
 * cross-origin only works because the API sends
 * `Access-Control-Expose-Headers: Content-Disposition` — without it this returns
 * null and every download lands as "report.pdf".
 */
function filenameFrom(res: Response): string | null {
  const header = res.headers.get('Content-Disposition')
  if (!header) return null
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(header)
  return match ? decodeURIComponent(match[1]) : null
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

  auth: {
    login: (email: string, password: string) =>
      request<{ accessToken: string; expiresAt: string; officer: Officer }>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      }),
    me: () => request<Officer>('/api/auth/me'),
  },

  metrics: () => request<DashboardMetrics>('/api/metrics'),

  applicants: {
    list: (params: { q?: string; page?: number; pageSize?: number } = {}) =>
      request<Paged<Applicant>>(`/api/applicants${query(params)}`),
    get: (id: number) => request<Applicant>(`/api/applicants/${id}`),
    create: (body: ApplicantInput) =>
      request<Applicant>('/api/applicants', { method: 'POST', body: JSON.stringify(body) }),
    update: (id: number, body: ApplicantInput) =>
      request<Applicant>(`/api/applicants/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
    remove: (id: number) => request<void>(`/api/applicants/${id}`, { method: 'DELETE' }),
    restore: (id: number) =>
      request<Applicant>(`/api/applicants/${id}/restore`, { method: 'POST' }),
    cases: (id: number) => request<NaturalizationCase[]>(`/api/applicants/${id}/cases`),
    history: (id: number) => request<AuditEvent[]>(`/api/applicants/${id}/history`),
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
    // No `actor`: the API takes it from the bearer token now.
    transition: (id: number, body: { status: CaseStatus; notes?: string }) =>
      request<NaturalizationCase>(`/api/cases/${id}/status`, {
        method: 'POST',
        body: JSON.stringify(body),
      }),
  },

  decisions: {
    list: (params: { from?: string; to?: string; fieldOffice?: string } = {}) =>
      request<Decision[]>(`/api/decisions${query(params)}`),
    // No `decidedBy`, for the same reason.
    create: (body: {
      caseId: number
      outcome: DecisionOutcome
      rationale: string
      denialReasonCode?: string
    }) => request<Decision>('/api/decisions', { method: 'POST', body: JSON.stringify(body) }),
  },

  reports: {
    caseRecord: (caseId: number) =>
      download(`/api/reports/case/${caseId}.pdf`, `case-record-${caseId}.pdf`),
    approvals: (params: { from?: string; to?: string; fieldOffice?: string } = {}) =>
      download(`/api/reports/approvals.pdf${query(params)}`, 'approvals.pdf'),
    pipeline: () => download('/api/reports/pipeline.pdf', 'pipeline.pdf'),
  },
}

export type ApplicantInput = Omit<Applicant, 'id' | 'createdAt'>
