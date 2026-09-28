import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import type {
  SnapshotDeploymentResponse,
  SnapshotListResponse,
  SnapshotResponse,
  SnapshotValidationRule,
} from '@/api'

/**
 * Snapshots: immutable, hashed records of everything published to a gateway.
 *
 * `GET /api/v1/snapshots` takes only `page` and `pageSize`, so paging is
 * server-side. A snapshot's content is not returned in the list — it is a whole
 * Ocelot document per row — so the list carries the composition counts instead.
 */

/**
 * The Ocelot versions a snapshot can target.
 *
 * Mirrors the values the domain's `OcelotVersion` accepts. The version is fixed
 * once at setup, so this is a read-only display on the create page rather than a
 * choice that changes the artifact.
 */
export const OCELOT_VERSIONS = ['18.x', '19.x', '20.x'] as const

/** The statuses the API accepts as a `?status=` filter. */
export const SNAPSHOT_STATUSES = ['Ready', 'Published', 'Archived'] as const
export type SnapshotStatusFilter = (typeof SNAPSHOT_STATUSES)[number] | null

export const snapshotKeys = {
  list: (page: number, pageSize: number, status: SnapshotStatusFilter) =>
    ['snapshots', 'list', page, pageSize, status] as const,
  detail: (version: number) => ['snapshots', 'detail', version] as const,
  compare: (version: number, compareWith: number) =>
    ['snapshots', 'compare', version, compareWith] as const,
  deployment: (version: number) => ['snapshots', 'deployment', version] as const,
}

export function useSnapshots(page: number, pageSize: number, status: SnapshotStatusFilter = null) {
  const api = useApi()

  return useQuery({
    // The status is part of the key, so switching it does not show the previous
    // filter's rows under the new heading.
    queryKey: snapshotKeys.list(page, pageSize, status),
    queryFn: ({ signal }) =>
      api.resources.snapshots.list({
        page,
        pageSize,
        // Omitted rather than sent empty: the API reads a missing status as "all".
        ...(status ? { status } : {}),
        signal,
      }) as Promise<SnapshotListResponse>,
    // Without this the table collapses to empty for a moment and then refills,
    // which reads as "no snapshots" rather than "loading".
    placeholderData: (previous) => previous,
  })
}

/**
 * The deployment state of one snapshot, per gateway.
 *
 * Fetched when a row is expanded rather than for every row on the page, since
 * most snapshots on a page have never been published and the answer is the same
 * for all of them.
 */
export function useSnapshotDeployment(version: number | undefined, enabled: boolean) {
  const api = useApi()

  return useQuery({
    queryKey: snapshotKeys.deployment(version ?? 0),
    queryFn: ({ signal }) => api.resources.snapshots.deployment(version!, { signal }),
    enabled: typeof version === 'number' && enabled,
  })
}

/**
 * A snapshot against the one before it.
 *
 * The API takes the *earlier* version as a query parameter, so a diff for the
 * first snapshot has nothing to compare against and is not offered.
 */
export function useSnapshotComparison(version: number, compareWith: number, enabled: boolean) {
  const api = useApi()

  return useQuery({
    queryKey: snapshotKeys.compare(version, compareWith),
    queryFn: ({ signal }) => api.resources.snapshots.compare(version, compareWith, { signal }),
    enabled: enabled && compareWith > 0 && compareWith !== version,
  })
}

export function useSnapshotMutations() {
  const api = useApi()
  const queryClient = useQueryClient()

  // Publishing and rolling back both change which snapshot is current, so the
  // overview and the publication list are stale too.
  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['snapshots'] }),
      queryClient.invalidateQueries({ queryKey: ['publications'] }),
      queryClient.invalidateQueries({ queryKey: ['overview'] }),
    ]).then(() => undefined)

  return {
    // Creating from the current management state is what the handler already
    // does; there is nothing to fill in, so it is one click.
    create: useMutation({
      mutationFn: (body: Parameters<typeof api.resources.snapshots.create>[0]) =>
        api.resources.snapshots.create(body),
      onSuccess: () =>
        Promise.all([
          queryClient.invalidateQueries({ queryKey: ['snapshots'] }),
          queryClient.invalidateQueries({ queryKey: ['overview'] }),
        ]).then(() => undefined),
    }),
    publish: useMutation({
      mutationFn: ({ version, body }: { version: number; body: Parameters<typeof api.resources.snapshots.publish>[1] }) =>
        api.resources.snapshots.publish(version, body),
      onSuccess: invalidate,
    }),
    rollback: useMutation({
      mutationFn: ({
        version,
        body,
      }: {
        version: number
        body: Parameters<typeof api.resources.snapshots.rollback>[1]
      }) => api.resources.snapshots.rollback(version, body),
      onSuccess: invalidate,
    }),
  }
}

/**
 * The validation results a snapshot actually carries, or null when the field is
 * absent altogether.
 *
 * Null is not the same as an empty list. An empty list means the API answered and
 * found no rules; a missing field means the answer did not include the column at
 * all — a server older than this page, or a partial response. Collapsing the
 * second into the first would report "not validated" for a snapshot whose
 * validation nobody looked at, which reads as reassurance.
 */
export function validationResultsOf(snapshot: {
  validationResults?: SnapshotValidationRule[] | null
}): SnapshotValidationRule[] | null {
  return Array.isArray(snapshot.validationResults) ? snapshot.validationResults : null
}

/** A snapshot's overall validation verdict, from its per-rule results. */
export function validationVerdict(snapshot: {
  validationResults?: SnapshotValidationRule[] | null
  status: string
}): { label: string; tone: 'ok' | 'bad' | 'warn' | 'mute' } {
  // An archived snapshot was valid when it was made; its status moved on since.
  if (snapshot.status.toLowerCase() === 'archived') {
    return { label: 'Archived', tone: 'mute' }
  }

  const results = validationResultsOf(snapshot)

  if (results === null) {
    // The column was not in the response at all. Saying "not validated" would
    // point at the snapshot; saying this points at the connection.
    return { label: 'Unknown', tone: 'mute' }
  }

  if (results.length === 0) {
    // No results is not a pass. Claiming one would mean a snapshot that was
    // never validated reads the same as one that passed.
    return { label: 'Not validated', tone: 'warn' }
  }

  const failed = results.filter((result) => !result.isValid)
  if (failed.length > 0) {
    return { label: `${failed.length} failed`, tone: 'bad' }
  }

  return { label: 'Valid', tone: 'ok' }
}

/**
 * Whether a snapshot has been published, and whether it still is what gateways run.
 *
 * Archiving does not clear `publishedAt`, so a snapshot that was published and
 * later replaced keeps that timestamp. Calling it "Published" would say it is
 * still current, which is the one thing an operator must not misread here.
 */
export function publicationState(
  snapshot: Pick<SnapshotResponse, 'publishedAt' | 'status'>,
): { label: string; tone: 'ok' | 'mute' } {
  if (!snapshot.publishedAt) return { label: 'Not published', tone: 'mute' }
  if (snapshot.status.toLowerCase() === 'archived') {
    return { label: 'Superseded', tone: 'mute' }
  }
  return { label: 'Published', tone: 'ok' }
}

/**
 * The four numbers the cards above the table show.
 *
 * The API has no summary endpoint, so these are counted from the rows that are
 * loaded. That is only the whole picture when the list is not paginated, so
 * `scope` says which it is — a count of one page presented as the total would
 * be wrong for exactly the large histories where an operator most wants it.
 */
export function snapshotSummary(
  snapshots: SnapshotResponse[],
  totalCount: number,
  pageSize: number,
): {
  publishedVersion: number | null
  readyCount: number
  failureCount: number
  scope: string
} {
  const published = snapshots.find(
    (snapshot) => snapshot.publishedAt !== null,
  )

  return {
    // The list is newest-first, so the first published row on the page is the
    // most recent one. Null means none is on this page, which is not the same as
    // none existing.
    publishedVersion: published?.version ?? null,
    readyCount: snapshots.filter((snapshot) => snapshot.status === 'Ready').length,
    failureCount: snapshots.filter(
      (snapshot) => validationResultsOf(snapshot)?.some((result) => !result.isValid),
    ).length,
    scope: totalCount > pageSize ? 'on this page' : 'in total',
  }
}

/**
 * A hash short enough to compare by eye, with enough left to tell two apart.
 *
 * The first twelve characters is what the rest of the industry shows, and the
 * full value is one click away.
 */
export function shortHash(hash: string): string {
  if (!hash) return '—'
  return hash.length <= 14 ? hash : `${hash.slice(0, 12)}…`
}

/**
 * The route and service counts, as a sentence.
 *
 * A count the API could not read stays a dash. Rendering it as `0` would claim
 * the snapshot is empty, which is a different and much more alarming fact than
 * "this one could not be read".
 */
export function describeComposition(
  snapshot: Pick<SnapshotResponse, 'routeCount' | 'serviceCount'> & {
    routeCount?: number | null
    serviceCount?: number | null
  },
): string {
  // `undefined` means the column was not in the response at all, which is a
  // different thing from a null the API sent deliberately. Both mean the same
  // thing to the operator, though: nobody can tell what is in here.
  const routes = snapshot.routeCount
  const services = snapshot.serviceCount

  if (routes == null && services == null) {
    return 'Contents could not be read'
  }

  return `${routes == null ? '—' : `${routes} routes`} · ${services == null ? '—' : `${services} services`}`
}

/**
 * Splits a diff line into a verdict and the text, when it carries one.
 *
 * The API returns differences as plain strings, so the part that says what
 * changed has to be pulled off the front for it to be styled.
 */
export function describeDifference(line: string): { kind: string | null; text: string } {
  const match = line.match(/^\s*(Added|Removed|Changed|added|removed|changed):\s*(.*)$/)
  if (!match) return { kind: null, text: line }
  return { kind: match[1].toLowerCase(), text: match[2] }
}

/** The outcome of a publish or rollback, as a sentence rather than a status code. */
export function describeDeployment(deployment: SnapshotDeploymentResponse): string {
  const status = deployment.status.toLowerCase()

  if (deployment.failureReason) return deployment.failureReason
  if (status.includes('complete') || status.includes('success')) {
    return `Snapshot #${deployment.snapshotVersion} was applied.`
  }
  if (status.includes('fail') || status.includes('error')) {
    return `Snapshot #${deployment.snapshotVersion} failed to apply.`
  }
  if (status.includes('progress') || status.includes('started')) {
    return `Snapshot #${deployment.snapshotVersion} is being applied.`
  }
  return `Snapshot #${deployment.snapshotVersion}: ${deployment.status}.`
}
