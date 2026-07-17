import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { MoreHorizontal, Pencil, Plus, Search, Trash2 } from 'lucide-react'
import { toast } from 'sonner'

import { EmptyState, ErrorState, LoadingRows, PageHeader } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { StatusBadge } from '@/components/status-badge'
import { api } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import { canManageApplicants, canWithdrawApplicants, type Applicant } from '@/lib/types'
import { useAsync } from '@/lib/use-async'

const PAGE_SIZE = 15

export function ApplicantsPage() {
  const { officer } = useAuth()
  const navigate = useNavigate()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const { data, error, loading, reload } = useAsync(
    () => api.applicants.list({ q: search, page, pageSize: PAGE_SIZE }),
    [search, page],
  )

  // Country is stored as a code; resolve it to a name for display. Loaded once.
  const countries = useAsync(() => api.lookups.countries(), [])
  const countryName = (code: string) =>
    countries.data?.find((c) => c.code === code)?.description ?? code

  /*
   * The row actions are the point of this screen for anyone who can change a
   * record: without them the backend role gating (Officer edits, Admin
   * withdraws) has no way in from the register itself — you had to open each
   * person's detail page to find it. We only reserve the actions column when the
   * signed-in officer can actually do at least one thing, so a Viewer sees the
   * same clean table as before.
   */
  const canEdit = canManageApplicants(officer)
  const canWithdraw = canWithdrawApplicants(officer)
  const showActions = canEdit || canWithdraw

  const [pendingWithdraw, setPendingWithdraw] = useState<Applicant | null>(null)
  const [withdrawing, setWithdrawing] = useState(false)

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    setPage(1)
    setSearch(q)
  }

  const withdraw = async () => {
    if (!pendingWithdraw) return
    setWithdrawing(true)
    try {
      await api.applicants.remove(pendingWithdraw.id)
      toast.success(`${pendingWithdraw.fullName} withdrawn from the register. The record is retained.`)
      setPendingWithdraw(null)
      reload()
    } catch (e: unknown) {
      toast.error(e instanceof Error ? e.message : 'Could not withdraw the record.')
    } finally {
      setWithdrawing(false)
    }
  }

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1

  return (
    <>
      <PageHeader
        title="Applicants"
        description="Everyone on file, whether or not they have an open case."
        actions={
          <div className="flex gap-2">
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

            {canEdit && (
              <Button asChild>
                <Link to="/applicants/new">
                  <Plus className="size-4" />
                  New applicant
                </Link>
              </Button>
            )}
          </div>
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
                  <TableHead className="hidden @2xl:table-cell">Country</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="hidden @3xl:table-cell">Admitted</TableHead>
                  {showActions && (
                    <TableHead className="w-0 text-right">
                      <span className="sr-only">Actions</span>
                    </TableHead>
                  )}
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
                    <TableCell className="hidden @2xl:table-cell">{countryName(a.countryCode)}</TableCell>
                    <TableCell>
                      <StatusBadge status={a.status} />
                    </TableCell>
                    <TableCell className="text-muted-foreground hidden text-sm @3xl:table-cell">
                      {new Date(a.admissionDate).toLocaleDateString()}
                    </TableCell>
                    {showActions && (
                      <TableCell className="text-right">
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button
                              variant="ghost"
                              size="icon"
                              aria-label={`Actions for ${a.fullName}`}
                            >
                              <MoreHorizontal className="size-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end" className="w-44">
                            {canEdit && (
                              <DropdownMenuItem onSelect={() => navigate(`/applicants/${a.id}/edit`)}>
                                <Pencil className="size-4" />
                                Edit record
                              </DropdownMenuItem>
                            )}
                            {canWithdraw && (
                              <DropdownMenuItem
                                variant="destructive"
                                onSelect={() => setPendingWithdraw(a)}
                              >
                                <Trash2 className="size-4" />
                                Withdraw record
                              </DropdownMenuItem>
                            )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    )}
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

      {/*
       * Withdrawing from the register is a soft delete — the same one the detail
       * page offers — so the confirmation says as much: the record and its audit
       * trail remain on file and an administrator can restore it. Only users who
       * canWithdraw ever reach this dialog, and the API enforces that regardless.
       */}
      <Dialog open={pendingWithdraw !== null} onOpenChange={(open) => !open && setPendingWithdraw(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Withdraw {pendingWithdraw?.fullName} from the register?</DialogTitle>
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
            <Button variant="ghost" onClick={() => setPendingWithdraw(null)}>
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
