/** Mirrors the C# domain in api/src/Naturalization.Api/Domain. Keep in sync. */

/**
 * What a user is allowed to do. Mirrors the C# OfficerRole enum. Viewer is
 * read-only, Officer can add and edit applicants, Admin can additionally
 * withdraw and restore records.
 */
export type OfficerRole = 'Viewer' | 'Officer' | 'Admin'

/** The signed-in user, as resolved by the API from the bearer token. */
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
 * Canonicalise a role string coming off the wire.
 *
 * The API sends the C# enum name ("Admin"). This is a cross-language boundary,
 * though, and a strict `role === 'Admin'` fails SILENTLY on any drift — a value
 * that arrives as "admin", "ADMIN" or "Admin " renders as an Admin in the UI but
 * matches nothing, so every action is stripped from an account the sidebar still
 * labels Admin. Compare trimmed and case-insensitively; an unrecognised value
 * returns undefined and the callers below fail OPEN, as before.
 */
export function normalizeRole(role: string | null | undefined): OfficerRole | undefined {
  switch (role?.trim().toLowerCase()) {
    case 'viewer':
      return 'Viewer'
    case 'officer':
      return 'Officer'
    case 'admin':
      return 'Admin'
    default:
      return undefined
  }
}

/**
 * Can this user create or edit applicant records? Officer and Admin can; a
 * read-only Viewer cannot. A convenience — "don't offer an action that would
 * only 403" — NOT a security boundary; the API enforces the rule regardless. So
 * when the role is unknown (an object from an API that predates roles) we fail
 * OPEN: show the action and let the server decide.
 */
export function canManageApplicants(officer: Officer | null): boolean {
  if (!officer) return false
  const role = normalizeRole(officer.role)
  if (!role) return true
  return role === 'Officer' || role === 'Admin'
}

/** Can this user withdraw or restore a record? Admin only (fails open on unknown role). */
export function canWithdrawApplicants(officer: Officer | null): boolean {
  if (!officer) return false
  const role = normalizeRole(officer.role)
  if (!role) return true
  return role === 'Admin'
}

/**
 * A row in the system audit log: who touched this record, and how.
 *
 * Has no foreign key and is never deleted, so it survives the record it
 * describes — which is what makes a withdrawn applicant's history still readable.
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

/**
 * Where a naturalization application stands. Mirrors the C# ApplicationStatus
 * enum. The whole per-case pipeline collapsed to these once the case, decision
 * and evidence tables were folded into the applicant row.
 */
export const APPLICATION_STATUSES = [
  'Received',
  'InReview',
  'Approved',
  'Denied',
  'Naturalized',
  'Withdrawn',
] as const

export type ApplicationStatus = (typeof APPLICATION_STATUSES)[number]

/** A code/description pair from a lookup table (towns, countries). */
export interface Lookup {
  code: string
  description: string
}

export interface Applicant {
  id: number
  alienNumber: string
  naturalizationNumber: string
  petitionNumber: string
  firstName: string
  /** Optional; the API returns "" when the applicant has no middle name. */
  middleName: string
  lastName: string
  /** Server-composed "First Middle Last" for display. Read-only — not sent on create/edit. */
  fullName: string
  birthDate: string
  admissionDate: string
  address1: string
  townCode: string
  countryCode: string
  zipCode: string
  email: string
  status: ApplicationStatus
  /** Null until the application is decided. */
  decisionDate: string | null
  decisionNotes: string
  createdAt: string
  /** Null on a record never changed since creation. */
  updatedAt: string | null
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

/** Human-facing labels — the enum names are terse on purpose. */
export const STATUS_LABELS: Record<ApplicationStatus, string> = {
  Received: 'Received',
  InReview: 'In review',
  Approved: 'Approved',
  Denied: 'Denied',
  Naturalized: 'Naturalized',
  Withdrawn: 'Withdrawn',
}

/** Terminal states carry no further action. */
export const TERMINAL_STATUSES: ApplicationStatus[] = ['Naturalized', 'Denied', 'Withdrawn']
