import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import { toCreateRequest, type RouteDraft } from './wizard-model'
import type { RouteValidationResponse } from '@/api'

/**
 * Query hooks for the Create Route Wizard.
 *
 * Creating a route is a one-shot, so it is a mutation rather than a query.
 */

export const wizardKeys = {
  services: ['wizard', 'services'] as const,
}

/**
 * The service list is needed on the first render: a route must belong to a
 * service, so there is nothing useful to show without it.
 */
export function useWizardServices() {
  const api = useApi()

  return useQuery({
    queryKey: wizardKeys.services,
    queryFn: ({ signal }) => api.resources.services.list({ pageSize: 100, signal }),
    staleTime: 5 * 60_000,
  })
}

export function useCreateRoute() {
  const api = useApi()
  const queryClient = useQueryClient()

  return useMutation({
    // The body is shaped in wizard-model, next to the draft it comes from.
    mutationFn: (draft: RouteDraft) => api.resources.routes.create(toCreateRequest(draft)),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['routes'] }),
        queryClient.invalidateQueries({ queryKey: ['overview'] }),
      ]).then(() => undefined),
  })
}

/**
 * Asks the API to check a draft before it is saved.
 *
 * Runs the same checks the create path would, without persisting anything, so
 * the Review step can report a problem before the operator commits to it.
 */
export function useValidateRouteDraft() {
  const api = useApi()

  return useMutation({
    mutationFn: (draft: RouteDraft) =>
      api.resources.routes.validateDraft(toCreateRequest(draft)),
  })
}

export type DraftValidation = RouteValidationResponse
