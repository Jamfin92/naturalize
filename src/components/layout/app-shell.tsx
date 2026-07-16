import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { LogOut, Moon, PanelLeftClose, PanelLeftOpen, Stamp, Sun, Users } from 'lucide-react'

import { UsFlag } from '@/components/flag/us-flag'
import { useTheme } from '@/components/theme-provider'
import { useAuth } from '@/lib/auth'
import { cn } from '@/lib/utils'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'

const NAV = [
  { to: '/applicants', label: 'Applicants', icon: Users },
  { to: '/reports', label: 'Reports', icon: Stamp },
]

function initials(name: string): string {
  return name
    .split(' ')
    .map((p) => p[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()
}

export function AppShell() {
  const [collapsed, setCollapsed] = useState(false)
  const { theme, toggleTheme } = useTheme()
  const { officer, signOut } = useAuth()
  const navigate = useNavigate()

  const handleSignOut = () => {
    signOut()
    navigate('/')
  }

  return (
    <div className="bg-background flex min-h-screen">
      <aside
        className={cn(
          'bg-sidebar border-sidebar-border sticky top-0 flex h-screen shrink-0 flex-col border-r transition-[width] duration-200',
          collapsed ? 'w-16' : 'w-60',
        )}
      >
        <div className="flex h-16 items-center gap-2.5 px-3">
          <div className="ring-sidebar-border w-9 shrink-0 overflow-hidden rounded-xs shadow-sm ring-1">
            <UsFlag animated={false} />
          </div>
          {!collapsed && (
            <div className="min-w-0 leading-tight">
              <div className="font-heading text-sidebar-foreground truncate text-sm font-bold">
                Naturalize
              </div>
              <div className="text-muted-foreground truncate text-[11px]">
                Case Management
              </div>
            </div>
          )}
        </div>

        <Separator className="bg-sidebar-border" />

        <nav className="flex flex-1 flex-col gap-1 p-2">
          {NAV.map(({ to, label, icon: Icon }) => {
            const link = (
              <NavLink
                key={to}
                to={to}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                    'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
                    isActive &&
                      'bg-sidebar-primary text-sidebar-primary-foreground hover:bg-sidebar-primary hover:text-sidebar-primary-foreground',
                    collapsed && 'justify-center px-0',
                  )
                }
              >
                <Icon className="size-4 shrink-0" />
                {/*
                 * Collapsed, the label is hidden but must still be readable: an
                 * icon-only link has no accessible name, and a tooltip is not
                 * one — it only fires on hover/focus and screen readers may
                 * never announce it. Keep the text in the tree, visually hidden.
                 */}
                <span className={cn(collapsed && 'sr-only')}>{label}</span>
              </NavLink>
            )

            // Collapsed rail hides the labels, so surface them on hover instead.
            return collapsed ? (
              <Tooltip key={to}>
                <TooltipTrigger asChild>{link}</TooltipTrigger>
                <TooltipContent side="right">{label}</TooltipContent>
              </Tooltip>
            ) : (
              link
            )
          })}
        </nav>

        <Separator className="bg-sidebar-border" />

        <div className={cn('flex items-center gap-2 p-2', collapsed && 'flex-col')}>
          <Avatar className="size-8 shrink-0">
            <AvatarFallback className="bg-sidebar-accent text-sidebar-accent-foreground text-xs font-semibold">
              {officer ? initials(officer.name) : '—'}
            </AvatarFallback>
          </Avatar>
          {!collapsed && officer && (
            <div className="min-w-0 flex-1 leading-tight">
              <div className="text-sidebar-foreground truncate text-xs font-semibold">
                {officer.name}
              </div>
              <div className="text-muted-foreground truncate text-[11px]">
                {officer.fieldOffice} · {officer.role}
              </div>
            </div>
          )}
          <Tooltip>
            <TooltipTrigger asChild>
              <Button variant="ghost" size="icon" className="size-8" onClick={handleSignOut}>
                <LogOut className="size-4" />
                <span className="sr-only">Sign out</span>
              </Button>
            </TooltipTrigger>
            <TooltipContent side="right">Sign out</TooltipContent>
          </Tooltip>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="bg-background/80 border-border sticky top-0 z-10 flex h-16 items-center gap-2 border-b px-4 backdrop-blur">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => setCollapsed((c) => !c)}
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          >
            {collapsed ? (
              <PanelLeftOpen className="size-4" />
            ) : (
              <PanelLeftClose className="size-4" />
            )}
          </Button>

          <div className="flex-1" />

          <Button variant="ghost" size="icon" onClick={toggleTheme} aria-label="Toggle theme">
            {theme === 'dark' ? <Sun className="size-4" /> : <Moon className="size-4" />}
          </Button>
        </header>

        {/*
         * The one place container queries genuinely earn their keep: the
         * sidebar collapses, which changes how much room `main` has WITHOUT
         * the viewport changing at all. A viewport media query is simply the
         * wrong instrument here — it cannot see this. Descendants opt in with
         * `@lg:` / `@3xl:` and reflow against their real available width.
         */}
        <main className="@container min-w-0 flex-1 p-4 lg:p-6">
          <Outlet />
        </main>

        <footer className="text-muted-foreground border-border border-t px-6 py-3 text-center text-xs">
          Reference implementation. Not affiliated with USCIS or any government agency. All case
          data shown is fabricated.
        </footer>
      </div>
    </div>
  )
}
