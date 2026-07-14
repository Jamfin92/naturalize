import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Search } from 'lucide-react'

import { EmptyState, ErrorState, LoadingRows, PageHeader } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
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

const PAGE_SIZE = 15

export function ApplicantsPage() {
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const { data, error, loading } = useAsync(
    () => api.applicants.list({ q: search, page, pageSize: PAGE_SIZE }),
    [search, page],
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
        title="Applicants"
        description="Everyone on file, whether or not they have an open case."
        actions={
          <form onSubmit={submit} className="flex gap-2">
            <div className="relative">
              <Search className="text-muted-foreground absolute top-1/2 left-2.5 size-4 -translate-y-1/2" />
              <Input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="Name or A-Number…"
                className="w-56 pl-8"
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

      {data && data.items.length === 0 && (
        <EmptyState message={search ? `No applicants match “${search}”.` : 'No applicants yet.'} />
      )}

      {data && data.items.length > 0 && (
        <>
          <Card className="overflow-hidden p-0">
            <Table>
              <TableHeader>
                <TableRow className="bg-muted/50 hover:bg-muted/50">
                  <TableHead>Name</TableHead>
                  <TableHead>A-Number</TableHead>
                  <TableHead className="hidden @2xl:table-cell">Country of birth</TableHead>
                  <TableHead className="hidden @3xl:table-cell">LPR since</TableHead>
                  <TableHead className="hidden @4xl:table-cell">Residence</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell>
                      <Link
                        to={`/applicants/${a.id}`}
                        className="text-foreground hover:text-primary font-medium underline-offset-4 hover:underline"
                      >
                        {a.fullName}
                      </Link>
                    </TableCell>
                    <TableCell className="text-muted-foreground font-mono text-xs">
                      {a.alienNumber}
                    </TableCell>
                    <TableCell className="hidden @2xl:table-cell">{a.countryOfBirth}</TableCell>
                    <TableCell className="text-muted-foreground hidden text-sm @3xl:table-cell">
                      {new Date(a.lawfulPermanentResidentSince).toLocaleDateString()}
                    </TableCell>
                    <TableCell className="text-muted-foreground hidden text-sm @4xl:table-cell">
                      {a.city}, {a.state}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Card>

          <div className="mt-4 flex items-center justify-between">
            <p className="text-muted-foreground text-sm">
              {data.total} applicant{data.total === 1 ? '' : 's'} · page {data.page} of {pages}
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
