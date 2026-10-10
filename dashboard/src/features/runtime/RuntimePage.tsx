import { useState } from 'react'
import { X } from 'lucide-react'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
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
import { useRuntimeGateways, useRuntimeGateway, useReconcile, formatTimestamp, gatewayStatusTone, shortHash } from './queries'
import type { RuntimeStatusResponse } from '@/api'

/** The current state of all registered gateways. */
export function RuntimePage() {
  const [viewing, setViewing] = useState<string | null>(null)

  const result = useRuntimeGateways()
  const { data, isPending, isError, error, refetch } = result
  const gateways = (data?.gateways ?? []) as RuntimeStatusResponse[]
  const reconcile = useReconcile()

  if (isPending) return <RuntimeSkeleton />
  if (isError || !data) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Could not load gateways</AlertTitle>
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
          <h1 className="text-2xl font-semibold">Runtime Gateways</h1>
          <p className="text-sm text-muted-foreground">
            Registered gateways, their health, and configuration sync status.
          </p>
        </div>

        <Button variant="outline" onClick={() => refetch()} disabled={isPending}>
          <span className="size-4 mr-2" aria-hidden="true">⟳</span>
          Refresh
        </Button>
      </header>

      {gateways.length === 0 ? (
        <Card>
          <CardContent className="py-6 text-center text-sm text-muted-foreground">
            No gateways registered. Register a gateway from the Gateways page to
            start receiving heartbeats and configuration updates.
          </CardContent>
        </Card>
      ) : (
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Gateway</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Current Version</TableHead>
                <TableHead>Target Version</TableHead>
                <TableHead>Last Heartbeat</TableHead>
                <TableHead>Last Sync</TableHead>
                <TableHead className="text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {gateways.map((gw: RuntimeStatusResponse) => {
                const gatewayId = gw.gatewayId
                return (
                  <RuntimeRow
                    key={gatewayId}
                    gateway={gw}
                    isReconciling={reconcile.isPending && reconcile.variables?.gatewayId === gatewayId}
                    onView={setViewing}
                  />
                )
              })}
            </TableBody>
          </Table>
        </div>
      )}

      {viewing && (
        <GatewayDetailSheet
          gatewayId={viewing}
          onClose={() => setViewing(null)}
        />
      )}
    </div>
  )
}

function RuntimeRow({
  gateway,
  isReconciling,
  onView,
}: {
  gateway: RuntimeStatusResponse
  isReconciling: boolean
  onView: (id: string) => void
}) {
  const { tone } = gatewayStatusTone(gateway.status)
  const reconcile = useReconcile()

  return (
    <TableRow className="cursor-pointer hover:bg-accent" onClick={() => onView(gateway.gatewayId)}>
      <TableCell>
        <div className="flex items-center gap-2">
          <span className="size-4 text-muted-foreground" aria-hidden="true">⧉</span>
          <span className="font-mono text-xs">{shortHash(gateway.gatewayId)}</span>
        </div>
      </TableCell>
      <TableCell>
        <Badge variant={tone === 'ok' ? 'default' : tone === 'bad' ? 'destructive' : tone === 'warn' ? 'secondary' : 'outline'}>
          {gateway.status}
        </Badge>
      </TableCell>
      <TableCell className="text-sm font-mono">
        {gateway.currentVersion ?? '—'}
      </TableCell>
      <TableCell className="text-sm">
        {gateway.targetVersion !== null ? (
          gateway.targetVersion !== gateway.currentVersion ? (
            <span className="text-primary font-medium">#{gateway.targetVersion}</span>
          ) : (
            <span className="text-muted-foreground">#{gateway.targetVersion}</span>
          )
        ) : (
          <span className="text-muted-foreground">—</span>
        )}
      </TableCell>
      <TableCell className="text-sm text-muted-foreground">
        {formatTimestamp(gateway.lastHeartbeat)}
      </TableCell>
      <TableCell className="text-sm text-muted-foreground">
        {formatTimestamp(gateway.lastSynchronized)}
      </TableCell>
      <TableCell className="text-right">
        <Button
          variant="outline"
          size="sm"
          onClick={(e) => {
            e.stopPropagation()
            reconcile.mutate(gateway.gatewayId)
          }}
          disabled={isReconciling}
          className="w-full sm:w-auto"
        >
          {isReconciling ? 'Reconciling…' : 'Reconcile'}
        </Button>
      </TableCell>
    </TableRow>
  )
}

/** Detail view for a single gateway. */
function GatewayDetailSheet({
  gatewayId,
  onClose,
}: {
  gatewayId: string
  onClose: () => void
}) {
  const { data: gateway, isPending } = useRuntimeGateway(gatewayId, true)

  if (isPending || !gateway) return <GatewayDetailSkeleton />

  const reconcile = useReconcile()

  return (
    <Sheet open onOpenChange={(open) => !open && onClose()}>
      <SheetContent className="w-full overflow-y-auto sm:max-w-3xl">
        <SheetHeader>
          <SheetTitle>Gateway #{gateway.gatewayId.slice(0, 8)}</SheetTitle>
          <SheetDescription>
            Status: {gateway.status} · Last heartbeat: {formatTimestamp(gateway.lastHeartbeat)}
          </SheetDescription>
        </SheetHeader>

        <div className="space-y-4 px-4 pb-6">
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant={gatewayStatusTone(gateway.status).tone === 'ok' ? 'default' : 'destructive'}>
              {gateway.status}
            </Badge>
            {gateway.capabilities.length > 0 && (
              <Badge variant="outline">{gateway.capabilities.join(', ')}</Badge>
            )}
          </div>

          <Separator />

          <Card>
            <CardHeader>
              <CardTitle>Version sync</CardTitle>
              <CardDescription>Current vs target configuration version</CardDescription>
            </CardHeader>
            <CardContent>
              <dl className="grid gap-1 sm:grid-cols-[110px_1fr] text-sm">
                <dt>Current version</dt>
                <dd>{gateway.currentVersion ?? '—'}</dd>
                <dt>Target version</dt>
                <dd>{gateway.targetVersion ?? '—'}</dd>
                <dt>Last config applied</dt>
                <dd>{formatTimestamp(gateway.lastConfigApplied)}</dd>
                <dt>Last synchronized</dt>
                <dd>{formatTimestamp(gateway.lastSynchronized)}</dd>
                <dt>Last heartbeat</dt>
                <dd>{formatTimestamp(gateway.lastHeartbeat)}</dd>
              </dl>
            </CardContent>
          </Card>

          {gateway.capabilities.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Capabilities</CardTitle>
                <CardDescription>Features this gateway supports</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="flex flex-wrap gap-2">
                  {gateway.capabilities.map((cap) => (
                    <Badge key={cap} variant="outline">{cap}</Badge>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}

          {gateway.activeRoutes.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Active routes</CardTitle>
                <CardDescription>Routes currently handled by this gateway</CardDescription>
              </CardHeader>
              <CardContent>
                <ul className="space-y-1 text-sm">
                  {gateway.activeRoutes.map((route) => (
                    <li key={route} className="flex items-center gap-2 text-xs">
                      <span className="size-3.5 text-muted-foreground" aria-hidden="true">⟳</span>
                      <code className="font-mono text-muted-foreground">{route}</code>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          )}

          {Object.keys(gateway.runtimeInfo).length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Runtime info</CardTitle>
                <CardDescription>Additional metadata reported by the gateway</CardDescription>
              </CardHeader>
              <CardContent>
                <dl className="grid gap-1 sm:grid-cols-[120px_1fr] text-xs text-muted-foreground">
                  {Object.entries(gateway.runtimeInfo).map(([key, value]) => (
                    <div key={key} className="flex gap-2">
                      <dt className="font-mono">{key}</dt>
                      <dd className="truncate">{value}</dd>
                    </div>
                  ))}
                </dl>
              </CardContent>
            </Card>
          )}

          <Separator />

          <div className="flex gap-2">
            <Button
              variant="outline"
              onClick={() => reconcile.mutate(gateway.gatewayId)}
              disabled={reconcile.isPending}
            >
              Reconcile now
            </Button>
            <Button variant="destructive" onClick={onClose}>
              <X className="size-4 mr-2" aria-hidden="true" />
              Close
            </Button>
          </div>
        </div>
      </SheetContent>
    </Sheet>
  )
}

function GatewayDetailSkeleton() {
  return (
    <Sheet open>
      <SheetContent className="w-full overflow-y-auto sm:max-w-3xl">
        <SheetHeader>
          <SheetTitle>
            <div className="animate-pulse h-6 w-48 bg-muted rounded" />
          </SheetTitle>
        </SheetHeader>
        <div className="space-y-4 px-4 pb-6">
          <div className="animate-pulse h-8 w-48 bg-muted rounded" />
          <Separator />
          <div className="animate-pulse h-32 w-full bg-muted rounded" />
        </div>
      </SheetContent>
    </Sheet>
  )
}

function RuntimeSkeleton() {
  return (
    <div className="space-y-6">
      <div className="animate-pulse h-8 w-64 bg-muted rounded" />
      <div className="animate-pulse h-16 w-full bg-muted rounded" />
      <div className="animate-pulse h-64 w-full bg-muted rounded" />
    </div>
  )
}