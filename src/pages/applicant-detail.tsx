import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, Pencil, Trash2 } from 'lucide-react'
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
import { useAuth } from '@/lib/auth'
import { canManageApplicants, canWithdrawApplicants } from '@/lib/types'
import { useAsync } from '@/lib/use-async'

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="space-y-0.5">
      <dt className="text-muted-foreground text-xs tracking-wide uppercase">{label}</dt>
      <dd className="text-foreground text-sm">{value || '—'}</dd>
    </div>
  )
}

export function ApplicantDetailPage() {
  const { id } = useParams<{ id: string }>()
  const applicantId = Number(id)
  const navigate = useNavigate()
  const { officer } = useAuth()

  const [confirming, setConfirming] = useState(false)
  const [withdrawing, setWithdrawing] = useState(false)

  const applicant = useAsync(() => api.applicants.get(applicantId), [applicantId])
  const history = useAsync(() => api.applicants.history(applicantId), [applicantId])
  const towns = useAsync(() => api.lookups.towns(), [])
  const countries = useAsync(() => api.lookups.countries(), [])

  if (applicant.error) return <ErrorState message={applicant.error} />
  if (applicant.loading || !applicant.data) return <LoadingRows rows={5} />

  const a = applicant.data
  const townName = towns.data?.find((t) => t.code === a.townCode)?.description ?? a.townCode
  const countryName =
    countries.data?.find((c) => c.code === a.countryCode)?.description ?? a.countryCode

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
            {canManageApplicants(officer) && (
              <Button variant="outline" size="sm" asChild>
                <Link to={`/applicants/${applicantId}/edit`}>
                  <Pencil className="size-4" />
                  Edit
                </Link>
              </Button>
            )}
            {canWithdrawApplicants(officer) && (
              <Button variant="destructive" size="sm" onClick={() => setConfirming(true)}>
                <Trash2 className="size-4" />
                Withdraw
              </Button>
            )}
          </div>
        }
      />

      <div className="grid gap-6 @4xl:grid-cols-2">
        <Card>
          <CardContent className="p-5">
            <div className="mb-4 flex items-center justify-between">
              <h2 className="font-heading text-foreground text-base font-bold">
                Personal particulars
              </h2>
              <StatusBadge status={a.status} />
            </div>
            <dl className="grid grid-cols-2 gap-4">
              <Field label="Date of birth" value={new Date(a.birthDate).toLocaleDateString()} />
              <Field label="Admission date" value={new Date(a.admissionDate).toLocaleDateString()} />
              <Field label="Country" value={countryName} />
              <Field label="Town" value={townName} />
              <div className="col-span-2">
                <Field label="Residence" value={`${a.address1}, ${townName} ${a.zipCode}`} />
              </div>
              <Field label="Email" value={a.email} />
            </dl>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-5">
            <h2 className="font-heading text-foreground mb-4 text-base font-bold">
              Application &amp; decision
            </h2>
            <dl className="grid grid-cols-2 gap-4">
              <Field label="Petition number" value={a.petitionNumber} />
              <Field label="Naturalization number" value={a.naturalizationNumber} />
              <Field
                label="Decision date"
                value={a.decisionDate ? new Date(a.decisionDate).toLocaleDateString() : ''}
              />
              <div />
              <div className="col-span-2">
                <Field label="Decision notes" value={a.decisionNotes} />
              </div>
            </dl>
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
            Every change to this record, and the user who made it. Append-only — withdrawing a
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
                  <TableHead>User</TableHead>
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
                  <strong>not destroyed</strong>: the applicant record and its full audit trail
                  remain on file, and the withdrawal itself is recorded against your name.
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
