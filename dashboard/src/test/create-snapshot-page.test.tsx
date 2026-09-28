import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { CreateSnapshotPage } from '@/features/snapshots/create-snapshot-page'
import type { SnapshotResponse } from '@/api'

const PUBLISHED: SnapshotResponse = {
  version: 104,
  hash: 'sha256:aaaaaaaaaaaaaaaaaaaa',
  content: '{}',
  status: 'Published',
  createdBy: 'alex',
  createdAt: '2026-01-02T03:04:05Z',
  publishedAt: '2026-01-02T04:00:00Z',
  archivedAt: null,
  routeCount: 126,
  serviceCount: 18,
  pluginVersions: [],
  validationResults: [],
}

type Handler = (url: string, method: string, body: unknown) => Response

const ok = (payload: unknown) =>
  new Response(JSON.stringify(payload), {
    status: 200,
    headers: { 'content-type': 'application/json' },
  })

const fail = (status: number, error: string) =>
  new Response(JSON.stringify({ error }), {
    status,
    headers: { 'content-type': 'application/json' },
  })

function renderCreate(handler: Handler) {
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
      { path: '/snapshots/new', element: <CreateSnapshotPage /> },
      { path: '/snapshots', element: <p>Snapshot list</p> },
    ],
    { initialEntries: ['/snapshots/new'] },
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

/** Routes and services totals, as the create page reads them. */
const counts = (routes: number, services: number): Handler => (url) => {
  if (url.includes('/api/v1/routes')) return ok({ routes: [], totalCount: routes, page: 1, pageSize: 1 })
  if (url.includes('/api/v1/services')) {
    return ok({ services: [], totalCount: services, page: 1, pageSize: 1 })
  }
  if (url.includes('/api/v1/snapshots')) {
    return ok({ snapshots: [PUBLISHED], totalCount: 1, page: 1, pageSize: 20 })
  }
  return ok({})
}

describe('CreateSnapshotPage', () => {
  it('says plainly that creating does not publish', () => {
    // The single most expensive misunderstanding on this page: an operator who
    // believes a created snapshot is live will not publish it.
    renderCreate(counts(126, 18))

    expect(
      screen.getByText(/creating a snapshot does not publish it to gateways/i),
    ).toBeInTheDocument()
  })

  it('shows what the current management state contains before anything is created', async () => {
    // The point of the page: the decision is made against a visible state, not
    // against whatever happened to be there when a button was pressed.
    renderCreate(counts(126, 18))

    expect(await screen.findByText('126 routes')).toBeInTheDocument()
    expect(screen.getByText('18 services')).toBeInTheDocument()
  })

  it('does not claim zero when the counts could not be read', async () => {
    renderCreate(() => fail(500, 'down'))

    // "0 routes" would read as "about to seal an empty artifact", which is the
    // most damaging thing this page could say while the API is broken.
    expect(await screen.findByText('— routes')).toBeInTheDocument()
    expect(screen.getByText('— services')).toBeInTheDocument()
  })

  it('names the lifecycle rules that make a snapshot trustworthy', () => {
    renderCreate(counts(1, 1))

    expect(screen.getByText(/immutable by design/i)).toBeInTheDocument()
    expect(screen.getByText(/validation required/i)).toBeInTheDocument()
    expect(screen.getByText(/publication is separate/i)).toBeInTheDocument()
  })

  it('reports the currently published snapshot as the baseline to beat', async () => {
    renderCreate(counts(126, 18))

    expect(await screen.findByText('#104')).toBeInTheDocument()
    expect(
      screen.getByText(/use snapshot #104 as comparison baseline/i),
    ).toBeInTheDocument()
  })

  it('offers no baseline when nothing has been published, and says why', async () => {
    renderCreate((url) => {
      if (url.includes('/api/v1/routes')) return ok({ routes: [], totalCount: 3, page: 1, pageSize: 1 })
      if (url.includes('/api/v1/services')) {
        return ok({ services: [], totalCount: 2, page: 1, pageSize: 1 })
      }
      if (url.includes('/api/v1/snapshots')) {
        return ok({ snapshots: [], totalCount: 0, page: 1, pageSize: 20 })
      }
      return ok({})
    })

    expect(
      await screen.findByText(/no snapshot has been published yet/i),
    ).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: /use a published snapshot/i })).toBeDisabled()
  })

  it('admits the API has one source, so a baseline is a record and not a different artifact', async () => {
    renderCreate(counts(126, 18))

    await userEvent.click(await screen.findByRole('radio', { name: /use snapshot #104/i }))

    // Silently pretending the baseline changes what gets captured would make the
    // recorded hash describe a file the operator never saw.
    expect(
      screen.getByText(/the artifact is still built from the current management state/i),
    ).toBeInTheDocument()
  })

  it('tells the operator their label and summary are not stored yet', async () => {
    renderCreate(counts(1, 1))

    await userEvent.type(screen.getByLabelText(/snapshot label/i), 'payments policy')

    // Silently dropping what someone typed is worse than saying it will not be
    // kept, because they would assume the audit trail had it.
    expect(
      screen.getByText(/does not yet store the label or change summary/i),
    ).toBeInTheDocument()
  })

  it('says the Ocelot version cannot be changed here', () => {
    renderCreate(counts(1, 1))

    expect(
      screen.getByText(/the version is chosen once at setup/i),
    ).toBeInTheDocument()
  })

  it('creates the snapshot and reports that it is not live', async () => {
    const { calls } = renderCreate((url, method) =>
      method === 'POST'
        ? ok({ ...PUBLISHED, version: 105 })
        : counts(126, 18)(url, method, undefined),
    )

    await userEvent.click(await screen.findByRole('button', { name: /create snapshot/i }))

    await waitFor(() => {
      expect(
        calls.some((call) => call.url.endsWith('/api/v1/snapshots') && call.method === 'POST'),
      ).toBe(true)
    })

    expect(await screen.findByText(/snapshot #105 created/i)).toBeInTheDocument()
    expect(screen.getByText(/not running anywhere until it is published/i)).toBeInTheDocument()
  })

  it('reports a refused creation with the server\'s own reason', async () => {
    renderCreate((url, method) =>
      method === 'POST'
        ? fail(409, 'route conflicts detected: /payments')
        : counts(126, 18)(url, method, undefined),
    )

    await userEvent.click(await screen.findByRole('button', { name: /create snapshot/i }))

    expect(await screen.findByText('The snapshot was not created')).toBeInTheDocument()
    expect(screen.getByText(/route conflicts detected/i)).toBeInTheDocument()
  })

  it('names the permission a creation needs when the API refuses on role', async () => {
    renderCreate((url, method) =>
      method === 'POST' ? fail(403, 'Forbidden') : counts(1, 1)(url, method, undefined),
    )

    await userEvent.click(await screen.findByRole('button', { name: /create snapshot/i }))

    expect(await screen.findByText('Not permitted')).toBeInTheDocument()
    expect(screen.getByText(/snapshotmanager or admin role/i)).toBeInTheDocument()
  })

  it('leaves the page when cancelled, having created nothing', async () => {
    const { calls } = renderCreate(counts(1, 1))

    await userEvent.click(screen.getByRole('button', { name: /cancel/i }))

    expect(await screen.findByText('Snapshot list')).toBeInTheDocument()
    expect(calls.some((call) => call.method === 'POST')).toBe(false)
  })
})
