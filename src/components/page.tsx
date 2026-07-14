import { AlertTriangle, Inbox } from 'lucide-react'
import { Skeleton } from '@/components/ui/skeleton'

export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string
  description?: string
  actions?: React.ReactNode
}) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div className="space-y-1">
        <h1 className="font-heading text-foreground text-2xl font-bold tracking-tight">{title}</h1>
        {description && <p className="text-muted-foreground text-sm">{description}</p>}
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  )
}

export function LoadingRows({ rows = 6 }: { rows?: number }) {
  return (
    <div className="space-y-2">
      {Array.from({ length: rows }, (_, i) => (
        <Skeleton key={i} className="h-12 w-full" />
      ))}
    </div>
  )
}

/*
 * The API being unreachable is the single most likely failure for anyone who
 * clones this repo and runs only the frontend, so name that cause explicitly
 * rather than showing a bare "request failed".
 */
export function ErrorState({ message }: { message: string }) {
  return (
    <div className="border-destructive/40 bg-destructive/5 text-foreground flex gap-3 rounded-md border p-4">
      <AlertTriangle className="text-destructive mt-0.5 size-5 shrink-0" />
      <div className="space-y-1 text-sm">
        <p className="font-semibold">Could not load data</p>
        <p className="text-muted-foreground">{message}</p>
        <p className="text-muted-foreground">
          Is the API running? Start it with{' '}
          <code className="bg-muted rounded px-1 py-0.5 text-xs">
            dotnet run --project api/src/Naturalization.Api
          </code>
          .
        </p>
      </div>
    </div>
  )
}

export function EmptyState({ message }: { message: string }) {
  return (
    <div className="border-border text-muted-foreground flex flex-col items-center gap-2 rounded-md border border-dashed p-10 text-center">
      <Inbox className="size-6" />
      <p className="text-sm">{message}</p>
    </div>
  )
}
