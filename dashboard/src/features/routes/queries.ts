import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import { ApiError } from '@/api'
import type { RouteResponse, ServiceListResponse } from '@/api'

/**
 * Query hooks for the Routes list page.
 *
 * Filters, search and paging are all resolved by the API, so they live in the
 * query key: changing any of them refetches rather than filtering locally.
 */

export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const

export interface RouteFilters {
  page: number
  pageSize: number
  search: string
  isEnabled?: boolean
  serviceId?: string
}

export const routeKeys = {
  list: (filters: RouteFilters) => ['routes', 'list', filters] as const,
  detail: (id: string) => ['routes', 'detail', id] as const,
  effective: (id: string) => ['routes', 'effective', id] as const,
  history: (id: string) => ['routes', 'history', id] as const,
  serviceRoutes: (id: string) => ['routes', 'service', id] as const,
  services: ['routes', 'services'] as const,
}

export function useRoutes(filters: RouteFilters) {
  const api = useApi()

  return useQuery({
    queryKey: routeKeys.list(filters),
    queryFn: ({ signal }) =>
      api.resources.routes.list({
        page: filters.page,
        pageSize: filters.pageSize,
        // An empty string would be dropped by the client's query builder.
        search: filters.search.trim() || undefined,
        isEnabled: filters.isEnabled,
        serviceId: filters.serviceId,
        signal,
      }),
    // Keeps the previous page on screen while the next one loads, so the table
    // does not flash empty between pages.
    placeholderData: (previous) => previous,
  })
}

/**
 * Services for the filter dropdown and for resolving `serviceId` to a name.
 *
 * The routes list only carries a `serviceId`, so a name lookup needs a second
 * request. Fetched once and cached rather than per row.
 */
export function useServiceOptions() {
  const api = useApi()

  return useQuery({
    queryKey: routeKeys.services,
    queryFn: ({ signal }) =>
      api.resources.services.list({ pageSize: 100, signal }) as Promise<ServiceListResponse>,
    staleTime: 5 * 60_000,
  })
}

/** The route behind the details page. */
export function useRoute(id: string | undefined) {
  const api = useApi()

  return useQuery({
    queryKey: routeKeys.detail(id ?? ''),
    queryFn: ({ signal }) => api.resources.routes.get(id!, { signal }),
    enabled: Boolean(id),
  })
}

/**
 * The effective Ocelot JSON, only fetched while its tab is open.
 *
 * This is a server-side projection, so it is fetched on demand rather than
 * alongside the route: most visits to the page never need it.
 */
export function useRouteEffective(id: string | undefined, enabled: boolean) {
  const api = useApi()

  return useQuery({
    queryKey: routeKeys.effective(id ?? ''),
    queryFn: ({ signal }) => api.resources.routes.effective(id!, { signal }),
    enabled: Boolean(id) && enabled,
  })
}

export function useRouteHistory(id: string | undefined, enabled: boolean) {
  const api = useApi()

  return useQuery({
    queryKey: routeKeys.history(id ?? ''),
    queryFn: ({ signal }) => api.resources.routes.history(id!, { signal }),
    enabled: Boolean(id) && enabled,
  })
}

/** Other routes on the same service, for context on the details page. */
export function useServiceRoutes(serviceId: string | undefined, enabled: boolean) {
  const api = useApi()

  return useQuery({
    queryKey: routeKeys.serviceRoutes(serviceId ?? ''),
    queryFn: ({ signal }) => api.resources.services.routes(serviceId!, { signal }),
    enabled: Boolean(serviceId) && enabled,
  })
}

export function useRouteMutations() {
  const api = useApi()
  const queryClient = useQueryClient()

  // Mutations return the cache work, not a value, so onSuccess stays void.
  const invalidate = (id?: string) =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['routes'] }),
      queryClient.invalidateQueries({ queryKey: ['overview'] }),
      // The effective config and history are derived from the route, so they
      // go stale the moment it is enabled, disabled or edited.
      id
        ? Promise.all([
            queryClient.invalidateQueries({ queryKey: routeKeys.effective(id) }),
            queryClient.invalidateQueries({ queryKey: routeKeys.history(id) }),
          ])
        : Promise.resolve(),
    ]).then(() => undefined)

  return {
    enable: useMutation({
      mutationFn: (id: string) => api.resources.routes.enable(id),
      onSuccess: (_result, id) => invalidate(id),
    }),
    disable: useMutation({
      mutationFn: (id: string) => api.resources.routes.disable(id),
      onSuccess: (_result, id) => invalidate(id),
    }),
    remove: useMutation({
      mutationFn: (id: string) => api.resources.routes.remove(id),
      onSuccess: () => invalidate(),
    }),
  }
}

/**
 * Turns any failure into a displayable message.
 *
 * `ValidationProblemDetails` responses carry field errors, which matter here
 * because route validation reports problems on a specific field.
 */
export function toRouteError(error: unknown): {
  title: string
  message: string
  correlationId?: string
  isValidation: boolean
} {
  if (error instanceof ApiError) {
    if (error.isNetworkError) {
      return {
        title: 'Cannot reach the control plane API',
        message: `${error.message}\n\nIs the API running? Start it with:\n  dotnet run --project src/BitWrite.OcelotControl.Api`,
        isValidation: false,
      }
    }

    return {
      title: error.isValidation ? 'The route was rejected' : `Request failed (${error.status})`,
      message: error.message,
      correlationId: error.correlationId,
      isValidation: error.isValidation,
    }
  }

  return {
    title: 'Something went wrong',
    message: error instanceof Error ? error.message : String(error),
    isValidation: false,
  }
}

/** Narrowing helper for rows, since the API returns nullable option blocks. */
export function hasAuthentication(route: RouteResponse): boolean {
  return (route.authenticationOptions?.allowedScopes?.length ?? 0) > 0
}

export function hasRateLimit(route: RouteResponse): boolean {
  return route.rateLimitOptions?.enableRateLimiting === true
}

export function rateLimitSummary(route: RouteResponse): string | null {
  if (!hasRateLimit(route)) return null
  const { limit, period } = route.rateLimitOptions!
  return `${limit} / ${period}`
}
