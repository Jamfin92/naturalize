import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { STATUS_LABELS, type ApplicationStatus } from '@/lib/types'

/*
 * Status colour is semantic, not decorative:
 *   navy   = in motion
 *   green  = good outcome
 *   red    = adverse outcome (the flag red, reused as --destructive)
 *   muted  = received, or closed without adjudication
 */
const STATUS_CLASS: Record<ApplicationStatus, string> = {
  Received: 'bg-muted text-muted-foreground border-border',
  InReview: 'bg-primary/10 text-primary border-primary/30',
  Approved: 'bg-success/15 text-success border-success/40',
  Denied: 'bg-destructive/12 text-destructive border-destructive/40',
  Naturalized: 'bg-success/20 text-success border-success/50 font-semibold',
  Withdrawn: 'bg-muted text-muted-foreground border-border line-through',
}

export function StatusBadge({
  status,
  className,
}: {
  status: ApplicationStatus
  className?: string
}) {
  return (
    <Badge variant="outline" className={cn('font-medium', STATUS_CLASS[status], className)}>
      {STATUS_LABELS[status]}
    </Badge>
  )
}
