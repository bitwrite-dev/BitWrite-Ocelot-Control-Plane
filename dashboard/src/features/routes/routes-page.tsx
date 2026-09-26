import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useSearchParams } from 'react-router-dom'

import { PageHeader } from '@/components/app-layout'
import { DataTable, type Column } from '@/components/data-table'
import { ErrorState } from '@/components/page-state'
import { EnabledBadge } from '@/components/status-badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import {
  PAGE_SIZE_OPTIONS,
  hasAuthentication,
  hasRateLimit,
  rateLimitSummary,
  toRouteError,
  useRouteMutations,
  useRoutes,
  useServiceOptions,
  type RouteFilters,
} from './queries'
import type { RouteResponse } from '@/api'

const FILTERS = 'enabled' as const
const SERVICE = 'service' as const
const SEARCH = 'search' as const

/**
 * Routes list (§30.2, §6.2).
 *
 * Filter, search and paging are server-side, so the URL carries the state: a
 * filtered list can be linked, reloaded or reached with the back button.
 */
export function RoutesPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [pendingDelete, setPendingDelete] = useState<RouteResponse | null>(null)

  const filters: RouteFilters = {
    page: Number(searchParams.get('page') ?? 1),
    pageSize: Number(searchParams.get('pageSize') ?? 20),
    search: searchParams.get(SEARCH) ?? '',
    isEnabled: readEnabledFilter(searchParams.get(FILTERS)),
    serviceId: searchParams.get(SERVICE) ?? undefined,
  }

  const { data, isPending, isFetching, error, refetch } = useRoutes(filters)
  const services = useServiceOptions()
  const { enable, disable, remove } = useRouteMutations()

  const serviceNames = new Map(
    (services.data?.services ?? []).map((service) => [service.id, service.name]),
  )

  /** Applies a filter change and always returns to page 1 — a new filter makes the old page number meaningless. */
  const updateParams = (changes: Record<string, string | null>, resetPage = true) => {
    const next = new URLSearchParams(searchParams)
    for (const [key, value] of Object.entries(changes)) {
      if (value === null || value === '') next.delete(key)
      else next.set(key, value)
    }
    if (resetPage) next.delete('page')
    setSearchParams(next)
  }

  const actionError =
    [enable.error, disable.error, remove.error].find(Boolean) ?? null
  const anyActionPending = enable.isPending || disable.isPending || remove.isPending

  const columns: Column<RouteResponse>[] = [
    {
      key: 'key',
      header: 'Key',
      cell: (route) => (
        <Link
          to={`/routes/${route.id}`}
          className="font-medium underline-offset-4 hover:underline"
        >
          {route.key || route.upstreamPath}
        </Link>
      ),
    },
    {
      key: 'method',
      header: 'Method',
      cell: (route) => <Badge variant="outline">{route.method}</Badge>,
    },
    {
      key: 'path',
      header: 'Upstream path',
      cell: (route) => (
        <span className="font-mono text-xs">{route.upstreamPath}</span>
      ),
    },
    {
      key: 'service',
      header: 'Service',
      cell: (route) => (
        <span className="text-sm">
          {serviceNames.get(route.serviceId) ?? (
            <span className="text-muted-foreground" title={route.serviceId}>
              unknown
            </span>
          )}
        </span>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      cell: (route) => <EnabledBadge isEnabled={route.isEnabled} />,
    },
    {
      key: 'auth',
      header: 'Auth',
      cell: (route) =>
        hasAuthentication(route) ? (
          <Badge variant="secondary" title={route.authenticationOptions!.allowedScopes.join(', ')}>
            {route.authenticationOptions!.allowedScopes.length} scope
            {route.authenticationOptions!.allowedScopes.length === 1 ? '' : 's'}
          </Badge>
        ) : (
          <span className="text-muted-foreground" aria-label="No authentication">
            —
          </span>
        ),
    },
    {
      key: 'ratelimit',
      header: 'Rate limit',
      cell: (route) =>
        hasRateLimit(route) ? (
          <span className="text-xs tabular-nums">{rateLimitSummary(route)}</span>
        ) : (
          <span className="text-muted-foreground" aria-label="No rate limiting">
            —
          </span>
        ),
    },
    {
      key: 'targets',
      header: 'Downstream',
      cell: (route) => (
        <span className="text-xs tabular-nums">
          {route.downstreamTargets
            .map((target) => `${target.host}:${target.port}`)
            .join(', ')}
        </span>
      ),
    },
    {
      key: 'actions',
      header: 'Actions',
      className: 'text-right',
      cell: (route) => (
        <div className="flex justify-end gap-2">
          {route.isEnabled ? (
            <Button
              variant="outline"
              size="sm"
              disabled={anyActionPending}
              onClick={() => disable.mutate(route.id)}
            >
              Disable
            </Button>
          ) : (
            <Button
              variant="outline"
              size="sm"
              disabled={anyActionPending}
              onClick={() => enable.mutate(route.id)}
            >
              Enable
            </Button>
          )}

          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button
                variant="outline"
                size="sm"
                disabled={anyActionPending}
                onClick={() => setPendingDelete(route)}
              >
                Delete
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete this route?</AlertDialogTitle>
                <AlertDialogDescription>
                  {route.key || route.upstreamPath} will be removed from the control
                  plane. This cannot be undone.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction
                  onClick={() => {
                    if (pendingDelete) remove.mutate(pendingDelete.id)
                    setPendingDelete(null)
                  }}
                >
                  Delete route
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Routes"
        description="All routes known to the control plane."
      />

      <div className="space-y-4">
        <div className="flex flex-wrap items-center gap-3">
          <Input
            value={filters.search}
            onChange={(event) => updateParams({ [SEARCH]: event.target.value })}
            placeholder="Search key, path or host…"
            aria-label="Search routes"
            className="max-w-xs"
          />

          <select
            className="h-9 rounded-md border bg-background px-3 text-sm"
            value={filters.isEnabled === undefined ? '' : String(filters.isEnabled)}
            onChange={(event) =>
              updateParams({ [FILTERS]: event.target.value || null })
            }
            aria-label="Filter by status"
          >
            <option value="">All statuses</option>
            <option value="true">Enabled</option>
            <option value="false">Disabled</option>
          </select>

          <select
            className="h-9 rounded-md border bg-background px-3 text-sm"
            value={filters.serviceId ?? ''}
            onChange={(event) => updateParams({ [SERVICE]: event.target.value || null })}
            aria-label="Filter by service"
          >
            <option value="">All services</option>
            {(services.data?.services ?? []).map((service) => (
              <option key={service.id} value={service.id}>
                {service.name}
              </option>
            ))}
          </select>

          {searchParams.size > 0 ? (
            <Button variant="ghost" size="sm" onClick={() => setSearchParams({})}>
              Clear filters
            </Button>
          ) : null}
        </div>

        {actionError ? (
          <ErrorState
            title={toRouteError(actionError).title}
            message={toRouteError(actionError).message}
            correlationId={toRouteError(actionError).correlationId}
          />
        ) : null}

        {error ? (
          <ErrorState
            {...toRouteError(error)}
            action={
              <Button variant="outline" size="sm" onClick={() => void refetch()}>
                Retry
              </Button>
            }
          />
        ) : (
          <DataTable
            caption="Routes known to the control plane"
            columns={columns}
            rows={data?.routes ?? []}
            rowKey={(route) => route.id}
            totalCount={data?.totalCount ?? 0}
            page={filters.page}
            pageSize={filters.pageSize}
            pageSizeOptions={[...PAGE_SIZE_OPTIONS]}
            onPageChange={(page) => updateParams({ page: String(page) }, false)}
            onPageSizeChange={(size) => updateParams({ pageSize: String(size) })}
            // isFetching rather than isPending: while paging, the previous page is
            // kept on screen and the table should not flash to skeletons.
            isLoading={isPending || (isFetching && !data)}
          />
        )}
      </div>
    </>
  )
}

function readEnabledFilter(value: string | null): boolean | undefined {
  if (value === 'true') return true
  if (value === 'false') return false
  return undefined
}
