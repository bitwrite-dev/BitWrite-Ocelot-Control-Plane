import { useState } from 'react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/sheet'
import {
  usePublications,
  useCurrentPublication,
  usePublicationDetail,
  publicationStatus,
  shortHash,
  formatTimestamp,
} from './queries'

const PAGE_SIZE = 20

/** The current publication and the list of past ones. */
export function PublicationsPage() {
  const [viewing, setViewing] = useState<string | null>(null)

  const { data, isPending, isError, error, refetch } = usePublications(1)
  const { data: current } = useCurrentPublication()

  if (isPending) return <PublicationsSkeleton />
  if (isError || !data) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Could not load publications</AlertTitle>
        <AlertDescription>
          {error instanceof Error ? error.message : String(error ?? 'Nothing was returned')}
          <Button
            variant="outline"
            size="sm"
            className="ml-3"
            onClick={() => refetch()}
          >
            Try again
          </Button>
        </AlertDescription>
      </Alert>
    )
  }

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold">Publications</h1>
          <p className="text-sm text-muted-foreground">
            History of which snapshots were published to which gateways, and the
            outcome of each deployment.
          </p>
        </div>
      </header>

      <CurrentPublicationCard current={current ?? null} />

      {data.publications.length === 0 ? (
        <Card>
          <CardContent className="pt-6 text-sm text-muted-foreground">
            No publications yet. Publish a snapshot from the Snapshots page to
            create the first publication.
          </CardContent>
        </Card>
      ) : (
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ID</TableHead>
                <TableHead>Snapshot</TableHead>
                <TableHead>Initiated by</TableHead>
                <TableHead>Started</TableHead>
                <TableHead>Completed</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Gateways</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.publications.map((publication) => (
                <PublicationRow
                  key={publication.id}
                  publication={publication}
                  onClick={() => setViewing(publication.id)}
                />
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Pagination
        page={1}
        pageSize={20}
        totalCount={data.totalCount}
        onPage={() => {}}
      />

      {viewing && (
        <PublicationDetailSheet
          publicationId={viewing}
          onClose={() => setViewing(null)}
        />
      )}
    </div>
  )
}

/** The currently published snapshot, if any. */
function CurrentPublicationCard({ current }: { current: { current: { snapshotVersion: number } | null } | null }) {
  if (!current?.current) {
    return (
      <Card className="border-dashed">
        <CardContent className="py-6 text-center text-sm text-muted-foreground">
          No snapshot is currently published. Gateways are running their default
          configuration.
        </CardContent>
      </Card>
    )
  }

  return (
    <Card className="border-primary">
      <CardContent className="py-4">
        <div className="flex flex-wrap items-center gap-3">
          <span className="text-sm font-medium text-primary">Currently published:</span>
          <Badge variant="default" className="text-sm">
            Snapshot #{current.current.snapshotVersion}
          </Badge>
          <p className="text-sm text-muted-foreground ml-auto">
            Gateways are running this snapshot.
          </p>
        </div>
      </CardContent>
    </Card>
  )
}

function PublicationRow({
  publication,
  onClick,
}: {
  publication: {
    id: string
    snapshotVersion: number
    status: string
    initiatedBy: string
    startedAt: string
    completedAt: string | null
    failureReason: string | null
    gatewayStates: { gatewayId: string; status: string }[]
  }
  onClick: () => void
}) {
  const { label, tone } = publicationStatus(publication)

  return (
    <TableRow className="cursor-pointer hover:bg-accent" onClick={onClick}>
      <TableCell className="font-mono text-xs">{shortHash(publication.id)}</TableCell>
      <TableCell>
        <Badge variant="outline">#{publication.snapshotVersion}</Badge>
      </TableCell>
      <TableCell className="text-sm">{publication.initiatedBy}</TableCell>
      <TableCell className="text-sm text-muted-foreground">
        {formatTimestamp(publication.startedAt)}
      </TableCell>
      <TableCell className="text-sm text-muted-foreground">
        {formatTimestamp(publication.completedAt)}
      </TableCell>
      <TableCell>
        <Badge variant={tone === 'ok' ? 'default' : tone === 'bad' ? 'destructive' : 'secondary'}>
          {label}
        </Badge>
      </TableCell>
      <TableCell className="text-right text-sm text-muted-foreground">
        {publication.gatewayStates.length} gateway{publication.gatewayStates.length !== 1 ? 's' : ''}
      </TableCell>
    </TableRow>
  )
}

/** Detail view for a single publication. */
function PublicationDetailSheet({
  publicationId,
  onClose,
}: {
  publicationId: string
  onClose: () => void
}) {
  const { data: publication, isPending } = usePublicationDetail(publicationId, !!publicationId)

  if (isPending || !publication) return <PublicationDetailSkeleton />

  return (
    <Sheet open onOpenChange={(open) => !open && onClose()}>
      <SheetContent className="w-full overflow-y-auto sm:max-w-3xl">
        <SheetHeader>
          <SheetTitle>Publication #{publication.id.slice(0, 8)}</SheetTitle>
          <SheetDescription>
            Snapshot #{publication.snapshotVersion} · {formatTimestamp(publication.startedAt)}
          </SheetDescription>
        </SheetHeader>

        <div className="space-y-4 px-4 pb-6">
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant={publicationStatus(publication).tone === 'ok' ? 'default' : 'destructive'}>
              {publicationStatus(publication).label}
            </Badge>
            <Badge variant="outline">Snapshot #{publication.snapshotVersion}</Badge>
          </div>

          <Separator />

          <Card>
            <CardHeader>
              <CardTitle>Gateway deployment</CardTitle>
              <CardDescription>What gateways are running this publication</CardDescription>
            </CardHeader>
            <CardContent>
              <dl className="grid gap-1 sm:grid-cols-[110px_1fr] text-sm">
                <dt>Publication ID</dt>
                <dd className="truncate font-mono text-xs">{publication.id}</dd>
                <dt>Snapshot</dt>
                <dd>#{publication.snapshotVersion}</dd>
                <dt>Status</dt>
                <dd>
                  <Badge variant={publicationStatus(publication).tone === 'ok' ? 'default' : 'destructive'}>
                    {publicationStatus(publication).label}
                  </Badge>
                </dd>
                <dt>Initiated by</dt>
                <dd>{publication.initiatedBy}</dd>
                <dt>Started</dt>
                <dd>{formatTimestamp(publication.startedAt)}</dd>
                <dt>Completed</dt>
                <dd>{formatTimestamp(publication.completedAt)}</dd>
                {publication.failureReason && (
                  <>
                    <dt>Failure reason</dt>
                    <dd className="text-destructive">{publication.failureReason}</dd>
                  </>
                )}
              </dl>
            </CardContent>
          </Card>

          <Separator />

          <Card>
            <CardHeader>
              <CardTitle>Gateway states</CardTitle>
              <CardDescription>Individual gateway deployment results</CardDescription>
            </CardHeader>
            <CardContent>
              {publication.gatewayStates.length === 0 ? (
                <p className="text-xs text-muted-foreground">No gateway states recorded.</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Gateway</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {publication.gatewayStates.map((gw: { gatewayId: string; status: string }) => (
                      <TableRow key={gw.gatewayId}>
                        <TableCell className="font-mono text-xs">{shortHash(gw.gatewayId)}</TableCell>
                        <TableCell>
                          <Badge variant={gw.status.toLowerCase() === 'completed' ? 'default' : 'secondary'}>
                            {gw.status}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          {publication.failureReason && (
            <Alert variant="destructive">
              <AlertTitle>Publication failed</AlertTitle>
              <AlertDescription>{publication.failureReason}</AlertDescription>
            </Alert>
          )}
        </div>
      </SheetContent>
    </Sheet>
  )
}

function PublicationDetailSkeleton() {
  return (
    <Sheet open>
      <SheetContent className="w-full overflow-y-auto sm:max-w-3xl">
        <SheetHeader>
          <SheetTitle>
            <Skeleton className="h-6 w-48" />
          </SheetTitle>
        </SheetHeader>
        <div className="space-y-4 px-4 pb-6">
          <Skeleton className="h-8 w-48" />
          <Separator />
          <Skeleton className="h-32 w-full" />
        </div>
      </SheetContent>
    </Sheet>
  )
}

function Pagination({
  page,
  pageSize,
  totalCount,
  onPage,
}: {
  page: number
  pageSize: number
  totalCount: number
  onPage: (page: number) => void
}) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  if (totalPages <= 1) return null

  return (
    <div className="flex items-center justify-between text-sm">
      <p className="text-muted-foreground">
        Showing {PAGE_SIZE * (page - 1) + 1}–
        {Math.min(PAGE_SIZE * page, totalCount)} of {totalCount} publications
      </p>
      <div className="flex gap-2">
        <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>
          Previous
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={page >= totalPages}
          onClick={() => onPage(page + 1)}
        >
          Next
        </Button>
      </div>
    </div>
  )
}

function PublicationsSkeleton() {
  return (
    <div className="space-y-6">
      <Skeleton className="h-8 w-64" />
      <Skeleton className="h-16 w-full" />
      <Skeleton className="h-64 w-full" />
    </div>
  )
}