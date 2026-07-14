import { useState } from 'react'
import { Download, FileBarChart, FileSpreadsheet, FileText } from 'lucide-react'
import { toast } from 'sonner'

import { PageHeader } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { api } from '@/lib/api'

/*
 * These were <a href> links until the reports moved behind authentication.
 *
 * A browser navigation cannot carry an Authorization header, so an <a href> to a
 * protected endpoint opens a blank tab containing 401 JSON — and no test catches
 * that, because the request never goes through the API client at all. The PDF is
 * now fetched with the bearer token and saved from a blob; see `download()` in
 * src/lib/api.ts.
 */
function ReportCard({
  icon: Icon,
  title,
  description,
  onDownload,
  disabled,
  children,
}: {
  icon: React.ElementType
  title: string
  description: string
  onDownload: () => Promise<void>
  disabled?: boolean
  children?: React.ReactNode
}) {
  const [busy, setBusy] = useState(false)

  const handleClick = async () => {
    setBusy(true)
    try {
      await onDownload()
    } catch (e: unknown) {
      toast.error(e instanceof Error ? e.message : 'Could not generate the report.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className="flex flex-col">
      <CardContent className="flex flex-1 flex-col gap-4 p-5">
        <div className="flex items-start gap-3">
          <div className="bg-primary/10 text-primary grid size-10 shrink-0 place-items-center rounded-md">
            <Icon className="size-5" />
          </div>
          <div className="min-w-0 space-y-1">
            <h2 className="font-heading text-foreground text-base font-bold">{title}</h2>
            <p className="text-muted-foreground text-sm leading-relaxed">{description}</p>
          </div>
        </div>

        {children}

        <div className="mt-auto pt-1">
          {/*
           * The accessible name names the report. Three buttons all announcing
           * "Download PDF" tells a screen-reader user nothing about which one they
           * are on — the visible label leans on the heading above it, which is
           * context the button itself does not carry.
           */}
          <Button
            className="w-full"
            onClick={handleClick}
            disabled={busy || disabled}
            aria-label={`Download ${title}`}
          >
            <Download className="size-4" />
            {busy ? 'Generating…' : 'Download PDF'}
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}

export function ReportsPage() {
  const today = new Date()
  const firstOfYear = new Date(today.getFullYear(), 0, 1)

  const [from, setFrom] = useState(firstOfYear.toISOString().slice(0, 10))
  const [to, setTo] = useState(today.toISOString().slice(0, 10))
  const [office, setOffice] = useState('')
  const [caseId, setCaseId] = useState('')

  return (
    <>
      <PageHeader
        title="Reports"
        description="Generated server-side as PDFs. Fonts are embedded, so they render identically anywhere."
      />

      <div className="grid gap-6 @3xl:grid-cols-2 @5xl:grid-cols-3">
        <ReportCard
          icon={FileBarChart}
          title="Approvals report"
          description="Every decision in a date range, with approve/deny counts broken out by field office."
          onDownload={() => api.reports.approvals({ from, to, fieldOffice: office || undefined })}
        >
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="from" className="text-xs">
                From
              </Label>
              <Input
                id="from"
                type="date"
                value={from}
                onChange={(e) => setFrom(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="to" className="text-xs">
                To
              </Label>
              <Input id="to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
            </div>
            <div className="col-span-2 space-y-1.5">
              <Label htmlFor="office" className="text-xs">
                Field office <span className="text-muted-foreground">(all if blank)</span>
              </Label>
              <Input
                id="office"
                value={office}
                onChange={(e) => setOffice(e.target.value)}
                placeholder="e.g. Boston, MA"
              />
            </div>
          </div>
        </ReportCard>

        <ReportCard
          icon={FileSpreadsheet}
          title="Pipeline report"
          description="Current caseload by status, with case aging and the oldest pending matters flagged."
          onDownload={() => api.reports.pipeline()}
        />

        <ReportCard
          icon={FileText}
          title="Case record"
          description="The complete file for a single case: applicant particulars, timeline, evidence, and decision."
          onDownload={() => api.reports.caseRecord(Number(caseId))}
          // Previously this rendered href="#" when blank, so clicking it opened a
          // blank tab. Disable it instead of pretending it's a link.
          disabled={!caseId}
        >
          <div className="space-y-1.5">
            <Label htmlFor="caseId" className="text-xs">
              Case ID
            </Label>
            <Input
              id="caseId"
              type="number"
              min={1}
              value={caseId}
              onChange={(e) => setCaseId(e.target.value)}
              placeholder="e.g. 1"
            />
            <p className="text-muted-foreground text-xs">
              Case IDs are listed on each applicant's record.
            </p>
          </div>
        </ReportCard>
      </div>
    </>
  )
}
