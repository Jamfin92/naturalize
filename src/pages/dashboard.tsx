import { Link } from 'react-router-dom'
import { CheckCircle2, Clock, Timer, XCircle } from 'lucide-react'

import { ErrorState, LoadingRows, PageHeader } from '@/components/page'
import { StatusBadge } from '@/components/status-badge'
import { Card, CardContent } from '@/components/ui/card'
import { api } from '@/lib/api'
import { useAsync } from '@/lib/use-async'
import { STATUS_LABELS, type CaseStatus } from '@/lib/types'
import { cn } from '@/lib/utils'

function Metric({
  icon: Icon,
  label,
  value,
  tone,
}: {
  icon: React.ElementType
  label: string
  value: string | number
  tone: 'primary' | 'success' | 'destructive' | 'accent'
}) {
  const tones = {
    primary: 'text-primary bg-primary/10',
    success: 'text-success bg-success/12',
    destructive: 'text-destructive bg-destructive/10',
    accent: 'text-accent-foreground bg-accent/20',
  }
  return (
    <Card>
      <CardContent className="flex items-center gap-4 p-5">
        <div className={cn('grid size-10 shrink-0 place-items-center rounded-md', tones[tone])}>
          <Icon className="size-5" />
        </div>
        <div className="min-w-0">
          <div className="font-heading text-foreground text-2xl leading-none font-bold">
            {value}
          </div>
          <div className="text-muted-foreground mt-1 truncate text-xs">{label}</div>
        </div>
      </CardContent>
    </Card>
  )
}

export function DashboardPage() {
  const { data, error, loading } = useAsync(() => api.metrics(), [])

  return (
    <>
      <PageHeader
        title="Dashboard"
        description="Caseload at a glance, across the field office."
      />

      {error && <ErrorState message={error} />}
      {loading && <LoadingRows rows={4} />}

      {data && (
        <div className="space-y-6">
          {/*
           * `@2xl:` not `2xl:` — these tiles must reflow against the width of
           * <main>, which changes when the sidebar collapses even though the
           * viewport does not. See app-shell.tsx.
           */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 @3xl:grid-cols-4">
            <Metric icon={Clock} label="Cases pending" value={data.pendingCases} tone="primary" />
            <Metric
              icon={CheckCircle2}
              label="Approved this month"
              value={data.approvedThisMonth}
              tone="success"
            />
            <Metric
              icon={XCircle}
              label="Denied this month"
              value={data.deniedThisMonth}
              tone="destructive"
            />
            <Metric
              icon={Timer}
              label="Median days to decision"
              value={data.medianDaysToDecision}
              tone="accent"
            />
          </div>

          <div className="grid gap-6 @4xl:grid-cols-[1.3fr_1fr]">
            <Card>
              <CardContent className="p-5">
                <h2 className="font-heading text-foreground mb-4 text-base font-bold">
                  Pipeline by status
                </h2>
                <PipelineChart counts={data.statusCounts} />
              </CardContent>
            </Card>

            <Card>
              <CardContent className="p-5">
                <h2 className="font-heading text-foreground mb-4 text-base font-bold">
                  Recent activity
                </h2>
                {data.recentEvents.length === 0 ? (
                  <p className="text-muted-foreground text-sm">No activity recorded yet.</p>
                ) : (
                  <ol className="space-y-3">
                    {data.recentEvents.map((e) => (
                      <li key={e.id} className="flex gap-3 text-sm">
                        <div className="bg-accent mt-1.5 size-1.5 shrink-0 rounded-full" />
                        <div className="min-w-0 flex-1">
                          <p className="text-foreground">
                            <Link
                              to={`/cases/${e.caseId}`}
                              className="hover:text-primary font-medium underline-offset-4 hover:underline"
                            >
                              {e.eventType}
                            </Link>
                          </p>
                          <p className="text-muted-foreground truncate text-xs">
                            {e.notes || '—'}
                          </p>
                          <p className="text-muted-foreground mt-0.5 text-[11px]">
                            {new Date(e.occurredAt).toLocaleDateString()} · {e.actor}
                          </p>
                        </div>
                      </li>
                    ))}
                  </ol>
                )}
              </CardContent>
            </Card>
          </div>
        </div>
      )}
    </>
  )
}

function PipelineChart({ counts }: { counts: Array<{ status: CaseStatus; count: number }> }) {
  const max = Math.max(1, ...counts.map((c) => c.count))

  return (
    <div className="space-y-2.5">
      {counts.map(({ status, count }) => (
        <div key={status} className="flex items-center gap-3">
          <div className="w-44 shrink-0">
            <StatusBadge status={status} className="text-[11px]" />
          </div>
          <div className="bg-muted h-5 min-w-0 flex-1 overflow-hidden rounded-sm">
            <div
              className="bg-primary h-full rounded-sm transition-[width] duration-500"
              style={{ width: `${(count / max) * 100}%` }}
              role="img"
              aria-label={`${STATUS_LABELS[status]}: ${count} cases`}
            />
          </div>
          <div className="text-foreground w-8 shrink-0 text-right font-mono text-xs tabular-nums">
            {count}
          </div>
        </div>
      ))}
    </div>
  )
}
