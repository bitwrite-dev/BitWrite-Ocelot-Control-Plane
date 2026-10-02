import { useQuery } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import type { AuditListResponse, AuditResponse } from '@/api'

import { pageSize, type PageRequest } from './paging'

/**
 * The audit log: what was done, by whom, to what, and whether it worked.
 *
 * Every command in the product records here, so this is the one page that can
 * answer "who changed this" for any resource — the reason the log is read is
 * usually to settle a question about the past rather than to look at the past.
 *
 * Filters are sent to the server, not applied here. The log is the one list in
 * the product that grows without bound, so counting only the page in hand would
 * report a total that is wrong the moment there is more than one page.
 */

/** What the API accepts as a filter. Each is optional and they combine. */
export interface AuditFilters {
  actor?: string
  action?: string
  resourceType?: string
  resourceId?: string
  from?: string
  to?: string
}

export const emptyAuditFilters: AuditFilters = {}

/** True when nothing is being filtered, so the UI can say so plainly. */
export function hasActiveFilters(filters: AuditFilters): boolean {
  return Object.values(filters).some((value) => value !== undefined && value !== '')
}

/**
 * Drops blank entries so an untouched field is not sent as an empty filter.
 *
 * `action=`, with nothing after it, is a filter for the empty action — which
 * matches nothing, and reads as "this log has no entries" rather than as
 * "that filter was ignored".
 */
export function toQuery(filters: AuditFilters): PageRequest {
  const query: PageRequest = { pageSize: pageSize.audit }

  const set = (key: keyof AuditFilters, value: string | undefined) => {
    if (value !== undefined && value !== '') {
      // Each key maps to a field of the same name on the request, so this is a
      // rename of the pair rather than a cast.
      Object.assign(query, { [key]: value })
    }
  }

  set('actor', filters.actor)
  set('action', filters.action)
  set('resourceType', filters.resourceType)
  set('resourceId', filters.resourceId)
  set('from', filters.from ? startOfDayIso(filters.from) : undefined)
  set('to', filters.to ? endOfDayIso(filters.to) : undefined)

  return query
}

/**
 * The start of a calendar day, in UTC.
 *
 * `<input type="date">` produces `2026-10-03` and nothing else, which ASP.NET binds
 * as midnight in the server's own time zone — Asia/Tehran here, so
 * `2026-10-02T20:30Z`. An entry from 21:14Z the previous evening is then "from
 * 2026-10-03" on screen and matches, which is not what picking that date means.
 *
 * Stated in UTC instead: the log timestamps are UTC, so the day the reader sees is
 * the day the records carry.
 */
function startOfDayIso(date: string): string {
  return `${date}T00:00:00.000Z`
}

/**
 * The end of a calendar day.
 *
 * `2026-10-02` bound as midnight that day is 00:00, so `to=2026-10-02` excluded
 * everything written during the 2nd — the reader picked the 2nd and got the 1st.
 * The last millisecond of the day is what "up to and including" means.
 */
function endOfDayIso(date: string): string {
  return `${date}T23:59:59.999Z`
}

/**
 * The resource types the log records, for the filter's dropdown.
 *
 * Read from what the log actually contains rather than a hard-coded list, so a
 * resource type added later appears without a second edit here.
 */
export function resourceTypesIn(entries: AuditResponse[]): string[] {
  return [...new Set(entries.map((entry) => entry.resourceType))].sort()
}

/** The actions the log records, for the filter's dropdown. */
export function actionsIn(entries: AuditResponse[]): string[] {
  return [...new Set(entries.map((entry) => entry.action))].sort()
}

export function useAuditLog(page: number, filters: AuditFilters) {
  const api = useApi()

  return useQuery({
    // The filters are part of the key, so changing one cannot show the previous
    // filter's rows under the new heading while the request is in flight.
    queryKey: ['audit', 'list', page, filters],
    queryFn: ({ signal }) =>
      api.resources.audit.list({ ...toQuery(filters), page, signal }) as Promise<AuditListResponse>,
    // The log only grows as the product is used, so a short staleness window saves
    // a round trip without showing anything out of date for long.
    staleTime: 15_000,
    // Rows from the previous filter stay on screen rather than collapsing to a
    // spinner, which for a page that is only read reads as "nothing happened".
    placeholderData: (previous) => previous,
  })
}

export function useAuditStats(enabled: boolean) {
  const api = useApi()

  return useQuery({
    queryKey: ['audit', 'stats'],
    queryFn: ({ signal }) => api.resources.audit.stats({ signal }),
    // The counts are derived from the same records as the list, so refetching them
    // on every page change would show totals that disagree with the rows beside
    // them.
    enabled,
    staleTime: 15_000,
  })
}