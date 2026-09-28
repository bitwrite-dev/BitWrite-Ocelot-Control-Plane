import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { CreateSnapshotPage } from '@/features/snapshots/create-snapshot-page'
import type { PreviewSnapshotResponse, SnapshotResponse } from '@/api'

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

const CLEAN_PREVIEW: PreviewSnapshotResponse = {
  content: '{"Routes":[]}',
  hash: 'sha256:previewhash0000000000000000000000000000000000000000000000000000000',
  routeCount: 2,
  serviceCount: 1,
  pluginVersions: [],
  validationResults: [
    { rule: 'RouteConflicts', isValid: true, message: null },
    { rule: 'References', isValid: true, message: null },
    { rule: 'OcelotCapabilities', isValid: true, message: null },
  ],
  isValid: true,
  ocelotVersion: '20.0.0',
  nextVersion: 105,
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

const baseline =
  (preview: PreviewSnapshotResponse = CLEAN_PREVIEW, created?: SnapshotResponse): Handler =>
  (url, method) => {
    if (url.endsWith('/preview')) return ok(preview)
    if (url.endsWith('/api/v1/snapshots') && method === 'POST') {
      return ok(created ?? { ...PUBLISHED, version: 105, status: 'Ready', publishedAt: null })
    }
    if (url.includes('/api/v1/routes')) {
      return ok({ routes: [], totalCount: 2, page: 1, pageSize: 1 })
    }
    if (url.includes('/api/v1/services')) {
      return ok({ services: [], totalCount: 1, page: 1, pageSize: 1 })
    }
    if (url.includes('/api/v1/snapshots')) {
      return ok({ snapshots: [PUBLISHED], totalCount: 1, page: 1, pageSize: 20 })
    }
    return ok({})
  }

/** Walks from step 1 to step 3, which is what a passing validation unlocks. */
async function advanceToSeal() {
  await userEvent.click(await screen.findByRole('button', { name: /continue to validation/i }))
  await screen.findByText('Resolved configuration')
  await userEvent.click(await screen.findByRole('button', { name: /continue to review/i }))
}

describe('the three steps', () => {
  it('starts on step one, not on the seal', () => {
    renderCreate(baseline())

    expect(screen.getByText('Step 1 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /continue to validation/i })).toBeInTheDocument()
  })

  it('does not run the validation before the operator asks for it', () => {
    // A preview is a server round trip; running it on arrival would resolve an
    // artifact for a page the operator may immediately leave.
    const { calls } = renderCreate(baseline())

    expect(calls.some((call) => call.url.endsWith('/preview'))).toBe(false)
  })

  it('really runs the validation when the step is reached', async () => {
    const { calls } = renderCreate(baseline())

    await userEvent.click(await screen.findByRole('button', { name: /continue to validation/i }))

    await waitFor(() => {
      expect(calls.some((call) => call.url.endsWith('/preview') && call.method === 'POST')).toBe(true)
    })
    expect(await screen.findByText('Step 2 of 3')).toBeInTheDocument()
  })

  it('shows the hash the artifact would carry, not one it might get', async () => {
    renderCreate(baseline())

    await userEvent.click(await screen.findByRole('button', { name: /continue to validation/i }))

    // The server computed this over the exact string it would store, so the
    // operator sees the hash rather than trusting that one will appear later.
    expect(await screen.findByText('sha256:previ…')).toBeInTheDocument()
  })

  it('lists every rule it ran, not just the verdict', async () => {
    renderCreate(baseline())

    await userEvent.click(await screen.findByRole('button', { name: /continue to validation/i }))

    expect(await screen.findByText('RouteConflicts')).toBeInTheDocument()
    expect(screen.getByText('References')).toBeInTheDocument()
    expect(screen.getByText('OcelotCapabilities')).toBeInTheDocument()
  })

  it('offers the seal only after validation passed', async () => {
    renderCreate(baseline())

    await userEvent.click(await screen.findByRole('button', { name: /continue to validation/i }))
    await screen.findByText('Step 2 of 3')

    // Sealing an artifact that failed would record a broken configuration as
    // the desired state.
    expect(screen.getByRole('button', { name: /continue to review/i })).toBeEnabled()
  })

  it('refuses to move past a failed validation', async () => {
    renderCreate(
      baseline({
        ...CLEAN_PREVIEW,
        isValid: false,
        validationResults: [
          {
            rule: 'RouteConflicts',
            isValid: false,
            message: 'Overlapping routes: GET:/api/orders localhost',
          },
        ],
      }),
    )

    await userEvent.click(await screen.findByRole('button', { name: /continue to validation/i }))

    expect(await screen.findByText('Validation failed')).toBeInTheDocument()
    expect(screen.getByText(/overlapping routes/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /continue to review/i })).toBeDisabled()
  })

  it('reports that nothing could be built, rather than offering an empty artifact', async () => {
    renderCreate(
      baseline({
        ...CLEAN_PREVIEW,
        content: null,
        hash: null,
        isValid: false,
        validationResults: [
          {
            rule: 'ConfigurationBuild',
            isValid: false,
            message: 'The management state could not be turned into a configuration',
          },
        ],
      }),
    )

    await userEvent.click(await screen.findByRole('button', { name: /continue to validation/i }))

    expect(
      await screen.findByText(/nothing could be built from the current state/i),
    ).toBeInTheDocument()
    expect(screen.getByText(/no hash — nothing was built/i)).toBeInTheDocument()
  })

  it('shows a warning without blocking, because clearing a gateway is deliberate', async () => {
    renderCreate(
      baseline({
        ...CLEAN_PREVIEW,
        routeCount: 0,
        validationResults: [
          { rule: 'HasContent', isValid: false, message: 'Warning: no routes are configured.' },
        ],
      }),
    )

    await userEvent.click(await screen.findByRole('button', { name: /continue to validation/i }))

    expect(await screen.findByText(/warning: no routes are configured/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /continue to review/i })).toBeEnabled()
  })

  it('names the version it will take, so the seal is not a surprise', async () => {
    renderCreate(baseline())

    await advanceToSeal()

    expect(await screen.findByText(/becomes snapshot #105/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /create snapshot #105/i })).toBeInTheDocument()
  })

  it('creates the snapshot and says it is not live yet', async () => {
    const { calls } = renderCreate(baseline())

    await advanceToSeal()
    await userEvent.click(screen.getByRole('button', { name: /create snapshot #105/i }))

    await waitFor(() => {
      expect(
        calls.some((call) => call.url.endsWith('/api/v1/snapshots') && call.method === 'POST'),
      ).toBe(true)
    })

    expect(await screen.findByText('Snapshot #105 created')).toBeInTheDocument()
    expect(screen.getByText(/not running anywhere until it is published/i)).toBeInTheDocument()
  })

  it('lets the operator re-run validation after editing the state', async () => {
    const { calls } = renderCreate(baseline())

    await userEvent.click(await screen.findByRole('button', { name: /continue to validation/i }))
    await screen.findByText('Step 2 of 3')
    await userEvent.click(screen.getByRole('button', { name: /re-run validation/i }))

    await waitFor(() => {
      const previews = calls.filter((call) => call.url.endsWith('/preview'))
      expect(previews.length).toBeGreaterThan(1)
    })
  })
})

describe('what the source step shows', () => {
  it('counts what is about to be sealed', async () => {
    renderCreate(baseline())

    expect(await screen.findByText('2 routes')).toBeInTheDocument()
    expect(screen.getByText('1 services')).toBeInTheDocument()
  })

  it('does not claim zero when the counts could not be read', async () => {
    renderCreate(() => fail(500, 'down'))

    // "0 routes" would read as "about to seal an empty artifact", which is the
    // most damaging thing this page could say while the API is broken.
    expect(await screen.findByText('— routes')).toBeInTheDocument()
  })

  it('says plainly that creating does not publish', () => {
    renderCreate(baseline())

    expect(
      screen.getByText(/creating a snapshot does not publish it to gateways/i),
    ).toBeInTheDocument()
  })

  it('offers no baseline when nothing has been published', async () => {
    renderCreate((url) => {
      if (url.includes('/api/v1/routes')) return ok({ routes: [], totalCount: 0, page: 1, pageSize: 1 })
      if (url.includes('/api/v1/services')) {
        return ok({ services: [], totalCount: 0, page: 1, pageSize: 1 })
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

  it('admits the API has one source, so a baseline is a record and not a different artifact', () => {
    renderCreate(baseline())

    // Silently pretending the baseline changes what gets captured would make the
    // recorded hash describe a file the operator never saw.
    expect(
      screen.getByText(/a baseline only records what this snapshot will be judged against/i),
    ).toBeInTheDocument()
  })

  it('tells the operator their label and summary are not stored yet', async () => {
    renderCreate(baseline())

    await advanceToSeal()
    await userEvent.type(screen.getByLabelText(/snapshot label/i), 'payments policy')

    // Silently dropping what someone typed is worse than saying it will not be
    // kept, because they would assume the audit trail had it.
    expect(
      screen.getByText(/does not yet store the label or change summary/i),
    ).toBeInTheDocument()
  })
})
