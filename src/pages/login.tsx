import { useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { AlertCircle, KeyRound, Moon, Sun } from 'lucide-react'

import { UsFlag } from '@/components/flag/us-flag'
import { useTheme } from '@/components/theme-provider'
import { useAuth } from '@/lib/auth'
import { ApiError } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

/** Landing page after sign-in when nothing more specific was requested. */
const HOME = '/dashboard'

export function LoginPage() {
  const { officer, signIn } = useAuth()
  const { theme, toggleTheme } = useTheme()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  /*
   * RequireAuth stashes the page they were trying to reach. Honour it: bouncing
   * someone to the dashboard after they asked for /applicants/42 and got
   * intercepted is a small betrayal that the old code committed every time.
   */
  const intended = (location.state as { from?: string } | null)?.from ?? HOME

  if (officer) return <Navigate to={intended} replace />

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSubmitting(true)

    try {
      await signIn(email.trim(), password)
      // No navigate() here: signIn sets the officer, and the <Navigate> above
      // then redirects. Navigating here as well races that render.
    } catch (err: unknown) {
      setError(
        err instanceof ApiError && err.status === 401
          ? 'Those credentials were not recognised.'
          : err instanceof Error
            ? err.message
            : 'Could not sign in.',
      )
      setSubmitting(false)
    }
  }

  return (
    <div className="bg-background relative flex min-h-screen flex-col">
      {/* Brass hairline across the very top — the one gilt accent on the page. */}
      <div className="bg-accent h-1 w-full" />

      <header className="mx-auto flex w-full max-w-6xl items-center justify-between px-6 py-4">
        <div className="flex items-center gap-2.5">
          <div className="ring-border w-8 overflow-hidden rounded-xs shadow-sm ring-1">
            <UsFlag animated={false} />
          </div>
          <span className="font-heading text-foreground text-base font-bold tracking-tight">
            Naturalize
          </span>
        </div>

        <nav className="flex items-center gap-1">
          <Button variant="ghost" size="sm" asChild>
            <a href="#about">About</a>
          </Button>
          <Button variant="ghost" size="sm" asChild>
            <a href="#process">The Process</a>
          </Button>
          <Button variant="ghost" size="sm" asChild>
            <a
              href="https://github.com/Jamfin92/naturalize"
              target="_blank"
              rel="noreferrer noopener"
            >
              Source
            </a>
          </Button>
          <Button variant="ghost" size="icon" onClick={toggleTheme} aria-label="Toggle theme">
            {theme === 'dark' ? <Sun className="size-4" /> : <Moon className="size-4" />}
          </Button>
        </nav>
      </header>

      <main className="mx-auto grid w-full max-w-6xl flex-1 items-center gap-10 px-6 py-10 lg:grid-cols-[1.15fr_1fr] lg:gap-16">
        {/* The flag: the largest object on the page, by a wide margin. */}
        <section className="flex flex-col gap-7">
          <div className="ring-border/70 overflow-hidden rounded-md shadow-2xl ring-1">
            <UsFlag title="Flag of the United States of America" />
          </div>

          <div className="space-y-3">
            <h1 className="font-heading text-foreground text-3xl leading-tight font-bold tracking-tight text-balance lg:text-4xl">
              Every case is a person waiting to become a citizen.
            </h1>
            <p className="text-muted-foreground max-w-prose leading-relaxed text-pretty">
              Naturalize is an open-source records system for tracking N-400 applicants from filing
              through the oath of allegiance — the interviews, the evidence, the decisions, and the
              audit trail behind each one.
            </p>
          </div>

          <div className="text-muted-foreground flex flex-wrap items-center gap-x-6 gap-y-2 text-sm">
            <span className="text-foreground font-medium">13 stripes</span>
            <span className="text-foreground font-medium">50 stars</span>
            <span className="text-foreground font-medium">One oath</span>
          </div>
        </section>

        {/* Sign-in. */}
        <section>
          <Card className="border-border shadow-lg">
            <CardContent className="space-y-6 p-6 lg:p-8">
              <div className="space-y-1.5">
                <h2 className="font-heading text-foreground text-xl font-bold">Officer sign-in</h2>
                <p className="text-muted-foreground text-sm">
                  Access is limited to authorized adjudication staff.
                </p>
              </div>

              <form onSubmit={handleSubmit} className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="email">Work email</Label>
                  <Input
                    id="email"
                    type="email"
                    autoComplete="username"
                    placeholder="a.hernandez@example.gov"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="password">Password</Label>
                  <Input
                    id="password"
                    type="password"
                    autoComplete="current-password"
                    placeholder="••••••••••••"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                  />
                </div>

                {error && (
                  <div
                    role="alert"
                    className="border-destructive/40 bg-destructive/10 text-destructive flex items-start gap-2 rounded-md border px-3 py-2 text-xs"
                  >
                    <AlertCircle className="mt-px size-3.5 shrink-0" />
                    <span>{error}</span>
                  </div>
                )}

                <Button type="submit" className="w-full" disabled={submitting}>
                  {submitting ? 'Signing in…' : 'Sign in'}
                </Button>
              </form>

              {/*
               * The credentials are printed here because this is a demo whose
               * every applicant is fabricated, and a sign-in wall with no way
               * through helps nobody. The README says the same, at more length.
               */}
              <div className="border-accent bg-accent/10 text-foreground/80 flex gap-2.5 rounded-md border-l-2 py-2.5 pr-3 pl-3 text-xs leading-relaxed">
                <KeyRound className="text-accent mt-px size-4 shrink-0" />
                <div className="space-y-1">
                  <p>
                    <span className="text-foreground font-semibold">Demo build.</span> Sign in with{' '}
                    <code className="font-mono">a.hernandez@example.gov</code> /{' '}
                    <code className="font-mono">Naturalize!Demo1</code>. All case data is fabricated.
                  </p>
                  <p>
                    Accounts are seeded and the signing key ships in the repo — provision a real
                    identity provider before this goes anywhere near a real record.
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>
        </section>
      </main>

      <footer className="border-border border-t">
        <div className="text-muted-foreground mx-auto flex w-full max-w-6xl flex-col gap-1 px-6 py-5 text-center text-xs sm:flex-row sm:justify-between sm:text-left">
          <p>
            Open source under the MIT License. Not affiliated with USCIS or any government agency.
          </p>
          <p>Not legal advice.</p>
        </div>
      </footer>
    </div>
  )
}
