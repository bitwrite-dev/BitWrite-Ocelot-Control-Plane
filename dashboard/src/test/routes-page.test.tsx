import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'

import { createApiClient, type ApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { RoutesPage } from '@/features/routes/routes-page'
import { OverviewPage } from '@/features/overview/overview-page'

const SERVICES = {
  services: [
    { id: 'svc-users', name: 'users-api', description: null, downstreamTargets: [], createdAt: '', updatedAt: '' },
    { id: 'svc-orders', name: 'orders-api', description: null, downstreamTargets: [], createdAt: '', updatedAt: '' },
  ],
  totalCount: 2,
  page: 1,
  pageSize: 100,
}

function route(overrides: Record<string, unknown> = {}) {
  return {
    id: 'route-1',
    key: 'users-list',
    method: 'GET',
    upstreamPath: '/api/users',
    host: '',
    serviceId: 'svc-users',
    isEnabled: true,
    downstreamTargets: [{ host: 'localhost', port: 5001, scheme: 'http', path: '/' }],
    authenticationOptions: { allowedScopes: ['users.read'] },
    rateLimitOptions: { enableRateLimiting: true, period: 'Minute', limit: 100 },
    qoSOptions: null,
    cacheOptions: null,
    loadBalancerOptions: null,
    createdAt: '2026-01-02T03:04:05+00:00',
    updatedAt: '2026-01-02T03:04:05+00:00',
    ...overrides,
  }
}

interface StubOptions {
  routes?: unknown[]
  totalCount?: number
  status?: number
  /** Fails the routes list with this body, to exercise the error path. */
  listError?: { status: number; body: unknown }
}

/** Stubs fetch by path, so the real client still builds and encodes URLs. */
function stubFetch(options: StubOptions = {}) {
  const routes = options.routes ?? [route()]
  const calls: string[] = []

  const impl = vi.fn(async (input: RequestInfo | URL, _init?: RequestInit) => {
    const url = new URL(String(input), 'http://api.test')
    calls.push(url.pathname + url.search)

    if (url.pathname === '/api/v1/services') {
      return new Response(JSON.stringify(SERVICES), { status: 200 })
    }

    if (url.pathname === '/api/v1/routes') {
      if (options.listError) {
        return new Response(JSON.stringify(options.listError.body), {
          status: options.listError.status,
        })
      }
      return new Response(
        JSON.stringify({
          routes,
          totalCount: options.totalCount ?? routes.length,
          page: Number(url.searchParams.get('page') ?? 1),
          pageSize: Number(url.searchParams.get('pageSize') ?? 20),
        }),
        { status: 200 },
      )
    }

    // Mutations: enable / disable / delete
    return new Response('{}', { status: 200 })
  })

  return { impl, calls }
}

function renderRoutes(fetchImpl: ReturnType<typeof vi.fn>, initialPath = '/routes') {
  const client: ApiClient = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: fetchImpl as unknown as typeof fetch,
  })

  const router = createMemoryRouter(
    [
      {
        path: '/',
        children: [
          { path: 'routes', element: <RoutesPage /> },
          { path: '*', element: <OverviewPage /> },
        ],
      },
    ],
    { initialEntries: [initialPath] },
  )

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })}
    >
      <ApiClientContext.Provider value={client}>{children}</ApiClientContext.Provider>
    </QueryClientProvider>
  )

  render(<RouterProvider router={router} />, { wrapper })
  return client
}

describe('RoutesPage', () => {
  it('renders a row per route with its spec columns', async () => {
    const { impl } = stubFetch()
    renderRoutes(impl)

    expect(await screen.findByText('users-list')).toBeInTheDocument()
    expect(screen.getByText('/api/users')).toBeInTheDocument()
    // The service name also appears in the filter dropdown, so scope to the row.
    const row = screen.getByText('users-list').closest('tr') as HTMLElement
    expect(within(row).getByText('users-api')).toBeInTheDocument()
    expect(within(row).getByText('Enabled')).toBeInTheDocument()
    // Auth and rate-limit indicators
    expect(within(row).getByText('1 scope')).toBeInTheDocument()
    expect(within(row).getByText('100 / Minute')).toBeInTheDocument()
    expect(within(row).getByText('localhost:5001')).toBeInTheDocument()
  })

  it('shows an em dash where auth or rate limiting is absent', async () => {
    const { impl } = stubFetch({
      routes: [
        route({
          id: 'r2',
          key: 'plain',
          upstreamPath: '/api/plain',
          authenticationOptions: null,
          rateLimitOptions: { enableRateLimiting: false, period: '', limit: 0 },
        }),
      ],
    })
    renderRoutes(impl)

    expect(await screen.findByText('plain')).toBeInTheDocument()
    expect(screen.getByLabelText('No authentication')).toBeInTheDocument()
    expect(screen.getByLabelText('No rate limiting')).toBeInTheDocument()
  })

  it('sends paging, search and filters to the API rather than filtering locally', async () => {
    const { impl, calls } = stubFetch()
    renderRoutes(impl)

    await screen.findByText('users-list')

    // Change the status filter
    await userEvent.selectOptions(screen.getByLabelText('Filter by status'), 'false')
    await waitFor(() => {
      expect(calls.some((c) => c.includes('isEnabled=false'))).toBe(true)
    })

    // Filter by service
    await userEvent.selectOptions(screen.getByLabelText('Filter by service'), 'svc-orders')
    await waitFor(() => {
      expect(calls.some((c) => c.includes('serviceId=svc-orders'))).toBe(true)
    })
  })

  it('puts filter state in the URL so a filtered list can be linked', async () => {
    const { impl, calls } = stubFetch()
    renderRoutes(impl)

    await screen.findByText('users-list')
    await userEvent.selectOptions(screen.getByLabelText('Filter by status'), 'true')

    await waitFor(() => {
      expect(calls.some((c) => c.includes('isEnabled=true'))).toBe(true)
    })
    // Resetting to page 1 must drop any stale page number.
    expect(calls.every((c) => !c.includes('page=2'))).toBe(true)
  })

  it('reports the total count and honours the page size', async () => {
    const { impl } = stubFetch({ totalCount: 57 })
    renderRoutes(impl)

    expect(await screen.findByText(/Showing 1–20 of 57/)).toBeInTheDocument()
  })

  it('disables the row that is already disabled, enabling the one that is not', async () => {
    const { impl } = stubFetch({
      routes: [
        route({ id: 'r1', key: 'on', isEnabled: true }),
        route({ id: 'r2', key: 'off', isEnabled: false }),
      ],
    })
    renderRoutes(impl)

    await screen.findByText('on')
    const onRow = screen.getByText('on').closest('tr') as HTMLElement
    const offRow = screen.getByText('off').closest('tr') as HTMLElement

    expect(within(onRow).getByRole('button', { name: 'Disable' })).toBeInTheDocument()
    expect(within(offRow).getByRole('button', { name: 'Enable' })).toBeInTheDocument()
  })

  it('sends a PATCH when enabling and disables the button while it is in flight', async () => {
    const { impl, calls } = stubFetch({
      routes: [route({ id: 'r2', key: 'off', isEnabled: false })],
    })
    renderRoutes(impl)

    const row = (await screen.findByText('off')).closest('tr') as HTMLElement
    const enableButton = within(row).getByRole('button', { name: 'Enable' })

    await userEvent.click(enableButton)

    await waitFor(() => {
      const patchCall = impl.mock.calls.find(
        (call) => (call[1] as RequestInit | undefined)?.method === 'PATCH',
      )
      expect(patchCall).toBeDefined()
      expect(String(patchCall![0])).toContain('/api/v1/routes/r2/enable')
    })
    expect(calls.length).toBeGreaterThan(0)
  })

  it('requires confirmation before deleting', async () => {
    const { impl } = stubFetch()
    renderRoutes(impl)

    const row = (await screen.findByText('users-list')).closest('tr') as HTMLElement
    await userEvent.click(within(row).getByRole('button', { name: 'Delete' }))

    expect(await screen.findByText('Delete this route?')).toBeInTheDocument()

    const dialog = screen.getByRole('alertdialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    await waitFor(() => {
      expect(screen.queryByText('Delete this route?')).not.toBeInTheDocument()
    })
  })

  it('shows an empty state rather than a blank table when nothing matches', async () => {
    const { impl } = stubFetch({ routes: [], totalCount: 0 })
    renderRoutes(impl)

    expect(await screen.findByText('No records')).toBeInTheDocument()
  })

  it('surfaces the correlation id when the list request fails', async () => {
    const { impl } = stubFetch({
      listError: {
        status: 500,
        body: { correlationId: 'trace-xyz-789', error: 'Redis unavailable', type: 'DomainException' },
      },
    })
    renderRoutes(impl)

    expect(await screen.findByText('Request failed (500)')).toBeInTheDocument()
    expect(screen.getByText('Redis unavailable')).toBeInTheDocument()
    expect(screen.getByText(/trace-xyz-789/)).toBeInTheDocument()
  })

  it('distinguishes a network failure from an API error', async () => {
    const impl = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'))
    renderRoutes(impl)

    expect(await screen.findByText('Cannot reach the control plane API')).toBeInTheDocument()
  })
})
