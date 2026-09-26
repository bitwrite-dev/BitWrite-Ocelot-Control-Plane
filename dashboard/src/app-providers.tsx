import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

import { createApiClient, type ApiClient } from '@/api'

/**
 * The API client and query cache are wired once, here, and provided through
 * context so components never import a module-level singleton.
 *
 * The client is created per QueryClient so tests can build an isolated cache and
 * inject a stub `fetchImpl`.
 */
export function createAppProviders({
  client,
  children,
}: {
  client?: ApiClient
  children: ReactNode
}) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        // Overview polls nothing, but gateway and publication state change on the
        // order of seconds once gateways report in, so a short stale window keeps
        // the dashboard feeling live without hammering Redis.
        staleTime: 10_000,
        retry: 1,
        refetchOnWindowFocus: true,
      },
    },
  })

  const api = client ?? createApiClient()

  return (
    <QueryClientProvider client={queryClient}>
      <ApiClientContext.Provider value={api}>{children}</ApiClientContext.Provider>
    </QueryClientProvider>
  )
}

import { createContext, useContext } from 'react'

/**
 * Exported so tests can inject a client with a stubbed `fetchImpl`. Application
 * code should use the `useApi` hook rather than reaching for the context.
 */
export const ApiClientContext = createContext<ApiClient | null>(null)

/** The shared API client. Throws if used outside `createAppProviders`. */
export function useApi(): ApiClient {
  const api = useContext(ApiClientContext)
  if (!api) {
    throw new Error('useApi must be used within createAppProviders')
  }
  return api
}
