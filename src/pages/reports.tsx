import { useState } from 'react'
import { Download, FileBarChart, FileSpreadsheet, FileText } from 'lucide-react'

import { PageHeader } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { api } from '@/lib/api'

/*
 * Reports are plain <a href> downloads rather than fetch + blob. The API sets
 * Content-Disposition: attachment, so the browser streams the PDF straight to
 * disk — no reason to buffer megabytes through JS to achieve the same thing.
 */
function ReportCard({
  icon: Icon,
  title,
  description,
  href,
  children,
}: {
  icon: React.ElementType
  title: string
  description: string
  href: string
  children?: React.ReactNode
}) {
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
          <Button asChild className="w-full">
            <a href={href} target="_blank" rel="noreferrer">
              <Download className="size-4" />
              Download PDF
            </a>
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
          href={api.reports.approvalsUrl({ from, to, fieldOffice: office || undefined })}
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
          href={api.reports.pipelineUrl()}
        />

        <ReportCard
          icon={FileText}
          title="Case record"
          description="The complete file for a single case: applicant particulars, timeline, evidence, and decision."
          href={caseId ? api.reports.caseRecordUrl(Number(caseId)) : '#'}
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
              Or download it directly from any case's detail page.
            </p>
          </div>
        </ReportCard>
      </div>
    </>
  )
}
