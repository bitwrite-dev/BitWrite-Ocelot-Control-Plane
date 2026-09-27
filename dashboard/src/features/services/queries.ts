import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import { ApiError } from '@/api'
import type { ServiceListResponse, ServiceResponse } from '@/api'

/**
 * Query hooks for the Services page.
 *
 * `GET /api/v1/services` takes only `page` and `pageSize`, so paging is
 * server-side and nothing else is filtered here.
 */

export const serviceKeys = {
  list: (page: number, pageSize: number) => ['services', 'list', page, pageSize] as const,
  detail: (id: string) => ['services', 'detail', id] as const,
  routes: (id: string) => ['services', 'routes', id] as const,
}

export function useServices(page: number, pageSize: number) {
  const api = useApi()

  return useQuery({
    queryKey: serviceKeys.list(page, pageSize),
    queryFn: ({ signal }) =>
      api.resources.services.list({ page, pageSize, signal }) as Promise<ServiceListResponse>,
    placeholderData: (previous) => previous,
  })
}

/**
 * The routes on a service.
 *
 * Used for the associations view, and for the delete confirmation: the API
 * refuses nothing on delete, so a service can be removed while routes still
 * point at it, and the warning has to come from here.
 */
export function useServiceRoutes(id: string | undefined, enabled: boolean) {
  const api = useApi()

  return useQuery({
    queryKey: serviceKeys.routes(id ?? ''),
    queryFn: ({ signal }) => api.resources.services.routes(id!, { signal }),
    enabled: Boolean(id) && enabled,
  })
}

export function useServiceMutations() {
  const api = useApi()
  const queryClient = useQueryClient()

  // A service shows up in route dropdowns and the overview, so anything derived
  // from the service list is stale once one changes.
  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['services'] }),
      queryClient.invalidateQueries({ queryKey: ['routes'] }),
      queryClient.invalidateQueries({ queryKey: ['overview'] }),
    ]).then(() => undefined)

  return {
    create: useMutation({
      mutationFn: (body: ServiceRequest) => api.resources.services.create(body),
      onSuccess: invalidate,
    }),
    update: useMutation({
      mutationFn: ({ id, body }: { id: string; body: ServiceRequest }) =>
        api.resources.services.update(id, body),
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: (id: string) => api.resources.services.remove(id),
      onSuccess: invalidate,
    }),
  }
}

/**
 * A service endpoint as the domain actually stores it.
 *
 * Host and port only: `ServiceEndpoint` has no scheme or path, and the API
 * reports `"http"` and `"/"` for both regardless of what was sent. See #474.
 */
export interface ServiceEndpointDraft {
  host: string
  /** A blank field is `''` rather than `NaN`, so it can be typed into. */
  port: number | ''
}

/** The editable form state. */
export interface ServiceDraft {
  name: string
  description: string | null
  downstreamTargets: (ServiceEndpointDraft & { scheme: string; path: string })[]
}

/** The request body the API accepts, where a port is a real number. */
export interface ServiceRequest {
  name: string
  description: string | null
  downstreamTargets: { host: string; port: number; scheme: string; path: string }[]
}

export const emptyServiceDraft = (): ServiceDraft => ({
  name: '',
  description: null,
  // The API requires at least one endpoint, so the form starts with one row.
  downstreamTargets: [{ host: '', port: '', scheme: 'http', path: '/' }],
})

/** Reads a stored service into the form state. */
export function serviceDraftFrom(service: ServiceResponse): ServiceDraft {
  return {
    name: service.name,
    description: service.description,
    downstreamTargets: service.downstreamTargets.map((target) => ({
      host: target.host,
      port: target.port,
      // Fixed, because the domain has nowhere to put them. See #474.
      scheme: 'http',
      path: '/',
    })),
  }
}

/** Converts form state into the request body. */
export function toServiceRequest(draft: ServiceDraft): ServiceRequest {
  return {
    name: draft.name.trim(),
    description: draft.description,
    downstreamTargets: draft.downstreamTargets.map((target) => ({
      host: target.host.trim(),
      port: Number(target.port),
      // The domain stores neither, so they are sent fixed rather than
      // pretending to be editable.
      scheme: 'http',
      path: '/',
    })),
  }
}

/**
 * Per-field errors from a rejected service request.
 *
 * The API answers with a `ValidationProblemDetails`, so the errors are keyed by
 * field and the form can point at the input at fault.
 */
export function toServiceFieldErrors(error: unknown): Record<string, string[]> {
  if (error instanceof ApiError && error.fieldErrors) {
    return error.fieldErrors
  }
  return {}
}

export function serviceErrorTitle(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.isNetworkError) return 'Cannot reach the control plane API'
    return error.isValidation ? 'The service was rejected' : `Request failed (${error.status})`
  }
  return 'Something went wrong'
}

export function serviceErrorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof Error) return error.message
  return String(error)
}
