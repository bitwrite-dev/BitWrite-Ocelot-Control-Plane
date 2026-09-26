import { Boxes, Camera, Route as RouteIcon, Server } from 'lucide-react'

import { PageHeader } from '@/components/app-layout'
import { EmptyState, ErrorState, LoadingState } from '@/components/page-state'
import { StatusBadge } from '@/components/status-badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  useCurrentPublication,
  useGateways,
  useRouteCount,
  useServiceCount,
  useSnapshotHistory,
  toDisplayError,
} from './queries'

/** A single headline number. */
function StatCard({
  title,
  value,
  description,
  icon: Icon,
}: {
  title: string
  value: string | number
  description?: string
  icon: typeof RouteIcon
}) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-2 space-y-0 pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
        <Icon className="size-4 text-muted-foreground" aria-hidden="true" />
      </CardHeader>
      <CardContent>
        <p className="font-heading text-3xl font-semibold tabular-nums">{value}</p>
        {description ? (
          <p className="mt-1 text-xs text-muted-foreground">{description}</p>
        ) : null}
      </CardContent>
    </Card>
  )
}

function GatewayCard({
  gateway,
}: {
  gateway: {
    gatewayId: string
    status: string
    currentVersion: number | null
    targetVersion: number | null
    lastHeartbeat: string | null
    lastConfigApplied: string | null
    capabilities: string[]
  }
}) {
  const behind = gateway.currentVersion !== gateway.targetVersion && gateway.targetVersion !== null

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <CardTitle className="truncate font-mono text-sm font-medium">{gateway.gatewayId}</CardTitle>
            <CardDescription className="mt-1">
              {gateway.lastHeartbeat ? (
                <>Last heartbeat {new Date(gateway.lastHeartbeat).toLocaleString()}</>
              ) : (
                'No heartbeat received'
              )}
            </CardDescription>
          </div>
          <StatusBadge status={gateway.status} />
        </div>
      </CardHeader>
      <CardContent className="space-y-2 text-sm">
        <div className="flex justify-between">
          <span className="text-muted-foreground">Current version</span>
          <span className="tabular-nums">{gateway.currentVersion ?? '—'}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Target version</span>
          <span className="tabular-nums">{gateway.targetVersion ?? '—'}</span>
        </div>
        {behind ? (
          <p className="text-xs text-amber-600 dark:text-amber-400">
            Behind target — a reconcile is pending.
          </p>
        ) : null}
      </CardContent>
    </Card>
  )
}

export function OverviewPage() {
  const publication = useCurrentPublication()
  const gateways = useGateways()
  const routeCount = useRouteCount()
  const serviceCount = useServiceCount()
  const snapshots = useSnapshotHistory()

  const anyLoading =
    publication.isPending || gateways.isPending || routeCount.isPending || serviceCount.isPending

  // A single failure is enough to make the page untrustworthy, so report the
  // first one rather than degrading every card independently.
  const failure = [publication, gateways, routeCount, serviceCount, snapshots].find((q) => q.error)
  if (failure?.error) {
    const display = toDisplayError(failure.error)
    return (
      <>
        <PageHeader title="Overview" description="Current state of the control plane." />
        <ErrorState
          title={display.title}
          message={display.message}
          correlationId={display.correlationId}
          action={
            <button
              type="button"
              onClick={() => void failure.refetch()}
              className="text-sm underline underline-offset-4"
            >
              Retry
            </button>
          }
        />
      </>
    )
  }

  const current = publication.data?.current ?? null
  const gatewayList = gateways.data?.gateways ?? []
  const history = snapshots.data?.snapshots ?? []

  return (
    <>
      <PageHeader
        title="Overview"
        description="Current state of the control plane."
      />

      <div className="space-y-6">
        {anyLoading && !publication.data ? (
          <LoadingState label="Loading overview" />
        ) : (
          <>
            {/* Published version */}
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-sm font-medium text-muted-foreground">
                  Current published version
                </CardTitle>
              </CardHeader>
              <CardContent>
                {current ? (
                  <div className="flex flex-wrap items-center gap-3">
                    <span className="font-heading text-3xl font-semibold tabular-nums">
                      {current.snapshotVersion}
                    </span>
                    <StatusBadge status={current.status} />
                    <span className="text-sm text-muted-foreground">
                      by {current.initiatedBy}
                      {current.completedAt
                        ? ` · ${new Date(current.completedAt).toLocaleString()}`
                        : ''}
                    </span>
                  </div>
                ) : (
                  <p className="text-sm text-muted-foreground">
                    Nothing published yet. Publish a snapshot to make it the current version.
                  </p>
                )}
              </CardContent>
            </Card>

            {/* Counts */}
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <StatCard title="Gateways" value={gatewayList.length} icon={Server} />
              <StatCard
                title="Routes"
                value={routeCount.data ?? 0}
                description={routeCount.data === undefined ? 'Loading…' : undefined}
                icon={RouteIcon}
              />
              <StatCard
                title="Services"
                value={serviceCount.data ?? 0}
                description={serviceCount.data === undefined ? 'Loading…' : undefined}
                icon={Boxes}
              />
              <StatCard title="Snapshots" value={snapshots.data?.totalCount ?? 0} icon={Camera} />
            </div>

            {/* Gateways */}
            <section aria-labelledby="gateways-heading">
              <h2 id="gateways-heading" className="mb-3 font-heading text-lg font-semibold">
                Gateway state
              </h2>
              {gatewayList.length === 0 ? (
                <EmptyState
                  title="No gateways registered"
                  description="Register a gateway to see its health and applied configuration version here."
                />
              ) : (
                <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                  {gatewayList.map((gateway) => (
                    <GatewayCard key={gateway.gatewayId} gateway={gateway} />
                  ))}
                </div>
              )}
            </section>

            {/* Snapshot history */}
            <section aria-labelledby="history-heading">
              <h2 id="history-heading" className="mb-3 font-heading text-lg font-semibold">
                Snapshot history
              </h2>
              {history.length === 0 ? (
                <EmptyState
                  title="No snapshots yet"
                  description="Snapshots capture the full runtime configuration at a point in time."
                />
              ) : (
                <ol className="space-y-2">
                  {[...history]
                    .sort((a, b) => b.version - a.version)
                    .map((snapshot) => (
                      <li
                        key={snapshot.version}
                        className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3"
                      >
                        <div className="flex items-center gap-3">
                          <span className="font-heading font-semibold tabular-nums">
                            v{snapshot.version}
                          </span>
                          <StatusBadge status={snapshot.status} />
                        </div>
                        <div className="text-sm text-muted-foreground">
                          <span>by {snapshot.createdBy}</span>
                          <span className="mx-2" aria-hidden="true">
                            ·
                          </span>
                          <span>{new Date(snapshot.createdAt).toLocaleString()}</span>
                        </div>
                      </li>
                    ))}
                </ol>
              )}
            </section>
          </>
        )}
      </div>
    </>
  )
}
