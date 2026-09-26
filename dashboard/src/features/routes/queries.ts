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

export function useRouteMutations() {
  const api = useApi()
  const queryClient = useQueryClient()

  // Mutations return the cache work, not a value, so onSuccess stays void.
  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['routes'] }),
      queryClient.invalidateQueries({ queryKey: ['overview'] }),
    ]).then(() => undefined)

  return {
    enable: useMutation({
      mutationFn: (id: string) => api.resources.routes.enable(id),
      onSuccess: invalidate,
    }),
    disable: useMutation({
      mutationFn: (id: string) => api.resources.routes.disable(id),
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: (id: string) => api.resources.routes.remove(id),
      onSuccess: invalidate,
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
