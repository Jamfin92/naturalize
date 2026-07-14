import { createContext, useContext, useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'

/*
 * Deliberately stubbed. Any credentials sign you in, and nothing is verified
 * against a server. This exists so the interface is navigable and so that
 * deployers have an obvious seam to drop a real identity provider into —
 * see docs/DEPLOYING.md. Do not mistake this for authentication.
 */

export interface Officer {
  name: string
  email: string
  fieldOffice: string
}

const STORAGE_KEY = 'naturalize.officer'

const AuthContext = createContext<{
  officer: Officer | null
  signIn: (email: string) => void
  signOut: () => void
}>({ officer: null, signIn: () => {}, signOut: () => {} })

function stored(): Officer | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as Officer) : null
  } catch {
    return null
  }
}

/** Turn "a.hernandez@example.gov" into "A. Hernandez" for display. */
function displayName(email: string): string {
  const local = email.split('@')[0] || 'Officer'
  return local
    .split(/[._-]+/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ')
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [officer, setOfficer] = useState<Officer | null>(stored)

  const signIn = (email: string) => {
    const next: Officer = {
      name: displayName(email),
      email,
      fieldOffice: 'Boston, MA',
    }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
    setOfficer(next)
  }

  const signOut = () => {
    localStorage.removeItem(STORAGE_KEY)
    setOfficer(null)
  }

  return <AuthContext value={{ officer, signIn, signOut }}>{children}</AuthContext>
}

// eslint-disable-next-line react-refresh/only-export-components
export const useAuth = () => useContext(AuthContext)

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const { officer } = useAuth()
  const location = useLocation()
  if (!officer) return <Navigate to="/" replace state={{ from: location.pathname }} />
  return <>{children}</>
}
