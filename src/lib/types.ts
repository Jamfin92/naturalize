/** Mirrors the C# domain in api/src/Naturalization.Api/Domain. Keep in sync. */

/** The signed-in caseworker, as resolved by the API from the bearer token. */
export interface Officer {
  id: number
  name: string
  email: string
  fieldOffice: string
}

/**
 * A row in the system audit log: who touched this record, and how.
 *
 * Distinct from CaseEvent (a case's own lifecycle). This one has no foreign key
 * and is never deleted, so it survives the record it describes — which is what
 * makes a withdrawn applicant's history still readable.
 */
export interface AuditEvent {
  id: number
  entityType: string
  entityId: number
  action: 'Created' | 'Updated' | 'Deleted' | 'Restored' | string
  actor: string
  occurredAt: string
  summary: string
}

export const CASE_STATUSES = [
  'Received',
  'BiometricsScheduled',
  'BiometricsCompleted',
  'InterviewScheduled',
  'InterviewCompleted',
  'Approved',
  'Denied',
  'OathScheduled',
  'Naturalized',
  'Withdrawn',
] as const

export type CaseStatus = (typeof CASE_STATUSES)[number]

export const DECISION_OUTCOMES = ['Approved', 'Denied', 'Continued'] as const
export type DecisionOutcome = (typeof DECISION_OUTCOMES)[number]

export const DOCUMENT_STATUSES = ['Received', 'Verified', 'Rejected'] as const
export type DocumentStatus = (typeof DOCUMENT_STATUSES)[number]

export interface Applicant {
  id: number
  alienNumber: string
  fullName: string
  dateOfBirth: string
  countryOfBirth: string
  nationality: string
  addressLine: string
  city: string
  state: string
  postalCode: string
  email: string
  phone: string
  lawfulPermanentResidentSince: string
  createdAt: string
}

export interface NaturalizationCase {
  id: number
  applicantId: number
  applicantName: string
  alienNumber: string
  receiptNumber: string
  filedOn: string
  fieldOffice: string
  status: CaseStatus
  biometricsOn: string | null
  interviewOn: string | null
  oathOn: string | null
  daysPending: number
}

export interface Decision {
  id: number
  caseId: number
  receiptNumber: string
  applicantName: string
  outcome: DecisionOutcome
  decidedOn: string
  decidedBy: string
  rationale: string
  denialReasonCode: string | null
}

export interface EvidenceDocument {
  id: number
  caseId: number
  documentType: string
  fileName: string
  contentType: string
  sizeBytes: number
  sha256: string
  status: DocumentStatus
  uploadedAt: string
}

export interface CaseEvent {
  id: number
  caseId: number
  eventType: string
  occurredAt: string
  actor: string
  notes: string
}

export interface CaseDetail extends NaturalizationCase {
  applicant: Applicant
  documents: EvidenceDocument[]
  events: CaseEvent[]
  decision: Decision | null
  /*
   * The legal next statuses, computed server-side by StatusTransitions.cs.
   * The client renders exactly these as buttons rather than re-implementing
   * the state machine — one source of truth, and no way for the UI to offer
   * a transition the API would reject.
   */
  allowedTransitions: CaseStatus[]
}

export interface DashboardMetrics {
  totalApplicants: number
  pendingCases: number
  approvedThisMonth: number
  deniedThisMonth: number
  medianDaysToDecision: number
  statusCounts: Array<{ status: CaseStatus; count: number }>
  recentEvents: CaseEvent[]
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

/** Human-facing labels — the enum names are terse on purpose. */
export const STATUS_LABELS: Record<CaseStatus, string> = {
  Received: 'Received',
  BiometricsScheduled: 'Biometrics scheduled',
  BiometricsCompleted: 'Biometrics completed',
  InterviewScheduled: 'Interview scheduled',
  InterviewCompleted: 'Interview completed',
  Approved: 'Approved',
  Denied: 'Denied',
  OathScheduled: 'Oath scheduled',
  Naturalized: 'Naturalized',
  Withdrawn: 'Withdrawn',
}

/** Terminal states carry no further action. */
export const TERMINAL_STATUSES: CaseStatus[] = ['Naturalized', 'Denied', 'Withdrawn']
