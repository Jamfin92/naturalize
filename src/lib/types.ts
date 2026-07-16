/** Mirrors the C# domain in api/src/Naturalization.Api/Domain. Keep in sync. */

/**
 * What an officer is allowed to do. Mirrors the C# OfficerRole enum. Viewer is
 * read-only, Officer can add and edit applicants, Admin can additionally
 * withdraw and restore records.
 */
export type OfficerRole = 'Viewer' | 'Officer' | 'Admin'

/** The signed-in caseworker, as resolved by the API from the bearer token. */
export interface Officer {
  id: number
  name: string
  email: string
  fieldOffice: string
  /**
   * Optional on purpose. An API older than this build simply won't send it, and
   * the permission helpers below fail OPEN in that case rather than stripping
   * every management action out of the UI. The server's 403 is the real gate.
   */
  role?: OfficerRole
}

/**
 * Can this officer create or edit applicant records? Officer and Admin can; a
 * read-only Viewer cannot.
 *
 * These helpers are a convenience — "don't offer an action that would only 403"
 * — NOT a security boundary; the API enforces the rule regardless. So when the
 * role is unknown (an officer object from an API that predates roles) we fail
 * OPEN: show the action and let the server decide. Hiding it would mean a
 * frontend one deploy ahead of its API silently loses the ability to edit
 * anything, which is precisely the trap the first cut of this fell into.
 */
export function canManageApplicants(officer: Officer | null): boolean {
  if (!officer) return false
  if (!officer.role) return true
  return officer.role === 'Officer' || officer.role === 'Admin'
}

/** Can this officer withdraw or restore a record? Admin only (fails open on unknown role). */
export function canWithdrawApplicants(officer: Officer | null): boolean {
  if (!officer) return false
  if (!officer.role) return true
  return officer.role === 'Admin'
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
  firstName: string
  /** Optional; the API returns "" when the applicant has no middle name. */
  middleName: string
  lastName: string
  /** Server-composed "First Middle Last" for display. Read-only — not sent on create/edit. */
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
