import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'

import { api, setUnauthorizedHandler } from './api'
import { clearToken, getToken, setToken } from './token'
import type { Officer } from './types'

/*
 * Real authentication, against the API.
 *
 * This was a stub: it accepted any email, verified nothing, and invented an
 * officer client-side. Sign-in now exchanges credentials for a JWT (see
 * api/src/Naturalization.Api/Endpoints/AuthEndpoints.cs), and the officer
 * identity is whatever the SERVER says it is — which matters, because that
 * identity is what gets written into the audit trail against every change to
 * somebody's record.
 *
 * There are no refresh tokens: a token lasts a shift, then you sign in again.
 */

interface AuthState {
  officer: Officer | null
  /** True until the stored token has been checked against the API. */
  loading: boolean
  signIn: (email: string, password: string) => Promise<void>
  signOut: () => void
}

const AuthContext = createContext<AuthState>({
  officer: null,
  loading: true,
  signIn: async () => {},
  signOut: () => {},
})

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [officer, setOfficer] = useState<Officer | null>(null)
  const [loading, setLoading] = useState(true)
  const navigate = useNavigate()

  const signOut = useCallback(() => {
    clearToken()
    setOfficer(null)
  }, [])

  /*
   * A token expires mid-session — eight hours in, halfway through a form. Without
   * this, every panel would simply start failing with a 401 and no explanation.
   * The API client raises it here instead, and we end the session cleanly.
   */
  useEffect(() => {
    setUnauthorizedHandler(() => {
      setOfficer(null)
      navigate('/', { replace: true })
    })
  }, [navigate])

  /*
   * On boot, a token in localStorage is a CLAIM, not proof — it may be expired,
   * or signed with a key the server has since rotated. Verify it before letting
   * anyone into the shell, or a stale token walks straight past RequireAuth and
   * then 401s on every request behind it.
   */
  useEffect(() => {
    if (!getToken()) {
      setLoading(false)
      return
    }

    let cancelled = false
    api.auth
      .me()
      .then((me) => {
        if (!cancelled) setOfficer(me)
      })
      .catch(() => {
        if (!cancelled) clearToken()
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [])

  const signIn = useCallback(async (email: string, password: string) => {
    const result = await api.auth.login(email, password)
    setToken(result.accessToken)
    setOfficer(result.officer)
  }, [])

  return <AuthContext value={{ officer, loading, signIn, signOut }}>{children}</AuthContext>
}

// eslint-disable-next-line react-refresh/only-export-components
export const useAuth = () => useContext(AuthContext)

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { officer, loading } = useAuth()
  const location = useLocation()

  // Do not redirect while the stored token is still being verified — that would
  // bounce a legitimately signed-in officer to the login page on every refresh.
  if (loading) {
    return (
      <div className="text-muted-foreground flex min-h-screen items-center justify-center text-sm">
        Verifying session…
      </div>
    )
  }

  if (!officer) {
    return <Navigate to="/" replace state={{ from: location.pathname }} />
  }

  return <>{children}</>
}
