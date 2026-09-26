import { useQuery } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import { ApiError } from '@/api'

/**
 * Query hooks for the Overview page.
 *
 * Kept in one module because the Overview is the first consumer and the shape is
 * still settling; later pages get their own.
 */

/** Query keys, centralised so invalidation cannot drift from the fetch. */
export const overviewKeys = {
  currentPublication: ['overview', 'current-publication'] as const,
  gateways: ['overview', 'gateways'] as const,
  routeCount: ['overview', 'route-count'] as const,
  serviceCount: ['overview', 'service-count'] as const,
  snapshots: ['overview', 'snapshots'] as const,
}

export function useCurrentPublication() {
  const api = useApi()
  return useQuery({
    queryKey: overviewKeys.currentPublication,
    queryFn: ({ signal }) => api.resources.publications.current({ signal }),
  })
}

/**
 * Gateway runtime state.
 *
 * Uses `GET /api/v1/runtime/gateways` rather than `GET /api/v1/runtime/status`:
 * the latter requires a `gatewayId` that callers are never told about and
 * answers 400. See #450.
 */
export function useGateways() {
  const api = useApi()
  return useQuery({
    queryKey: overviewKeys.gateways,
    queryFn: ({ signal }) => api.resources.runtime.gateways({ signal }),
  })
}

/** Total route count, taken from `totalCount` rather than the page length. */
export function useRouteCount() {
  const api = useApi()
  return useQuery({
    queryKey: overviewKeys.routeCount,
    queryFn: ({ signal }) => api.resources.routes.list({ pageSize: 1, signal }),
    select: (data) => data.totalCount,
  })
}

export function useServiceCount() {
  const api = useApi()
  return useQuery({
    queryKey: overviewKeys.serviceCount,
    queryFn: ({ signal }) => api.resources.services.list({ pageSize: 1, signal }),
    select: (data) => data.totalCount,
  })
}

/** Snapshot history for the version chart, newest first. */
export function useSnapshotHistory(limit = 20) {
  const api = useApi()
  return useQuery({
    queryKey: [...overviewKeys.snapshots, limit],
    queryFn: ({ signal }) => api.resources.snapshots.list({ pageSize: limit, signal }),
  })
}

/**
 * Reduces any query to a single displayable failure.
 *
 * A network failure and an API error need different wording, and surfacing the
 * correlation id is what makes a bug report actionable.
 */
export function toDisplayError(error: unknown): {
  title: string
  message: string
  correlationId?: string
} {
  if (error instanceof ApiError) {
    if (error.isNetworkError) {
      return {
        title: 'Cannot reach the control plane API',
        message: error.message,
      }
    }
    return {
      title: `Request failed${error.status ? ` (${error.status})` : ''}`,
      message: error.message,
      correlationId: error.correlationId,
    }
  }

  return {
    title: 'Something went wrong',
    message: error instanceof Error ? error.message : String(error),
  }
}
