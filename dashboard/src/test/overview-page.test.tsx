import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'

import { createApiClient, type ApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { OverviewPage } from '@/features/overview/overview-page'

const PUBLICATION = {
  current: {
    id: 'pub-1',
    snapshotVersion: 7,
    status: 'Published',
    initiatedBy: 'admin',
    startedAt: '2026-09-20T10:00:00+00:00',
    completedAt: '2026-09-20T10:05:00+00:00',
    failureReason: null,
    gatewayStates: [],
  },
  history: [],
}

const GATEWAYS = {
  gateways: [
    {
      gatewayId: 'gw-active',
      status: 'Active',
      currentVersion: 7,
      targetVersion: 7,
      lastHeartbeat: '2026-09-20T10:04:00+00:00',
      lastSynchronized: null,
      lastConfigApplied: '2026-09-20T10:05:00+00:00',
      runtimeInfo: {},
      capabilities: ['http'],
      activeRoutes: [],
    },
    {
      gatewayId: 'gw-behind',
      status: 'Degraded',
      currentVersion: 5,
      targetVersion: 7,
      lastHeartbeat: '2026-09-20T09:00:00+00:00',
      lastSynchronized: null,
      lastConfigApplied: null,
      runtimeInfo: {},
      capabilities: ['http'],
      activeRoutes: [],
    },
  ],
}

const SNAPSHOTS = {
  snapshots: [
    {
      version: 6,
      hash: 'a'.repeat(64),
      content: '{}',
      status: 'Archived',
      createdBy: 'admin',
      createdAt: '2026-09-19T10:00:00+00:00',
      publishedAt: '2026-09-19T10:01:00+00:00',
      archivedAt: '2026-09-20T10:00:00+00:00',
    },
    {
      version: 7,
      hash: 'b'.repeat(64),
      content: '{}',
      status: 'Published',
      createdBy: 'admin',
      createdAt: '2026-09-20T10:00:00+00:00',
      publishedAt: '2026-09-20T10:05:00+00:00',
      archivedAt: null,
    },
  ],
  totalCount: 2,
  page: 1,
  pageSize: 20,
}

function paged(totalCount: number) {
  return { routes: [], services: [], totalCount, page: 1, pageSize: 1 }
}

/** Stubs fetch by matching the request path, so the client still builds real URLs. */
function stubFetch(overrides: Record<string, unknown> = {}) {
  const responses: Record<string, unknown> = {
    '/api/v1/publications/current': PUBLICATION,
    '/api/v1/runtime/gateways': GATEWAYS,
    '/api/v1/snapshots': SNAPSHOTS,
    '/api/v1/routes': paged(12),
    '/api/v1/services': paged(4),
    ...overrides,
  }

  return vi.fn(async (input: RequestInfo | URL) => {
    const url = new URL(String(input), 'http://api.test')
    const path = url.pathname
    const body = responses[path]

    if (body === undefined) {
      return new Response(JSON.stringify({ error: `unstubbed ${path}` }), { status: 404 })
    }
    if (body instanceof Response) return body

    return new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    })
  })
}

/** Renders the page against an isolated cache and an injected client. */
function renderOverview(client: ApiClient) {
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })}
    >
      <ApiClientContext.Provider value={client}>{children}</ApiClientContext.Provider>
    </QueryClientProvider>
  )
  render(<OverviewPage />, { wrapper })
}

function renderPage(overrides?: Record<string, unknown>): ApiClient {
  const client = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: stubFetch(overrides) as unknown as typeof fetch,
  })
  renderOverview(client)
  return client
}

describe('OverviewPage', () => {
  it('shows the current published version', async () => {
    renderPage()

    const label = await screen.findByText('Current published version')
    // Scoped to the card: the same number appears in the gateway version columns.
    const card = label.closest('[data-slot="card"]') as HTMLElement
    expect(within(card).getByText('7')).toBeInTheDocument()
    expect(within(card).getByText('Published')).toBeInTheDocument()
    expect(within(card).getByText(/by admin/)).toBeInTheDocument()
  })

  it('uses totalCount for the route and service counts, not the page length', async () => {
    renderPage()

    // The stub returns an empty page with totalCount 12/4. A page-length read
    // would show 0.
    expect(await screen.findByText('12')).toBeInTheDocument()
    expect(screen.getByText('4')).toBeInTheDocument()
  })

  it('does not show a loading dash when a count is legitimately zero', async () => {
    // `0` is falsy in JS, so a truthiness check here would render "0 —" and imply
    // the count had not loaded yet.
    renderPage({ '/api/v1/routes': paged(0), '/api/v1/services': paged(0) })

    await screen.findByText('Current published version')
    expect(screen.queryByText('Loading…')).not.toBeInTheDocument()
  })

  it('renders a card per gateway with its status and version', async () => {
    renderPage()

    expect(await screen.findByText('gw-active')).toBeInTheDocument()
    expect(screen.getByText('gw-behind')).toBeInTheDocument()
    expect(screen.getByText('Active')).toBeInTheDocument()
    expect(screen.getByText('Degraded')).toBeInTheDocument()
  })

  it('flags a gateway that is behind its target version', async () => {
    renderPage()

    expect(await screen.findByText(/Behind target/)).toBeInTheDocument()
  })

  it('orders snapshot history newest first', async () => {
    renderPage()

    const history = await screen.findByRole('list')
    const items = within(history).getAllByRole('listitem')
    // The API returns 6 then 7; the list must reverse to newest first.
    expect(items[0]).toHaveTextContent('v7')
    expect(items[1]).toHaveTextContent('v6')
  })

  it('explains that nothing is published when current is null', async () => {
    renderPage({ '/api/v1/publications/current': { current: null, history: [] } })

    expect(await screen.findByText(/Nothing published yet/)).toBeInTheDocument()
  })

  it('shows an empty state rather than a blank list when no gateways exist', async () => {
    renderPage({ '/api/v1/runtime/gateways': { gateways: [] } })

    expect(await screen.findByText('No gateways registered')).toBeInTheDocument()
  })

  it('surfaces the correlation id when the API fails', async () => {
    renderPage({
      '/api/v1/publications/current': new Response(
        JSON.stringify({ correlationId: 'trace-abc-123', error: 'boom', type: 'DomainException' }),
        { status: 500, headers: { 'Content-Type': 'application/json' } },
      ),
    })

    expect(await screen.findByText(/Request failed \(500\)/)).toBeInTheDocument()
    expect(screen.getByText(/boom/)).toBeInTheDocument()
    expect(screen.getByText(/trace-abc-123/)).toBeInTheDocument()
  })

  it('distinguishes a network failure from an API error', async () => {
    const client = createApiClient({
      baseUrl: 'http://api.test',
      fetchImpl: vi.fn().mockRejectedValue(new TypeError('Failed to fetch')) as unknown as typeof fetch,
    })

    renderOverview(client)

    expect(await screen.findByText('Cannot reach the control plane API')).toBeInTheDocument()
  })

  it('queries the gateways endpoint, not runtime/status', async () => {
    const fetchImpl = stubFetch()
    const client = createApiClient({
      baseUrl: 'http://api.test',
      fetchImpl: fetchImpl as unknown as typeof fetch,
    })

    renderOverview(client)

    await screen.findByText('Current published version')

    const paths = fetchImpl.mock.calls.map((c) => new URL(String(c[0]), 'http://x').pathname)
    expect(paths).toContain('/api/v1/runtime/gateways')
    // /runtime/status answers 400 because it requires an undocumented gatewayId.
    expect(paths).not.toContain('/api/v1/runtime/status')
  })
})
