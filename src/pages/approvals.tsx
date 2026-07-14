import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Gavel } from 'lucide-react'
import { toast } from 'sonner'

import { EmptyState, ErrorState, LoadingRows, PageHeader } from '@/components/page'
import { OutcomeBadge } from '@/components/status-badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { api } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import { useAsync } from '@/lib/use-async'
import { DECISION_OUTCOMES, type DecisionOutcome } from '@/lib/types'

export function ApprovalsPage() {
  const { officer } = useAuth()
  const decisions = useAsync(() => api.decisions.list(), [])

  // Cases that have completed interview and are therefore ripe for a decision.
  const ripe = useAsync(() => api.cases.list({ status: 'InterviewCompleted', pageSize: 50 }), [])

  const [open, setOpen] = useState(false)
  const [caseId, setCaseId] = useState<number | null>(null)
  const [outcome, setOutcome] = useState<DecisionOutcome>('Approved')
  const [rationale, setRationale] = useState('')
  const [denialCode, setDenialCode] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async () => {
    if (!caseId) return
    setBusy(true)
    try {
      await api.decisions.create({
        caseId,
        outcome,
        decidedBy: officer?.name ?? 'Unknown officer',
        rationale,
        denialReasonCode: outcome === 'Denied' ? denialCode : undefined,
      })
      toast.success('Decision recorded.')
      setOpen(false)
      setRationale('')
      setDenialCode('')
      setCaseId(null)
      decisions.reload()
      ripe.reload()
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not record decision')
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <PageHeader
        title="Approvals"
        description="Record adjudication outcomes and review the decisions already entered."
        actions={
          <Button onClick={() => setOpen(true)} disabled={!ripe.data?.items.length}>
            <Gavel className="size-4" />
            Record decision
          </Button>
        }
      />

      {/* Awaiting decision */}
      <Card className="mb-6">
        <CardContent className="p-5">
          <h2 className="font-heading text-foreground mb-4 text-base font-bold">
            Awaiting decision
          </h2>
          {ripe.error && <ErrorState message={ripe.error} />}
          {ripe.loading && <LoadingRows rows={2} />}
          {ripe.data?.items.length === 0 && (
            <EmptyState message="No cases have completed interview and are awaiting a decision." />
          )}
          {ripe.data && ripe.data.items.length > 0 && (
            <ul className="divide-border divide-y">
              {ripe.data.items.map((c) => (
                <li key={c.id} className="flex items-center gap-3 py-2.5 first:pt-0 last:pb-0">
                  <div className="min-w-0 flex-1">
                    <Link
                      to={`/cases/${c.id}`}
                      className="text-foreground hover:text-primary text-sm font-medium underline-offset-4 hover:underline"
                    >
                      {c.applicantName}
                    </Link>
                    <div className="text-muted-foreground font-mono text-xs">
                      {c.receiptNumber}
                    </div>
                  </div>
                  <span className="text-muted-foreground shrink-0 text-xs">
                    {c.daysPending} days pending
                  </span>
                  <Button
                    size="sm"
                    variant="secondary"
                    onClick={() => {
                      setCaseId(c.id)
                      setOpen(true)
                    }}
                  >
                    Decide
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      {/* Decisions already on record */}
      <h2 className="font-heading text-foreground mb-3 text-base font-bold">Decision register</h2>
      {decisions.error && <ErrorState message={decisions.error} />}
      {decisions.loading && <LoadingRows rows={4} />}
      {decisions.data?.length === 0 && <EmptyState message="No decisions recorded yet." />}
      {decisions.data && decisions.data.length > 0 && (
        <Card className="overflow-hidden p-0">
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/50 hover:bg-muted/50">
                <TableHead>Applicant</TableHead>
                <TableHead>Receipt</TableHead>
                <TableHead>Outcome</TableHead>
                <TableHead className="hidden @2xl:table-cell">Decided</TableHead>
                <TableHead className="hidden @3xl:table-cell">Officer</TableHead>
                <TableHead className="hidden @4xl:table-cell">Rationale</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {decisions.data.map((d) => (
                <TableRow key={d.id}>
                  <TableCell>
                    <Link
                      to={`/cases/${d.caseId}`}
                      className="text-foreground hover:text-primary font-medium underline-offset-4 hover:underline"
                    >
                      {d.applicantName}
                    </Link>
                  </TableCell>
                  <TableCell className="text-muted-foreground font-mono text-xs">
                    {d.receiptNumber}
                  </TableCell>
                  <TableCell>
                    <OutcomeBadge outcome={d.outcome} />
                  </TableCell>
                  <TableCell className="text-muted-foreground hidden text-sm @2xl:table-cell">
                    {new Date(d.decidedOn).toLocaleDateString()}
                  </TableCell>
                  <TableCell className="text-muted-foreground hidden text-sm @3xl:table-cell">
                    {d.decidedBy}
                  </TableCell>
                  <TableCell className="text-muted-foreground hidden max-w-xs truncate text-sm @4xl:table-cell">
                    {d.denialReasonCode ? `[${d.denialReasonCode}] ` : ''}
                    {d.rationale}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Card>
      )}

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="font-heading">Record a decision</DialogTitle>
            <DialogDescription>
              This writes to the case's permanent audit trail and cannot be edited afterwards.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Case</Label>
              <Select
                value={caseId ? String(caseId) : ''}
                onValueChange={(v) => setCaseId(Number(v))}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select a case awaiting decision" />
                </SelectTrigger>
                <SelectContent>
                  {ripe.data?.items.map((c) => (
                    <SelectItem key={c.id} value={String(c.id)}>
                      {c.applicantName} — {c.receiptNumber}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Outcome</Label>
              <Select value={outcome} onValueChange={(v) => setOutcome(v as DecisionOutcome)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {DECISION_OUTCOMES.map((o) => (
                    <SelectItem key={o} value={o}>
                      {o}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {outcome === 'Denied' && (
              <div className="space-y-2">
                <Label htmlFor="code">Denial reason code</Label>
                <Input
                  id="code"
                  value={denialCode}
                  onChange={(e) => setDenialCode(e.target.value)}
                  placeholder="e.g. 316(a) — continuous residence"
                />
              </div>
            )}

            <div className="space-y-2">
              <Label htmlFor="rationale">Rationale</Label>
              <Input
                id="rationale"
                value={rationale}
                onChange={(e) => setRationale(e.target.value)}
                placeholder="Basis for the determination"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={!caseId || busy}>
              Record decision
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
