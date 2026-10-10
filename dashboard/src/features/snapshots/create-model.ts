import { useQuery } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import type { PreviewSnapshotResponse, SnapshotListResponse, GlobalConfigurationResponse, RouteListResponse, ServiceListResponse } from '@/api'

import { useSnapshotMutations } from './queries'

/**
 * The data behind the create page's three steps.
 *
 * The counts come from the totals the list endpoints already return, so the page
 * can show what is about to be sealed without adding an endpoint that only
 * supports one screen.
 */

/** What the current management state holds, by count and draft status. */
export function useCreateSourceCounts() {
  const api = useApi()

  const routes = useQuery({
    queryKey: ['routes', 'list', 1, 1],
    queryFn: ({ signal }) => api.resources.routes.list({ page: 1, pageSize: 1, signal }),
    staleTime: 30_000,
  })

  const services = useQuery({
    queryKey: ['services', 'list', 1, 1],
    queryFn: ({ signal }) => api.resources.services.list({ page: 1, pageSize: 1, signal }),
    staleTime: 30_000,
  })

  const globalConfig = useQuery({
    queryKey: ['global-configuration', 'current'],
    queryFn: ({ signal }) => api.resources.globalConfiguration.get({ signal }),
    staleTime: 30_000,
  })

  return {
    isPending: routes.isPending || services.isPending || globalConfig.isPending,
    counts: {
      routes: routes.data ? routes.data.totalCount : null,
      services: services.data ? services.data.totalCount : null,
    },
    draftStatus: {
      routes: routes.data ? routes.data.routes.filter((r) => r.isDraft).length : null,
      services: services.data ? services.data.services.filter((s) => s.isDraft).length : null,
      globalConfig: globalConfig.data && globalConfig.data.isDraft ? 1 : 0,
    },
  }
}

/**
 * The most recently published snapshot, if any.
 *
 * The list is newest-first, so the first row that was published is the one
 * gateways are meant to be running. A snapshot that was published and later
 * archived keeps its `publishedAt`, which is exactly what makes it a candidate.
 */
export function useLatestPublishedSnapshot() {
  const api = useApi()

  const query = useQuery({
    queryKey: ['snapshots', 'published-latest'],
    queryFn: ({ signal }) =>
      api.resources.snapshots.list({ page: 1, pageSize: 20, signal }) as Promise<SnapshotListResponse>,
    staleTime: 30_000,
  })

  return {
    isPending: query.isPending,
    snapshot: query.data?.snapshots.find((snapshot) => snapshot.publishedAt !== null) ?? null,
  }
}

/**
 * Resolves and validates the artifact a snapshot would contain.
 *
 * Kept out of the automatic query cache on purpose: it is a server round trip
 * whose result is only meaningful for the step the operator is looking at, and
 * serving a stale preview after they changed a route would show a verdict for a
 * configuration that no longer exists.
 */
export function usePreviewSnapshot() {
  const api = useApi()

  const query = useQuery({
    queryKey: ['snapshots', 'preview'],
    queryFn: ({ signal }) => api.resources.snapshots.preview({ signal }),
    // Not run on mount — the page triggers it when the operator reaches step 2,
    // so arriving at the step is what does the work.
    enabled: false,
    // The management state can move under the operator, so a preview is never
    // reused across a revisit without an explicit re-run.
    staleTime: 0,
    gcTime: 0,
  })

  return {
    data: query.data as PreviewSnapshotResponse | null,
    isFetching: query.isFetching,
    isError: query.isError,
    error: query.error,
    run: () => query.refetch(),
  }
}

/** Creating, plus what came of it. */
export function useSnapshotCreate() {
  const { create } = useSnapshotMutations()

  return {
    create,
    isPending: create.isPending,
    error: create.error,
    data: create.data,
  }
}
