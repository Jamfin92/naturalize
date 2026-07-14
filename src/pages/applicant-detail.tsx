import { Link, useParams } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'

import { EmptyState, ErrorState, LoadingRows, PageHeader } from '@/components/page'
import { StatusBadge } from '@/components/status-badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
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

  const applicant = useAsync(() => api.applicants.get(applicantId), [applicantId])
  const cases = useAsync(() => api.applicants.cases(applicantId), [applicantId])

  if (applicant.error) return <ErrorState message={applicant.error} />
  if (applicant.loading || !applicant.data) return <LoadingRows rows={5} />

  const a = applicant.data

  return (
    <>
      <PageHeader
        title={a.fullName}
        description={`A-Number ${a.alienNumber}`}
        actions={
          <Button variant="outline" size="sm" asChild>
            <Link to="/applicants">
              <ArrowLeft className="size-4" />
              All applicants
            </Link>
          </Button>
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
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {cases.data.map((c) => (
                    <TableRow key={c.id}>
                      <TableCell>
                        <Link
                          to={`/cases/${c.id}`}
                          className="text-foreground hover:text-primary font-mono text-xs font-medium underline-offset-4 hover:underline"
                        >
                          {c.receiptNumber}
                        </Link>
                      </TableCell>
                      <TableCell className="text-muted-foreground text-sm">
                        {new Date(c.filedOn).toLocaleDateString()}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={c.status} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>
    </>
  )
}
