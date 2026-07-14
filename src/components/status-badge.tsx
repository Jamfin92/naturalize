import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { STATUS_LABELS, type CaseStatus, type DecisionOutcome } from '@/lib/types'

/*
 * Status colour is semantic, not decorative:
 *   brass  = waiting on us (scheduled work)
 *   navy   = in motion
 *   green  = good outcome
 *   red    = adverse outcome (the flag red, reused as --destructive)
 *   muted  = closed without adjudication
 */
const STATUS_CLASS: Record<CaseStatus, string> = {
  Received: 'bg-muted text-muted-foreground border-border',
  BiometricsScheduled: 'bg-accent/15 text-accent-foreground border-accent/40',
  BiometricsCompleted: 'bg-primary/10 text-primary border-primary/30',
  InterviewScheduled: 'bg-accent/15 text-accent-foreground border-accent/40',
  InterviewCompleted: 'bg-primary/10 text-primary border-primary/30',
  Approved: 'bg-success/15 text-success border-success/40',
  Denied: 'bg-destructive/12 text-destructive border-destructive/40',
  OathScheduled: 'bg-accent/20 text-accent-foreground border-accent/50',
  Naturalized: 'bg-success/20 text-success border-success/50 font-semibold',
  Withdrawn: 'bg-muted text-muted-foreground border-border line-through',
}

export function StatusBadge({ status, className }: { status: CaseStatus; className?: string }) {
  return (
    <Badge variant="outline" className={cn('font-medium', STATUS_CLASS[status], className)}>
      {STATUS_LABELS[status]}
    </Badge>
  )
}

const OUTCOME_CLASS: Record<DecisionOutcome, string> = {
  Approved: 'bg-success/15 text-success border-success/40',
  Denied: 'bg-destructive/12 text-destructive border-destructive/40',
  Continued: 'bg-accent/15 text-accent-foreground border-accent/40',
}

export function OutcomeBadge({
  outcome,
  className,
}: {
  outcome: DecisionOutcome
  className?: string
}) {
  return (
    <Badge variant="outline" className={cn('font-medium', OUTCOME_CLASS[outcome], className)}>
      {outcome}
    </Badge>
  )
}
