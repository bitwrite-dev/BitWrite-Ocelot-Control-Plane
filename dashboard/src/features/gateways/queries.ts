import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import type { GatewayListResponse, GatewayRequest } from '@/api'

/**
 * Query hooks for the Gateways page.
 *
 * `GET /api/v1/gateways` takes only `page` and `pageSize`, so paging is
 * server-side and nothing else is filtered here.
 */

export const gatewayKeys = {
  list: (page: number, pageSize: number) => ['gateways', 'list', page, pageSize] as const,
  detail: (id: string) => ['gateways', 'detail', id] as const,
}

export function useGateways(page: number, pageSize: number) {
  const api = useApi()

  return useQuery({
    queryKey: gatewayKeys.list(page, pageSize),
    queryFn: ({ signal }) =>
      api.resources.gateways.list({ page, pageSize, signal }) as Promise<GatewayListResponse>,
    // Keeps the table from collapsing while a page is fetched, which otherwise
    // reads as "no gateways" for a moment.
    placeholderData: (previous) => previous,
  })
}

/**
 * Whether a gateway can be deleted.
 *
 * Fetched only when the confirmation opens, not with the list: it is a separate
 * question per gateway, and asking for all of them up front would be N requests
 * to answer a question most operators never ask.
 */
export function useGatewayDeletionEligibility(id: string | undefined, enabled: boolean) {
  const api = useApi()

  return useQuery({
    queryKey: gatewayKeys.detail(id ?? ''),
    queryFn: ({ signal }) => api.resources.gateways.deletionEligibility(id!, { signal }),
    enabled: Boolean(id) && enabled,
  })
}

export function useGatewayMutations() {
  const api = useApi()
  const queryClient = useQueryClient()

  // A gateway shows up in the overview and on publications, so anything derived
  // from the list is stale once one changes.
  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['gateways'] }),
      queryClient.invalidateQueries({ queryKey: ['publications'] }),
      queryClient.invalidateQueries({ queryKey: ['overview'] }),
    ]).then(() => undefined)

  return {
    create: useMutation({
      mutationFn: (body: GatewayRequest) => api.resources.gateways.create(body),
      onSuccess: invalidate,
    }),
    update: useMutation({
      mutationFn: ({ id, body }: { id: string; body: GatewayRequest }) =>
        api.resources.gateways.update(id, body),
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: (id: string) => api.resources.gateways.remove(id),
      onSuccess: invalidate,
    }),
    setStatus: useMutation({
      mutationFn: ({ id, status }: { id: string; status: string }) =>
        api.resources.gateways.updateStatus(id, status),
      onSuccess: invalidate,
    }),
  }
}

/**
/**
 * How stale a report has to be before the status beside it is not evidence.
 *
 * A gateway is not answering inside this window is not reachable, whatever the
 * recorded label says. Used to mark the cell rather than to change any behaviour.
 */
export const HEARTBEAT_STALE_AFTER_MINUTES = 10

/** The statuses the API accepts.
 *
 * These are the exact strings `RuntimeStatus.From` accepts; anything else is
 * rejected by the domain, so offering a wider list would only produce a failed
 * save. None of them is a power state — there is no "shut down", and the only
 * lifecycle rule is the deletion restriction.
 */
export const GATEWAY_STATUSES = [
  'Disconnected',
  'Connecting',
  'Synchronized',
  'Applying',
  'Active',
  'Degraded',
] as const

export type GatewayStatus = (typeof GATEWAY_STATUSES)[number]

/** The editable form state. */
export interface GatewayDraft {
  name: string
  description: string
}

export interface GatewayRequestBody {
  name: string
  description: string | null
}

export const emptyGatewayDraft = (): GatewayDraft => ({
  name: '',
  description: '',
})

export function toGatewayRequest(draft: GatewayDraft): GatewayRequestBody {
  return {
    name: draft.name.trim(),
    // A blank description is null rather than "", which the API would store as
    // an empty description rather than as none.
    description: draft.description.trim() === '' ? null : draft.description.trim(),
  }
}

/**
 * The rules for the form, checked here rather than only on submit.
 *
 * The bounds mirror the API's `MaxLength`, so an operator is told at the field
 * rather than after a round trip.
 */
export function gatewayErrors(draft: GatewayDraft): Record<string, string> {
  const errors: Record<string, string> = {}
  const name = draft.name.trim()

  if (name === '') {
    errors.name = 'A name is required'
  } else if (name.length > 200) {
    errors.name = 'Name must be 200 characters or fewer'
  }

  if (draft.description.length > 1000) {
    errors.description = 'Description must be 1000 characters or fewer'
  }

  return errors
}
