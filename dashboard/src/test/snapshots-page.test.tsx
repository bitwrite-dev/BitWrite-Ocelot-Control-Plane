import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { SnapshotsPage } from '@/features/snapshots/snapshots-page'
import {
  describeComposition,
  describeDeployment,
  snapshotSummary,
  describeDifference,
  publicationState,
  shortHash,
  validationVerdict,
} from '@/features/snapshots/queries'
import type { SnapshotDeploymentResponse, SnapshotResponse } from '@/api'

const BASE: SnapshotResponse = {
  version: 4,
  hash: 'sha256:abcdef0123456789abcdef',
  content: '{}',
  status: 'Active',
  createdBy: 'operator',
  createdAt: '2026-01-02T03:04:05Z',
  publishedAt: null,
  archivedAt: null,
  routeCount: 2,
  serviceCount: 1,
  pluginVersions: [],
  validationResults: [],
}

const EARLIER: SnapshotResponse = {
  ...BASE,
  version: 3,
  hash: 'sha256:9876543210fedcba9876',
  status: 'Archived',
  publishedAt: '2026-01-01T00:00:00Z',
  archivedAt: '2026-01-05T00:00:00Z',
}

const PUBLISHED_DEPLOYMENT: SnapshotDeploymentResponse = {
  publicationId: 'pub-1',
  snapshotVersion: 4,
  status: 'Completed',
  startedAt: '2026-01-02T04:00:00Z',
  completedAt: '2026-01-02T04:00:02Z',
  failureReason: null,
}

type Handler = (url: string, method: string, body: unknown) => Response

function list(snapshots: SnapshotResponse[], totalCount = snapshots.length, page = 1) {
  return { snapshots, totalCount, page, pageSize: 20 }
}

function renderSnapshots(handler: Handler) {
  const calls: { url: string; method: string; body: unknown }[] = []

  const fetchImpl = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    const body = init?.body ? JSON.parse(String(init.body)) : undefined
    calls.push({ url, method, body })
    return handler(url, method, body)
  })

  const client = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: fetchImpl as unknown as typeof fetch,
  })

  const router = createMemoryRouter(
    [
      { path: '/snapshots', element: <SnapshotsPage /> },
      { path: '/snapshots/new', element: <p>Create configuration snapshot</p> },
    ],
    { initialEntries: ['/snapshots'] },
  )

  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })}
    >
      <ApiClientContext.Provider value={client}>
        <RouterProvider router={router} />
      </ApiClientContext.Provider>
    </QueryClientProvider>,
  )

  return { calls }
}

const ok = (payload: unknown) =>
  new Response(JSON.stringify(payload), {
    status: 200,
    headers: { 'content-type': 'application/json' },
  })

// The wire shape the API actually returns for a failure; `message` would be
// dropped by the client, which would hide the server's own explanation.
const fail = (status: number, error: string) =>
  new Response(JSON.stringify({ error }), {
    status,
    headers: { 'content-type': 'application/json' },
  })

const listOnly =
  (snapshots: SnapshotResponse[]): Handler =>
  (url) =>
    url.includes('/api/v1/snapshots?')
      ? ok(list(snapshots))
      : fail(404, `unexpected request: ${url}`)

describe('SnapshotsPage', () => {
  beforeEach(() => {
    vi.useRealTimers()
  })

  it('lists each snapshot with its version, status, contents and creator', async () => {
    renderSnapshots(listOnly([BASE, EARLIER]))

    const row = await screen.findByRole('row', { name: /#4/ })
    within(row).getByText('2 routes · 1 services')
    within(row).getByText(/operator/)
  })

  it('asks the API for a page rather than every snapshot at once', async () => {
    const { calls } = renderSnapshots(listOnly([BASE]))

    await screen.findByRole('row', { name: /#4/ })
    expect(calls[0].url).toContain('page=1')
    expect(calls[0].url).toContain('pageSize=20')
  })

  it('shortens the hash for reading and keeps the whole value in the title', async () => {
    renderSnapshots(listOnly([BASE]))

    const code = await screen.findByTitle(BASE.hash)
    expect(code.textContent).toBe('sha256:abcde…')
  })

  it('says so plainly when there are no snapshots', async () => {
    renderSnapshots(listOnly([]))

    expect(await screen.findByText(/no snapshots yet/i)).toBeInTheDocument()
  })

  it('offers a retry when the list cannot be loaded, and reloads on demand', async () => {
    let attempt = 0
    renderSnapshots((_url) => {
      attempt += 1
      return attempt === 1 ? fail(500, 'the store is down') : ok(list([BASE]))
    })

    expect(await screen.findByText('Could not load snapshots')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /try again/i }))

    expect(await screen.findByRole('row', { name: /#4/ })).toBeInTheDocument()
  })

  it('does not call a snapshot validated when no results were recorded', async () => {
    // An empty result set is an unknown, not a pass. Reporting it as valid would
    // mean a snapshot nobody ever checked reads like one that was checked.
    renderSnapshots(listOnly([BASE]))

    const row = await screen.findByRole('row', { name: /#4/ })
    within(row).getByText('Not validated')
    expect(within(row).queryByText('Valid')).not.toBeInTheDocument()
  })

  it('counts the failed rules rather than hiding them behind a verdict', async () => {
    renderSnapshots(
      listOnly([
        {
          ...BASE,
          validationResults: [
            { rule: 'RoutesResolve', isValid: true, message: null },
            { rule: 'ServicesResolve', isValid: false, message: 'service unknown' },
            { rule: 'PluginsResolve', isValid: false, message: 'plugin missing' },
          ],
        },
      ]),
    )

    const row = await screen.findByRole('row', { name: /#4/ })
    within(row).getByText('2 failed')
  })

  it('does not claim a snapshot is empty when its contents could not be read', async () => {
    renderSnapshots(listOnly([{ ...BASE, routeCount: null, serviceCount: null }]))

    const row = await screen.findByRole('row', { name: /#4/ })
    // "0 routes" would say the snapshot is empty. A dash says nobody can tell,
    // which is a different fact and the only honest one here.
    within(row).getByText('Contents could not be read')
    expect(within(row).queryByText(/0 routes/)).not.toBeInTheDocument()
  })

  it('shows a real zero as zero rather than as a dash', async () => {
    renderSnapshots(listOnly([{ ...BASE, routeCount: 0, serviceCount: 0 }]))

    const row = await screen.findByRole('row', { name: /#4/ })
    within(row).getByText('0 routes · 0 services')
  })

  it('distinguishes what gateways run now from what was published once', async () => {
    renderSnapshots(listOnly([BASE, EARLIER]))

    const current = await screen.findByRole('row', { name: /#4/ })
    within(current).getByText('Not published')

    // EARLIER is archived with a publishedAt in the past: it was published, but a
    // later snapshot replaced it, so it must not read as current.
    const old = screen.getByRole('row', { name: /#3/ })
    within(old).getByText('Superseded')
  })

  it('sends the operator to the create page rather than creating from here', async () => {
    const { calls } = renderSnapshots(listOnly([BASE]))

    await userEvent.click(await screen.findByRole('button', { name: /create snapshot/i }))

    // Creating seals whatever the management state happens to hold, so it takes
    // a page that shows that state first. A one-click create here would capture
    // it unseen.
    expect(await screen.findByText('Create configuration snapshot')).toBeInTheDocument()
    expect(calls.some((call) => call.method === 'POST')).toBe(false)
  })

  it('filters by status on the server, which is the only place the full set exists', async () => {
    const { calls } = renderSnapshots((url) =>
      url.includes('status=')
        ? ok(list([EARLIER]))
        : listOnly([BASE])(url, 'GET', undefined),
    )

    await userEvent.click(await screen.findByRole('combobox', { name: 'Status' }))
    await userEvent.click(await screen.findByRole('option', { name: 'Archived' }))

    await waitFor(() => {
      expect(calls.some((call) => call.url.includes('status=Archived'))).toBe(true)
    })
  })

  it('does not send a status when the filter is All', async () => {
    // The API reads a missing status as "everything"; an empty string would be
    // a value it cannot parse.
    const { calls } = renderSnapshots(listOnly([BASE]))

    await screen.findByRole('combobox', { name: 'Status' })

    expect(calls[0].url).not.toContain('status=')
  })

  it('goes back to the first page when the filter changes', async () => {
    // Otherwise a narrow filter can leave the operator on page 7 of a list that
    // no longer has seven pages.
    const { calls } = renderSnapshots((url) =>
      url.includes('status=')
        ? ok(list([EARLIER]))
        : ok(list([BASE, EARLIER], 45, 3)),
    )

    await userEvent.click(await screen.findByRole('button', { name: 'Next' }))
    await userEvent.click(await screen.findByRole('combobox', { name: 'Status' }))
    await userEvent.click(await screen.findByRole('option', { name: 'Archived' }))

    await waitFor(() => {
      expect(calls.some((call) => call.url.includes('page=1') && call.url.includes('status=Archived'))).toBe(true)
    })
  })

  it('says when a filter matched nothing, rather than implying there are no snapshots', async () => {
    renderSnapshots((url) =>
      url.includes('status=') ? ok(list([], 0)) : listOnly([BASE])(url, 'GET', undefined),
    )

    await userEvent.click(await screen.findByRole('combobox', { name: 'Status' }))
    await userEvent.click(await screen.findByRole('option', { name: 'Published' }))

    expect(await screen.findByText(/no snapshots with the status published/i)).toBeInTheDocument()
  })

  it('opens the detail view with the validation results and their messages', async () => {
    renderSnapshots(
      listOnly([
        {
          ...BASE,
          validationResults: [
            { rule: 'RoutesResolve', isValid: true, message: null },
            { rule: 'ServicesResolve', isValid: false, message: 'service unknown' },
          ],
        },
      ]),
    )

    await userEvent.click(await screen.findByRole('button', { name: 'View' }))

    expect(await screen.findByText('2 rules checked')).toBeInTheDocument()
    expect(screen.getByText('RoutesResolve')).toBeInTheDocument()
    expect(screen.getByText('service unknown')).toBeInTheDocument()
  })

  it('does not present a never-published snapshot as deployed to gateways', async () => {
    renderSnapshots(listOnly([BASE]))

    await userEvent.click(await screen.findByRole('button', { name: 'View' }))

    expect(
      await screen.findByText(/never been published, so no gateway is running it/i),
    ).toBeInTheDocument()
  })

  it('shows the publication state of a snapshot that was deployed', async () => {
    renderSnapshots((url) =>
      url.includes('/deployment')
        ? ok(PUBLISHED_DEPLOYMENT)
        : listOnly([BASE])(url, 'GET', undefined),
    )

    await userEvent.click(await screen.findByRole('button', { name: 'View' }))

    expect(await screen.findByText('Completed')).toBeInTheDocument()
    expect(screen.getByText('pub-1')).toBeInTheDocument()
  })

  it('diffs a snapshot against the one before it and passes the other version as a parameter', async () => {
    const { calls } = renderSnapshots((url) => {
      if (url.includes('/compare')) {
        return ok({ versionA: 4, versionB: 3, differences: ['Added: /b', 'Removed: /a'] })
      }
      return listOnly([BASE, EARLIER])(url, 'GET', undefined)
    })

    // Two rows are loaded, so there are two Compare actions; the first belongs
    // to the newest snapshot (#4), whose diff target is #3.
    const compareButtons = await screen.findAllByRole('button', { name: 'Compare' })
    await userEvent.click(compareButtons[0])

    // The sheet is titled with the pair, so the operator can tell which two
    // snapshots the diff is about without reading the lines.
    const sheet = await screen.findByRole('dialog')
    expect(sheet).toHaveTextContent('#4 compared with #3')
    expect(await screen.findByText('/b')).toBeInTheDocument()

    const compareCall = calls.find((call) => call.url.includes('/compare'))
    // Without the query parameter the API has nothing to diff against.
    expect(compareCall?.url).toContain('compareWith=3')
  })

  it('does not offer a diff for the earliest snapshot, which has nothing to compare to', async () => {
    renderSnapshots(listOnly([EARLIER]))

    // One row loaded, so there is nothing older to diff it against; the action
    // stays disabled rather than opening an empty comparison.
    await screen.findByRole('button', { name: 'Compare' })
    expect(screen.getByRole('button', { name: 'Compare' })).toBeDisabled()
  })

  it('publishes a snapshot when asked', async () => {
    const { calls } = renderSnapshots((url, method) =>
      method === 'POST' ? ok(PUBLISHED_DEPLOYMENT) : listOnly([BASE])(url, method, undefined),
    )

    await userEvent.click(await screen.findByRole('button', { name: /publish #4/i }))

    await waitFor(() => {
      expect(calls.some((call) => call.url.endsWith('/4/publish') && call.method === 'POST')).toBe(
        true,
      )
    })
  })

  it('will not let a rollback through without a reason, because the API requires one', async () => {
    renderSnapshots(listOnly([BASE, EARLIER]))

    await userEvent.click(await screen.findByRole('button', { name: /roll back to #4/i }))

    const confirm = await screen.findByRole('button', { name: /roll back to #3/i })
    await userEvent.click(confirm)

    // The reason is an audit record, not a nicety: an empty one would be
    // rejected by the API after the operator thought it was accepted.
    expect(
      await screen.findByText(/the api requires a reason, so this cannot be submitted blank/i),
    ).toBeInTheDocument()
  })

  it('says that a rollback republishes rather than deletes', async () => {
    renderSnapshots(listOnly([BASE, EARLIER]))

    await userEvent.click(await screen.findByRole('button', { name: /roll back to #4/i }))

    expect(
      await screen.findByText(/every gateway will be published #3 again/i),
    ).toBeInTheDocument()
  })

  it('sends the target version and the reason when a rollback is confirmed', async () => {
    const { calls } = renderSnapshots((url, method) =>
      method === 'POST'
        ? ok({ ...PUBLISHED_DEPLOYMENT, snapshotVersion: 3, status: 'Completed' })
        : listOnly([BASE, EARLIER])(url, method, undefined),
    )

    await userEvent.click(await screen.findByRole('button', { name: /roll back to #4/i }))
    await userEvent.type(await screen.findByLabelText(/reason/i), 'bad route change')
    await userEvent.click(screen.getByRole('button', { name: /roll back to #3/i }))

    await waitFor(() => {
      const rollback = calls.find((call) => call.url.endsWith('/4/rollback'))
      expect(rollback).toBeDefined()
      expect(rollback?.body).toMatchObject({ targetVersion: 3, reason: 'bad route change' })
    })
  })

  it('reports what the rollback actually did, rather than only that it was accepted', async () => {
    renderSnapshots((url, method) =>
      method === 'POST'
        ? ok({ ...PUBLISHED_DEPLOYMENT, snapshotVersion: 3, status: 'Completed' })
        : listOnly([BASE, EARLIER])(url, method, undefined),
    )

    await userEvent.click(await screen.findByRole('button', { name: /roll back to #4/i }))
    await userEvent.type(await screen.findByLabelText(/reason/i), 'bad route change')
    await userEvent.click(screen.getByRole('button', { name: /roll back to #3/i }))

    // "It worked" hides whether the intended version is what gateways now run.
    expect(await screen.findByText('Snapshot #3 was applied.')).toBeInTheDocument()
  })

  it('explains a refused rollback instead of closing the dialog as if it succeeded', async () => {
    renderSnapshots((url, method) =>
      method === 'POST'
        ? fail(409, 'snapshot 3 is not deployable')
        : listOnly([BASE, EARLIER])(url, method, undefined),
    )

    await userEvent.click(await screen.findByRole('button', { name: /roll back to #4/i }))
    await userEvent.type(await screen.findByLabelText(/reason/i), 'bad route change')
    await userEvent.click(screen.getByRole('button', { name: /roll back to #3/i }))

    expect(await screen.findByText('The rollback was refused')).toBeInTheDocument()
    expect(screen.getByText(/not deployable/i)).toBeInTheDocument()
  })

  it('names the permission a rollback needs when the API refuses on role', async () => {
    renderSnapshots((url, method) =>
      method === 'POST' ? fail(403, 'forbidden') : listOnly([BASE, EARLIER])(url, method, undefined),
    )

    await userEvent.click(await screen.findByRole('button', { name: /roll back to #4/i }))
    await userEvent.type(await screen.findByLabelText(/reason/i), 'bad route change')
    await userEvent.click(screen.getByRole('button', { name: /roll back to #3/i }))

    expect(await screen.findByText('Not permitted')).toBeInTheDocument()
    expect(screen.getByText(/snapshotmanager or admin role/i)).toBeInTheDocument()
  })
})

describe('the summary cards', () => {
  it('reports the newest published version and the counts beside it', () => {
    const summary = snapshotSummary(
      [
        { ...BASE, version: 5, publishedAt: '2026-01-03T00:00:00Z' },
        { ...BASE, version: 4, status: 'Ready' },
        { ...BASE, version: 3, status: 'Ready' },
      ],
      3,
      20,
    )

    // The list is newest-first, so the first published row is the current one.
    expect(summary.publishedVersion).toBe(5)
    expect(summary.readyCount).toBe(2)
    expect(summary.failureCount).toBe(0)
  })

  it('counts a snapshot with a failing rule as a failure', () => {
    const summary = snapshotSummary(
      [
        {
          ...BASE,
          validationResults: [
            { rule: 'R', isValid: true, message: null },
            { rule: 'S', isValid: false, message: 'unknown' },
          ],
        },
      ],
      1,
      20,
    )

    expect(summary.failureCount).toBe(1)
  })

  it('scopes its counts to the page when the list is longer than one page', () => {
    // 105 snapshots over 20-row pages: presenting one page's count as the total
    // would be wrong in exactly the large histories where it matters most.
    const summary = snapshotSummary([{ ...BASE, status: 'Ready' }], 105, 20)

    expect(summary.scope).toBe('on this page')
  })

  it('claims a total only when the list really is one page', () => {
    expect(snapshotSummary([BASE], 12, 20).scope).toBe('in total')
  })

  it('reports no published version rather than guessing at one', () => {
    const summary = snapshotSummary([{ ...BASE, status: 'Ready' }], 1, 20)

    // None on this page is not the same as none anywhere; the caller says which.
    expect(summary.publishedVersion).toBeNull()
  })
})

describe('snapshot presentation', () => {
  it('reads as valid only when there is a result and none of them failed', () => {
    expect(
      validationVerdict({
        status: 'Active',
        validationResults: [{ rule: 'R', isValid: true, message: null }],
      }),
    ).toEqual({ label: 'Valid', tone: 'ok' })
  })

  it('does not re-judge an archived snapshot by its old results', () => {
    // The snapshot was valid when it was made; its status moved on since.
    expect(
      validationVerdict({
        status: 'Archived',
        validationResults: [{ rule: 'R', isValid: false, message: 'x' }],
      }),
    ).toEqual({ label: 'Archived', tone: 'mute' })
  })

  it('separates never published from published', () => {
    expect(publicationState({ publishedAt: null, status: 'Active' })).toEqual({
      label: 'Not published',
      tone: 'mute',
    })
    expect(publicationState({ publishedAt: '2026-01-01T00:00:00Z', status: 'Active' })).toEqual({
      label: 'Published',
      tone: 'ok',
    })
  })

  it('does not call an archived snapshot published, when it is no longer current', () => {
    // Archiving leaves publishedAt set, so the timestamp alone would say this is
    // still what gateways run. It is not: a later snapshot replaced it.
    expect(
      publicationState({ publishedAt: '2026-01-01T00:00:00Z', status: 'Archived' }),
    ).toEqual({ label: 'Superseded', tone: 'mute' })
  })

  it('keeps a short hash intact and shortens a long one', () => {
    expect(shortHash('abc123')).toBe('abc123')
    expect(shortHash('0123456789abcdef0123')).toBe('0123456789ab…')
    expect(shortHash('')).toBe('—')
  })

  it('keeps an unknown count as a dash when only one side is unreadable', () => {
    expect(describeComposition({ routeCount: 3, serviceCount: null })).toBe('3 routes · —')
    expect(describeComposition({ routeCount: null, serviceCount: 1 })).toBe('— · 1 services')
  })

  it('pulls the verdict off a diff line so the rest can be styled', () => {
    expect(describeDifference('Added: /b')).toEqual({ kind: 'added', text: '/b' })
    expect(describeDifference('Removed: /a')).toEqual({ kind: 'removed', text: '/a' })
  })

  it('leaves a diff line alone when it carries no verdict', () => {
    expect(describeDifference('/a → /b')).toEqual({ kind: null, text: '/a → /b' })
  })

  it('reports the version that was applied, not just that something was', () => {
    expect(
      describeDeployment({ ...PUBLISHED_DEPLOYMENT, snapshotVersion: 3, status: 'Completed' }),
    ).toBe('Snapshot #3 was applied.')
  })

  it('surfaces the failure reason in preference to a generic status', () => {
    expect(
      describeDeployment({
        ...PUBLISHED_DEPLOYMENT,
        status: 'Failed',
        failureReason: 'gateway edge-eu refused the config',
      }),
    ).toBe('gateway edge-eu refused the config')
  })
})
