import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Search } from 'lucide-react'

import { EmptyState, ErrorState, LoadingRows, PageHeader } from '@/components/page'
import { StatusBadge } from '@/components/status-badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
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
import { useAsync } from '@/lib/use-async'
import { CASE_STATUSES, STATUS_LABELS, TERMINAL_STATUSES, type CaseStatus } from '@/lib/types'

const PAGE_SIZE = 15
const ALL = '__all__'

export function CasesPage() {
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<string>(ALL)
  const [page, setPage] = useState(1)

  const { data, error, loading } = useAsync(
    () =>
      api.cases.list({
        q: search,
        status: status === ALL ? undefined : (status as CaseStatus),
        page,
        pageSize: PAGE_SIZE,
      }),
    [search, status, page],
  )

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    setPage(1)
    setSearch(q)
  }

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1

  return (
    <>
      <PageHeader
        title="Cases"
        description="Every N-400 on file, from receipt through the oath."
        actions={
          <form onSubmit={submit} className="flex flex-wrap gap-2">
            <Select
              value={status}
              onValueChange={(v) => {
                setPage(1)
                setStatus(v)
              }}
            >
              <SelectTrigger className="w-48">
                <SelectValue placeholder="All statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>All statuses</SelectItem>
                {CASE_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {STATUS_LABELS[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <div className="relative">
              <Search className="text-muted-foreground absolute top-1/2 left-2.5 size-4 -translate-y-1/2" />
              <Input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Receipt or name…"
                className="w-52 pl-8"
              />
            </div>
            <Button type="submit" variant="secondary">
              Search
            </Button>
          </form>
        }
      />

      {error && <ErrorState message={error} />}
      {loading && <LoadingRows />}

      {data && data.items.length === 0 && <EmptyState message="No cases match these filters." />}

      {data && data.items.length > 0 && (
        <>
          <Card className="overflow-hidden p-0">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/50 hover:bg-muted/50">
                  <TableHead>Receipt</TableHead>
                  <TableHead>Applicant</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="hidden @2xl:table-cell">Filed</TableHead>
                  <TableHead className="hidden @3xl:table-cell">Field office</TableHead>
                  <TableHead className="text-right">Days open</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell>
                      <Link
                        to={`/cases/${c.id}`}
                        className="text-foreground hover:text-primary font-mono text-xs font-medium underline-offset-4 hover:underline"
                      >
                        {c.receiptNumber}
                      </Link>
                    </TableCell>
                    <TableCell className="font-medium">{c.applicantName}</TableCell>
                    <TableCell>
                      <StatusBadge status={c.status} />
                    </TableCell>
                    <TableCell className="text-muted-foreground hidden text-sm @2xl:table-cell">
                      {new Date(c.filedOn).toLocaleDateString()}
                    </TableCell>
                    <TableCell className="text-muted-foreground hidden text-sm @3xl:table-cell">
                      {c.fieldOffice}
                    </TableCell>
                    {/* A day count on a closed case is noise — it stopped ticking. */}
                    <TableCell className="text-right font-mono text-xs tabular-nums">
                      {TERMINAL_STATUSES.includes(c.status) ? (
                        <span className="text-muted-foreground">—</span>
                      ) : (
                        c.daysPending
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Card>

          <div className="mt-4 flex items-center justify-between">
            <p className="text-muted-foreground text-sm">
              {data.total} case{data.total === 1 ? '' : 's'} · page {data.page} of {pages}
            </p>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >
                Previous
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= pages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        </>
      )}
    </>
  )
}
