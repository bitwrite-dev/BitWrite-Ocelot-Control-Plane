import { useQuery } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import type { SnapshotListResponse } from '@/api'

import { useSnapshotMutations } from './queries'

/**
 * The data the create page shows before anything is created.
 *
 * All of it comes from endpoints that already exist. The API has no preview of
 * the artifact it would produce, so the page reports what the current state
 * contains — which is the part an operator is actually deciding about — rather
 * than a rendering of a file that does not exist yet.
 */

/** What the current management state holds, by count. */
export function useCreateSourceCounts() {
  const api = useApi()

  const routes = useQuery({
    queryKey: ['routes', 'list', 1, 1],
    // Only the total is read, so one row is enough.
    queryFn: ({ signal }) => api.resources.routes.list({ page: 1, pageSize: 1, signal }),
    staleTime: 30_000,
  })

  const services = useQuery({
    queryKey: ['services', 'list', 1, 1],
    queryFn: ({ signal }) => api.resources.services.list({ page: 1, pageSize: 1, signal }),
    staleTime: 30_000,
  })

  return {
    isPending: routes.isPending || services.isPending,
    // null means the count could not be read, which the page shows as a dash.
    // It must not show 0: "no routes" would suggest an empty artifact is about
    // to be sealed.
    counts: {
      routes: routes.data ? routes.data.totalCount : null,
      services: services.data ? services.data.totalCount : null,
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

/** Creating, plus what came of it. */
export function useCreateSnapshot() {
  const { create } = useSnapshotMutations()
  return {
    create,
    isPending: create.isPending,
    error: create.error,
    data: create.data,
  }
}
