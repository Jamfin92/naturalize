import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, Download, FileCheck2, FileX2, FileClock } from 'lucide-react'
import { toast } from 'sonner'

import { ErrorState, LoadingRows, PageHeader } from '@/components/page'
import { OutcomeBadge, StatusBadge } from '@/components/status-badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { api } from '@/lib/api'
import { useAsync } from '@/lib/use-async'
import { STATUS_LABELS, TERMINAL_STATUSES, type DocumentStatus } from '@/lib/types'

const DOC_ICON: Record<DocumentStatus, React.ElementType> = {
  Received: FileClock,
  Verified: FileCheck2,
  Rejected: FileX2,
}

const DOC_TONE: Record<DocumentStatus, string> = {
  Received: 'text-muted-foreground',
  Verified: 'text-success',
  Rejected: 'text-destructive',
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

export function CaseDetailPage() {
  const { id } = useParams<{ id: string }>()
  const caseId = Number(id)
  const [busy, setBusy] = useState(false)

  const { data, error, loading, reload } = useAsync(() => api.cases.get(caseId), [caseId])

  if (error) return <ErrorState message={error} />
  if (loading || !data) return <LoadingRows rows={6} />

  const transition = async (status: (typeof data.allowedTransitions)[number]) => {
    setBusy(true)
    try {
      // No `actor`: the API reads it from the bearer token, so the timeline
      // cannot be signed with a name the client picked.
      await api.cases.transition(caseId, { status })
      toast.success(`Case moved to “${STATUS_LABELS[status]}”.`)
      reload()
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Transition failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <PageHeader
        title={data.applicantName}
        description={`${data.receiptNumber} · filed ${new Date(data.filedOn).toLocaleDateString()} · ${data.fieldOffice}`}
        actions={
          <>
            <Button
              variant="outline"
              size="sm"
              onClick={() =>
                api.reports
                  .caseRecord(caseId)
                  .catch((e: unknown) =>
                    toast.error(e instanceof Error ? e.message : 'Could not generate the PDF.'),
                  )
              }
            >
              <Download className="size-4" />
              Case record (PDF)
            </Button>
            <Button variant="outline" size="sm" asChild>
              <Link to="/cases">
                <ArrowLeft className="size-4" />
                All cases
              </Link>
            </Button>
          </>
        }
      />

      <div className="mb-6 flex flex-wrap items-center gap-3">
        <StatusBadge status={data.status} className="px-3 py-1 text-sm" />
        <span className="text-muted-foreground text-sm">
          {TERMINAL_STATUSES.includes(data.status)
            ? `Closed after ${data.daysPending} days`
            : `${data.daysPending} days open`}
        </span>
        {data.decision && (
          <>
            <Separator orientation="vertical" className="h-5" />
            <OutcomeBadge outcome={data.decision.outcome} />
            <span className="text-muted-foreground text-sm">
              by {data.decision.decidedBy} on{' '}
              {new Date(data.decision.decidedOn).toLocaleDateString()}
            </span>
          </>
        )}
      </div>

      {/* Advance the case. Buttons come from the server's state machine. */}
      {data.allowedTransitions.length > 0 && (
        <Card className="mb-6">
          <CardContent className="flex flex-wrap items-center gap-3 p-4">
            <span className="text-muted-foreground text-sm font-medium">Advance case to:</span>
            {data.allowedTransitions.map((s) => (
              <Button
                key={s}
                size="sm"
                variant={s === 'Denied' ? 'destructive' : 'default'}
                disabled={busy}
                onClick={() => transition(s)}
              >
                {STATUS_LABELS[s]}
              </Button>
            ))}
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="timeline">
        <TabsList>
          <TabsTrigger value="timeline">Timeline</TabsTrigger>
          <TabsTrigger value="documents">Documents ({data.documents.length})</TabsTrigger>
          <TabsTrigger value="applicant">Applicant</TabsTrigger>
        </TabsList>

        <TabsContent value="timeline" className="mt-4">
          <Card>
            <CardContent className="p-5">
              {data.events.length === 0 ? (
                <p className="text-muted-foreground text-sm">No events recorded.</p>
              ) : (
                <ol className="relative space-y-5 pl-6">
                  {/* The rail the event dots sit on. */}
                  <div className="bg-border absolute top-1 bottom-1 left-[3px] w-px" />
                  {data.events.map((e) => (
                    <li key={e.id} className="relative">
                      <div className="border-background bg-primary absolute top-1 -left-6 size-[7px] rounded-full border-2" />
                      <div className="text-foreground text-sm font-medium">{e.eventType}</div>
                      {e.notes && (
                        <div className="text-muted-foreground mt-0.5 text-sm">{e.notes}</div>
                      )}
                      <div className="text-muted-foreground mt-1 text-xs">
                        {new Date(e.occurredAt).toLocaleString()} · {e.actor}
                      </div>
                    </li>
                  ))}
                </ol>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="documents" className="mt-4">
          <Card>
            <CardContent className="p-5">
              {data.documents.length === 0 ? (
                <p className="text-muted-foreground text-sm">No evidence filed.</p>
              ) : (
                <ul className="divide-border divide-y">
                  {data.documents.map((d) => {
                    const Icon = DOC_ICON[d.status]
                    return (
                      <li key={d.id} className="flex items-center gap-3 py-3 first:pt-0 last:pb-0">
                        <Icon className={`size-5 shrink-0 ${DOC_TONE[d.status]}`} />
                        <div className="min-w-0 flex-1">
                          <div className="text-foreground truncate text-sm font-medium">
                            {d.documentType}
                          </div>
                          <div className="text-muted-foreground truncate text-xs">
                            {d.fileName} · {formatBytes(d.sizeBytes)} ·{' '}
                            <span className="font-mono">{d.sha256.slice(0, 12)}…</span>
                          </div>
                        </div>
                        <span className="text-muted-foreground shrink-0 text-xs">{d.status}</span>
                      </li>
                    )
                  })}
                </ul>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="applicant" className="mt-4">
          <Card>
            <CardContent className="p-5">
              <dl className="grid grid-cols-2 gap-4 @2xl:grid-cols-3">
                {[
                  ['A-Number', data.applicant.alienNumber],
                  ['Date of birth', new Date(data.applicant.dateOfBirth).toLocaleDateString()],
                  ['Country of birth', data.applicant.countryOfBirth],
                  ['Nationality', data.applicant.nationality],
                  [
                    'LPR since',
                    new Date(data.applicant.lawfulPermanentResidentSince).toLocaleDateString(),
                  ],
                  ['Email', data.applicant.email],
                  ['Phone', data.applicant.phone],
                  [
                    'Residence',
                    `${data.applicant.city}, ${data.applicant.state} ${data.applicant.postalCode}`,
                  ],
                ].map(([label, value]) => (
                  <div key={label} className="space-y-0.5">
                    <dt className="text-muted-foreground text-xs tracking-wide uppercase">
                      {label}
                    </dt>
                    <dd className="text-foreground text-sm">{value}</dd>
                  </div>
                ))}
              </dl>
              <Separator className="my-4" />
              <Button variant="outline" size="sm" asChild>
                <Link to={`/applicants/${data.applicantId}`}>View full applicant record</Link>
              </Button>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </>
  )
}
