/*
 * The bearer token, and the one place it lives.
 *
 * This is deliberately a separate module from `auth.tsx`, and it is the seam the
 * Okta carve-out hangs on: the API accepts an Okta-issued token exactly as
 * readily as a locally-issued one (it routes on the token's `iss` claim — see
 * api/src/Naturalization.Api/Auth/AuthExtensions.cs). So adding OIDC to this
 * front end means writing a redirect flow that deposits its token HERE. Nothing
 * downstream — not the API client, not RequireAuth — needs to know which
 * identity provider minted it.
 *
 * On localStorage: it is readable by any script that achieves XSS on this origin.
 * The alternative — an httpOnly cookie — is not reachable from JS at all, and is
 * what a deployment holding real records should use. It is not what this uses,
 * because a cookie means CSRF protection and a same-site story, and pretending
 * otherwise would be worse than saying so plainly. See the README.
 */

const KEY = 'naturalize.token'

let cached: string | null = null

export function getToken(): string | null {
  if (cached !== null) return cached
  try {
    cached = localStorage.getItem(KEY)
  } catch {
    cached = null
  }
  return cached
}

export function setToken(token: string): void {
  cached = token
  try {
    localStorage.setItem(KEY, token)
  } catch {
    /* private browsing: the in-memory copy still carries the session */
  }
}

export function clearToken(): void {
  cached = null
  try {
    localStorage.removeItem(KEY)
  } catch {
    /* nothing to do */
  }
}
