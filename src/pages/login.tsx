import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { Moon, ShieldCheck, Sun } from 'lucide-react'

import { UsFlag } from '@/components/flag/us-flag'
import { useTheme } from '@/components/theme-provider'
import { useAuth } from '@/lib/auth'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

export function LoginPage() {
  const { officer, signIn } = useAuth()
  const { theme, toggleTheme } = useTheme()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  if (officer) return <Navigate to="/dashboard" replace />

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!email.trim()) return
    signIn(email.trim())
    navigate('/dashboard')
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
                  />
                </div>

                <Button type="submit" className="w-full">
                  Sign in
                </Button>
              </form>

              {/*
               * Stated plainly and in the UI, not just the README. Anyone who
               * stands this up should know before they type anything that the
               * login does not actually verify a thing.
               */}
              <div className="border-accent bg-accent/10 text-foreground/80 flex gap-2.5 rounded-md border-l-2 py-2.5 pr-3 pl-3 text-xs leading-relaxed">
                <ShieldCheck className="text-accent mt-px size-4 shrink-0" />
                <p>
                  <span className="text-foreground font-semibold">Demo build.</span> Authentication
                  is stubbed — any email signs you in, and all case data is fabricated. Wire up a
                  real identity provider before deploying this anywhere.
                </p>
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
