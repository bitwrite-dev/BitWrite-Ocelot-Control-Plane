import { useQuery } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import type {
  PublicationResponse,
} from '@/api'

/**
 * Publications: records of which snapshot was sent to which gateways, when, and
 * what the outcome was.
 */

export const publicationKeys = {
  list: (page: number) =>
    ['publications', 'list', page] as const,
  current: () => ['publications', 'current'] as const,
  history: () => ['publications', 'history'] as const,
  detail: (id: string) => ['publications', 'detail', id] as const,
}

const PAGE_SIZE = 20

export function usePublications(page: number) {
  const api = useApi()

  return useQuery({
    queryKey: publicationKeys.list(page),
    queryFn: ({ signal }) =>
      api.resources.publications.list({ page, pageSize: PAGE_SIZE, signal }),
    placeholderData: (previous) => previous,
  })
}

export function useCurrentPublication() {
  const api = useApi()

  return useQuery({
    queryKey: publicationKeys.current(),
    queryFn: ({ signal }) => api.resources.publications.current({ signal }),
  })
}

export function usePublicationHistory() {
  const api = useApi()

  return useQuery({
    queryKey: publicationKeys.history(),
    queryFn: ({ signal }) => api.resources.publications.history({ signal }),
  })
}

export function usePublicationDetail(id: string, enabled: boolean) {
  const api = useApi()

  return useQuery({
    queryKey: publicationKeys.detail(id),
    queryFn: async ({ signal }) => {
      const list = await api.resources.publications.history({ signal })
      return list.find((p: PublicationResponse) => p.id === id) ?? null
    },
    enabled: enabled && !!id,
  })
}

/**
 * Whether a publication is currently the one gateways are running.
 */
export function publicationStatus(
  publication: Pick<PublicationResponse, 'status'>,
): { label: string; tone: 'ok' | 'bad' | 'warn' | 'mute' } {
  const status = publication.status.toLowerCase()

  if (status.includes('complete') || status.includes('success')) {
    return { label: 'Completed', tone: 'ok' }
  }
  if (status.includes('fail') || status.includes('error')) {
    return { label: 'Failed', tone: 'bad' }
  }
  if (status.includes('progress') || status.includes('started')) {
    return { label: 'In Progress', tone: 'warn' }
  }
  return { label: 'Unknown', tone: 'mute' }
}

/**
 * Format a timestamp for display.
 */
export function formatTimestamp(value: string | null): string {
  if (!value) return '—'
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}

/**
 * Shorten a hash for display.
 */
export function shortHash(hash: string): string {
  if (!hash) return '—'
  return hash.length <= 14 ? hash : `${hash.slice(0, 12)}…`
}