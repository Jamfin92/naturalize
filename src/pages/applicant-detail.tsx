import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, FileDown, Pencil, Trash2 } from 'lucide-react'
import { toast } from 'sonner'

import { EmptyState, ErrorState, LoadingRows, PageHeader } from '@/components/page'
import { StatusBadge } from '@/components/status-badge'
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { api } from '@/lib/api'
import { useAsync } from '@/lib/use-async'

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div className="space-y-0.5">
      <dt className="text-muted-foreground text-xs tracking-wide uppercase">{label}</dt>
      <dd className="text-foreground text-sm">{value}</dd>
    </div>
  )
}

export function ApplicantDetailPage() {
  const { id } = useParams<{ id: string }>()
  const applicantId = Number(id)
  const navigate = useNavigate()

  const [confirming, setConfirming] = useState(false)
  const [withdrawing, setWithdrawing] = useState(false)

  const applicant = useAsync(() => api.applicants.get(applicantId), [applicantId])
  const cases = useAsync(() => api.applicants.cases(applicantId), [applicantId])
  const history = useAsync(() => api.applicants.history(applicantId), [applicantId])

  if (applicant.error) return <ErrorState message={applicant.error} />
  if (applicant.loading || !applicant.data) return <LoadingRows rows={5} />

  const a = applicant.data

  const withdraw = async () => {
    setWithdrawing(true)
    try {
      await api.applicants.remove(applicantId)
      toast.success(`${a.fullName} withdrawn from the register. The record is retained.`)
      navigate('/applicants')
    } catch (e: unknown) {
      toast.error(e instanceof Error ? e.message : 'Could not withdraw the record.')
      setWithdrawing(false)
    }
  }

  const downloadCase = async (caseId: number) => {
    try {
      await api.reports.caseRecord(caseId)
    } catch (e: unknown) {
      toast.error(e instanceof Error ? e.message : 'Could not generate the case record.')
    }
  }

  return (
    <>
      <PageHeader
        title={a.fullName}
        description={`A-Number ${a.alienNumber}`}
        actions={
          <div className="flex gap-2">
            <Button variant="outline" size="sm" asChild>
              <Link to="/applicants">
                <ArrowLeft className="size-4" />
                All applicants
              </Link>
            </Button>
            <Button variant="outline" size="sm" asChild>
              <Link to={`/applicants/${applicantId}/edit`}>
                <Pencil className="size-4" />
                Edit
              </Link>
            </Button>
            <Button variant="destructive" size="sm" onClick={() => setConfirming(true)}>
              <Trash2 className="size-4" />
              Withdraw
            </Button>
          </div>
        }
      />

      <div className="grid gap-6 @4xl:grid-cols-[1fr_1.4fr]">
        <Card>
          <CardContent className="p-5">
            <h2 className="font-heading text-foreground mb-4 text-base font-bold">
              Personal particulars
            </h2>
            <dl className="grid grid-cols-2 gap-4">
              <Field label="Date of birth" value={new Date(a.dateOfBirth).toLocaleDateString()} />
              <Field label="Country of birth" value={a.countryOfBirth} />
              <Field label="Nationality" value={a.nationality} />
              <Field
                label="LPR since"
                value={new Date(a.lawfulPermanentResidentSince).toLocaleDateString()}
              />
              <div className="col-span-2">
                <Field
                  label="Residence"
                  value={`${a.addressLine}, ${a.city}, ${a.state} ${a.postalCode}`}
                />
              </div>
              <Field label="Email" value={a.email} />
              <Field label="Phone" value={a.phone} />
            </dl>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-5">
            <h2 className="font-heading text-foreground mb-4 text-base font-bold">Case history</h2>

            {cases.error && <ErrorState message={cases.error} />}
            {cases.loading && <LoadingRows rows={2} />}
            {cases.data?.length === 0 && (
              <EmptyState message="No naturalization case has been filed for this applicant." />
            )}

            {cases.data && cases.data.length > 0 && (
              <Table>
                <TableHeader>
                  <TableRow className="hover:bg-transparent">
                    <TableHead>Receipt</TableHead>
                    <TableHead>Filed</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Record</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {cases.data.map((c) => (
                    <TableRow key={c.id}>
                      <TableCell className="text-foreground font-mono text-xs font-medium">
                        {c.receiptNumber}
                      </TableCell>
                      <TableCell className="text-muted-foreground text-sm">
                        {new Date(c.filedOn).toLocaleDateString()}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={c.status} />
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => downloadCase(c.id)}
                          aria-label={`Download case record for ${c.receiptNumber}`}
                        >
                          <FileDown className="size-4" />
                          PDF
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>

      {/*
       * The record's own audit trail. This is what makes the soft delete legible:
       * withdraw a record and this list grows an entry rather than disappearing
       * along with everything else about the person.
       */}
      <Card className="mt-6">
        <CardContent className="p-5">
          <h2 className="font-heading text-foreground mb-1 text-base font-bold">Record history</h2>
          <p className="text-muted-foreground mb-4 text-sm">
            Every change to this record, and the officer who made it. Append-only — withdrawing a
            record adds to this trail rather than erasing it.
          </p>

          {history.error && <ErrorState message={history.error} />}
          {history.loading && <LoadingRows rows={2} />}
          {history.data?.length === 0 && (
            <EmptyState message="No changes recorded since this record was seeded." />
          )}

          {history.data && history.data.length > 0 && (
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead>When</TableHead>
                  <TableHead>Action</TableHead>
                  <TableHead>Officer</TableHead>
                  <TableHead>Detail</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.data.map((e) => (
                  <TableRow key={e.id}>
                    <TableCell className="text-muted-foreground text-sm whitespace-nowrap">
                      {new Date(e.occurredAt).toLocaleString()}
                    </TableCell>
                    <TableCell className="text-foreground text-sm font-medium">{e.action}</TableCell>
                    <TableCell className="text-sm">{e.actor}</TableCell>
                    <TableCell className="text-muted-foreground text-sm">{e.summary}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Dialog open={confirming} onOpenChange={setConfirming}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Withdraw {a.fullName} from the register?</DialogTitle>
            <DialogDescription asChild>
              <div className="space-y-2 text-sm">
                <p>
                  The record will no longer appear in the register or in reports. It is{' '}
                  <strong>not destroyed</strong>: the applicant, their cases, their evidence and
                  their full audit trail all remain on file, and the withdrawal itself is recorded
                  against your name.
                </p>
                <p>An administrator can restore the record afterwards.</p>
              </div>
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setConfirming(false)}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={withdraw} disabled={withdrawing}>
              {withdrawing ? 'Withdrawing…' : 'Withdraw record'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
