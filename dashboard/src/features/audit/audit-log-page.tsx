import { useState } from 'react'
import { X } from 'lucide-react'

import { StatusBadge } from '@/components/status-badge'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import type { AuditResponse } from '@/api'

import {
  emptyAuditFilters,
  hasActiveFilters,
  useAuditLog,
  useAuditStats,
  type AuditFilters,
} from './queries'
import { describeRange, totalPages } from './paging'

/**
 * The audit log.
 *
 * This page exists to answer a question about the past — who changed this, when,
 * and did it work — so it is built around the entry rather than around browsing.
 * The actor and the outcome are given the most prominent cells, because those are
 * what a reader looks for first.
 */
export function AuditLogPage() {
  const [page, setPage] = useState(1)
  const [filters, setFilters] = useState<AuditFilters>(emptyAuditFilters)
  // Held separately from `filters` so typing does not fire a request per
  // keystroke, and so the applied state is visible next to the input.
  const [draft, setDraft] = useState<AuditFilters>(emptyAuditFilters)

  const { data, isPending, isError, error, refetch, isFetching } = useAuditLog(page, filters)
  const stats = useAuditStats(!hasActiveFilters(filters))

  const apply = () => {
    setFilters(draft)
    // A filter that matches three rows should not leave the reader on page 7 of a
    // list that no longer has seven pages.
    setPage(1)
  }

  const clear = () => {
    setDraft(emptyAuditFilters)
    setFilters(emptyAuditFilters)
    setPage(1)
  }

  const entries = data?.audits ?? []
  const filtered = hasActiveFilters(filters)

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="text-2xl font-semibold">Audit log</h1>
        <p className="text-sm text-muted-foreground">
          Every action the control plane performed, and whether it succeeded. The record
          is written when the action is dispatched, so an entry here means it happened.
        </p>
      </header>

      {!filtered ? (
        <StatsCards
          total={stats.data?.totalCount}
          today={stats.data?.todayCount}
          week={stats.data?.thisWeekCount}
          month={stats.data?.thisMonthCount}
          loading={stats.isPending}
        />
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Find an entry</CardTitle>
          <CardDescription>
            Filters are sent to the server, so the count beside them is the count of
            everything that matches — not of the rows on this page.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="grid gap-3 md:grid-cols-4">
            <FilterField label="Actor" value={draft.actor} onChange={(actor) => setDraft({ ...draft, actor })} placeholder="system" />
            <FilterField label="Action" value={draft.action} onChange={(action) => setDraft({ ...draft, action })} placeholder="CreateService" />
            <FilterField label="Resource type" value={draft.resourceType} onChange={(resourceType) => setDraft({ ...draft, resourceType })} placeholder="Service" />
            <FilterField label="Resource id" value={draft.resourceId} onChange={(resourceId) => setDraft({ ...draft, resourceId })} placeholder="an id" />
          </div>

          <div className="grid gap-3 md:grid-cols-4">
            <FilterField label="From" type="date" value={draft.from} onChange={(from) => setDraft({ ...draft, from })} />
            <FilterField label="To" type="date" value={draft.to} onChange={(to) => setDraft({ ...draft, to })} />
            <div className="flex items-end gap-2 md:col-span-2">
              <Button onClick={apply} disabled={isFetching && !filtered}>
                Apply filters
              </Button>
              {filtered ? (
                <Button variant="ghost" onClick={clear}>
                  <X aria-hidden="true" className="size-4" />
                  Clear
                </Button>
              ) : null}
            </div>
          </div>
        </CardContent>
      </Card>

      {isPending ? (
        <div className="space-y-2">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-40 w-full" />
        </div>
      ) : isError || !data ? (
        <Alert variant="destructive">
          <AlertTitle>Could not load the audit log</AlertTitle>
          <AlertDescription>
            {error instanceof Error ? error.message : String(error ?? 'Nothing was returned')}
            <Button
              variant="outline"
              size="sm"
              className="ml-3"
              onClick={() => {
                refetch()
              }}
            >
              Try again
            </Button>
          </AlertDescription>
        </Alert>
      ) : entries.length === 0 ? (
        <Card>
          <CardContent className="pt-6 text-sm text-muted-foreground">
            {filtered
              ? 'No entries match those filters. Clearing them will show everything recorded.'
              : 'Nothing has been recorded yet. Every action the control plane performs writes an entry here.'}
          </CardContent>
        </Card>
      ) : (
        <>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>When</TableHead>
                  <TableHead>Actor</TableHead>
                  <TableHead>Action</TableHead>
                  <TableHead>Resource</TableHead>
                  <TableHead>Result</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {entries.map((entry) => (
                  <EntryRow key={entry.id} entry={entry} />
                ))}
              </TableBody>
            </Table>
          </div>

          <div className="flex items-center justify-between text-sm">
            <p className="text-muted-foreground">
              {describeRange(data.page, data.pageSize, data.totalCount)}
              {filtered ? ' matching your filters' : ''}
            </p>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={data.page <= 1}
                onClick={() => setPage((current) => Math.max(1, current - 1))}
              >
                Previous
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={data.page >= totalPages(data.totalCount, data.pageSize)}
                onClick={() => setPage((current) => current + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

function EntryRow({ entry }: { entry: AuditResponse }) {
  return (
    <TableRow>
      <TableCell className="whitespace-nowrap text-muted-foreground">
        {formatTimestamp(entry.timestamp)}
      </TableCell>
      <TableCell className="font-medium">{entry.actor}</TableCell>
      <TableCell>
        <code className="font-mono text-xs">{entry.action}</code>
      </TableCell>
      <TableCell>
        <div className="text-sm">{entry.resourceType}</div>
        <code className="font-mono text-xs text-muted-foreground" title={entry.resourceId}>
          {entry.resourceId.slice(0, 8)}
        </code>
      </TableCell>
      <TableCell>
        <StatusBadge status={entry.result} />
      </TableCell>
    </TableRow>
  )
}

function StatsCards({
  total,
  today,
  week,
  month,
  loading,
}: {
  total: number | undefined
  today: number | undefined
  week: number | undefined
  month: number | undefined
  loading: boolean
}) {
  const cards = [
    { label: 'All time', value: total },
    { label: 'Today', value: today },
    { label: 'This week', value: week },
    { label: 'This month', value: month },
  ]

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {cards.map((card) => (
        <Card key={card.label}>
          <CardHeader className="pb-2">
            <CardDescription>{card.label}</CardDescription>
            <CardTitle className="text-2xl">
              {loading ? <Skeleton className="h-8 w-16" /> : (card.value ?? '—')}
            </CardTitle>
          </CardHeader>
        </Card>
      ))}
    </div>
  )
}

function FilterField({
  label,
  value,
  onChange,
  placeholder,
  type = 'text',
}: {
  label: string
  value: string | undefined
  onChange: (value: string) => void
  placeholder?: string
  type?: string
}) {
  const id = `audit-filter-${label.toLowerCase().replace(/\s+/g, '-')}`

  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type={type}
        value={value ?? ''}
        placeholder={placeholder}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  )
}

function formatTimestamp(value: string): string {
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}
