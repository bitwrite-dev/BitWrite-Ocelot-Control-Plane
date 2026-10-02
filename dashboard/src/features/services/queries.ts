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
 * Host, port and weight. The form used to carry a scheme and a path too, which the
 * API accepted, discarded, and echoed back as `"http"` and `"/"` — so a typed path
 * came back as `/` with nothing to say whether it had been kept. See #474.
 */
export interface ServiceEndpointDraft {
  host: string
  /** A blank field is `''` rather than `NaN`, so it can be typed into. */
  port: number | ''
  /** Load-balancing weight. Editable, because the API now accepts and reports it. */
  weight: number | ''
}

/** The editable form state. */
export interface ServiceDraft {
  name: string
  description: string | null
  downstreamTargets: ServiceEndpointDraft[]
}

/** The request body the API accepts, where a port and a weight are real numbers. */
export interface ServiceRequest {
  name: string
  description: string | null
  downstreamTargets: { host: string; port: number; weight: number }[]
}

export const emptyServiceDraft = (): ServiceDraft => ({
  name: '',
  description: null,
  // The API requires at least one endpoint, so the form starts with one row.
  downstreamTargets: [{ host: '', port: '', weight: 1 }],
})

/** Reads a stored service into the form state. */
export function serviceDraftFrom(service: ServiceResponse): ServiceDraft {
  return {
    name: service.name,
    description: service.description,
    downstreamTargets: service.downstreamTargets.map((target) => ({
      host: target.host,
      port: target.port,
      // Read back rather than invented: it is in the domain, the API reports it,
      // and it decides how heavily a gateway sends traffic this way.
      weight: target.weight,
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
      // Sent as the operator set it, and stored: the update handler used to
      // match endpoints on host and port alone and drop a changed weight.
      weight: Number(target.weight),
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
