import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { RouteDetailsPage } from '@/features/routes/route-details-page'

const ROUTE = {
  id: 'route-1',
  key: 'users-list',
  method: 'GET',
  upstreamPath: '/api/users',
  host: 'api.example.com',
  serviceId: 'svc-users',
  isEnabled: true,
  downstreamTargets: [{ host: 'localhost', port: 5001, scheme: 'http', path: '/' }],
  authenticationOptions: { allowedScopes: ['users.read', 'users.write'] },
  rateLimitOptions: { enableRateLimiting: true, period: 'Minute', limit: 100 },
  qoSOptions: { timeoutSeconds: 90, circuitBreakerTimeoutSeconds: 30 },
  cacheOptions: { ttlSeconds: 300 },
  loadBalancerOptions: { algorithm: 'RoundRobin' },
  downstreamMethod: 'GET',
  downstreamHttpVersion: '2.0',
  downstreamHttpVersionPolicy: 'RequestVersionExact',
  dangerousAcceptAnyServerCertificateValidator: false,
  delegatingHandlers: [],
  httpClientOptions: null,
  timeoutSeconds: 45,
  downstreamPathTemplate: null,
  headerTransformations: null,
  createdAt: '2026-01-02T03:04:05+00:00',
  updatedAt: '2026-01-03T04:05:06+00:00',
}

const SERVICES = {
  services: [
    {
      id: 'svc-users',
      name: 'users-api',
      description: null,
      downstreamTargets: [],
      createdAt: '',
      updatedAt: '',
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 100,
}

const EFFECTIVE = JSON.stringify({
  Routes: [{ UpstreamPath: '/api/users', DownstreamPathAndHost: 'localhost:5001' }],
})

const HISTORY = {
  history: [
    { timestamp: '2026-01-02T03:04:05+00:00', action: 'Created', changedBy: 'admin', details: null },
    {
      timestamp: '2026-01-03T04:05:06+00:00',
      action: 'Enabled',
      changedBy: 'operator',
      details: 'from the console',
    },
  ],
}

const RELATED = {
  routes: [
    { ...ROUTE, id: 'route-2', key: 'users-detail', upstreamPath: '/api/users/{everything}' },
  ],
}

interface StubOptions {
  routeStatus?: number
  history?: unknown
  effective?: string
  /** Return the opposite isEnabled on the next detail fetch. */
  togglesIsEnabled?: boolean
  deleteStatus?: number
}

function stubFetch(options: StubOptions = {}) {
  const calls: string[] = []
  const toggled = new Set<string>()

  const impl = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://api.test')
    const method = init?.method ?? 'GET'
    const path = url.pathname
    calls.push(`${method} ${path}`)

    const routeMatch = path.match(/^\/api\/v1\/routes\/([^/]+)$/)
    const routeId = routeMatch?.[1]

    if (path === '/api/v1/services') {
      return new Response(JSON.stringify(SERVICES), { status: 200 })
    }
    if (path === '/api/v1/services/svc-users/routes') {
      return new Response(JSON.stringify(RELATED), { status: 200 })
    }
    if (path === '/api/v1/routes/route-1/effective') {
      return new Response(JSON.stringify({ ocelotJson: options.effective ?? EFFECTIVE }), {
        status: 200,
      })
    }
    if (path === '/api/v1/routes/route-1/history') {
      return new Response(JSON.stringify(options.history ?? HISTORY), { status: 200 })
    }

    if (routeId && method === 'DELETE') {
      if (options.deleteStatus) {
        return new Response(JSON.stringify({ error: 'nope' }), { status: options.deleteStatus })
      }
      // 204 has to be constructed with a null body.
      return new Response(null, { status: 204 })
    }

    const action = path.match(/^\/api\/v1\/routes\/([^/]+)\/(enable|disable)$/)
    if (action && method === 'PATCH') {
      toggled.add(action[1])
      return new Response(
        JSON.stringify({ ...ROUTE, isEnabled: action[2] === 'enable' }),
        { status: 200 },
      )
    }

    if (routeId && (method === 'PATCH' || method === 'PUT')) {
      toggled.add(routeId)
      return new Response(JSON.stringify({ ...ROUTE, isEnabled: !ROUTE.isEnabled }), {
        status: 200,
      })
    }

    if (routeId) {
      if (options.routeStatus === 404) {
        return new Response(JSON.stringify({ error: 'Route not found' }), { status: 404 })
      }
      // Echo the requested id, so navigating to a related route shows that
      // route rather than the fixture's own.
      const body = routeId === 'route-1' ? ROUTE : { ...ROUTE, id: routeId, key: 'users-detail' }
      const disabled = options.togglesIsEnabled && toggled.has(routeId)
      return new Response(JSON.stringify(disabled ? { ...body, isEnabled: false } : body), {
        status: 200,
      })
    }

    return new Response('{}', { status: 200 })
  })

  return { impl, calls }
}

function renderDetails(
  fetchImpl: ReturnType<typeof vi.fn>,
  options: { path?: string } = {},
) {
  const client = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: fetchImpl as unknown as typeof fetch,
  })

  const router = createMemoryRouter(
    [
      {
        path: '/',
        children: [
          { path: 'routes/:id', element: <RouteDetailsPage /> },
          { path: 'routes', element: <p>routes list</p> },
        ],
      },
    ],
    { initialEntries: [options.path ?? '/routes/route-1'] },
  )

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })}
    >
      <ApiClientContext.Provider value={client}>{children}</ApiClientContext.Provider>
    </QueryClientProvider>
  )

  render(<RouterProvider router={router} />, { wrapper })
}

describe('RouteDetailsPage', () => {
  it('renders the full configuration, resolving the service name', async () => {
    const { impl } = stubFetch()
    renderDetails(impl)

    expect(await screen.findByRole('heading', { name: 'users-list' })).toBeInTheDocument()
    expect(screen.getByText('/api/users')).toBeInTheDocument()
    expect(screen.getByText('api.example.com')).toBeInTheDocument()
    // serviceId resolved to a name rather than shown raw; it appears in the
    // summary line and again in the Service row.
    expect(screen.getAllByText('users-api').length).toBeGreaterThan(0)
    expect(screen.queryByText('svc-users')).not.toBeInTheDocument()
    expect(screen.getByText('http://localhost:5001/')).toBeInTheDocument()
    expect(screen.getByText('users.read')).toBeInTheDocument()
    expect(screen.getByText('users.write')).toBeInTheDocument()
    expect(screen.getByText('100 / Minute')).toBeInTheDocument()
    expect(screen.getByText('90s')).toBeInTheDocument()
    expect(screen.getByText('300s')).toBeInTheDocument()
    expect(screen.getByText('RoundRobin')).toBeInTheDocument()
  })

  it('marks unconfigured options explicitly instead of leaving blanks', async () => {
    const { impl } = stubFetch()
    const bare = {
      ...ROUTE,
      host: null,
      downstreamTargets: [],
      authenticationOptions: null,
      rateLimitOptions: null,
      qoSOptions: null,
      cacheOptions: null,
      loadBalancerOptions: null,
    }
    const impl2 = vi.fn(async (input: RequestInfo | URL) => {
      const url = new URL(String(input), 'http://api.test')
      if (url.pathname === '/api/v1/services') {
        return new Response(JSON.stringify(SERVICES), { status: 200 })
      }
      return new Response(JSON.stringify(bare), { status: 200 })
    })
    void impl
    renderDetails(impl2)

    await screen.findByRole('heading', { name: 'users-list' })
    expect(screen.getAllByText('Not configured').length).toBeGreaterThanOrEqual(6)
  })

  it('shows a not-found state rather than an error for a missing route', async () => {
    const { impl } = stubFetch({ routeStatus: 404 })
    renderDetails(impl)

    expect(await screen.findByRole('heading', { name: 'Route not found' })).toBeInTheDocument()
    expect(screen.getByText('No such route')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to routes' })).toBeInTheDocument()
  })

  it('loads the effective config only when its tab is opened', async () => {
    const { impl, calls } = stubFetch()
    renderDetails(impl)
    const user = userEvent.setup()

    await screen.findByRole('heading', { name: 'users-list' })
    expect(calls).not.toContain('GET /api/v1/routes/route-1/effective')

    await user.click(screen.getByRole('tab', { name: 'Effective config' }))

    // The payload is wrapped in an object, so it must be read from ocelotJson.
    await waitFor(() =>
      expect(calls).toContain('GET /api/v1/routes/route-1/effective'),
    )
    // Highlighting splits the JSON into tokens, so assert on the joined text.
    const block = await screen.findByText((_, element) =>
      element?.tagName === 'CODE' ? element.textContent?.includes('DownstreamPathAndHost') : false,
    )
    expect(block.textContent).toContain('/api/users')
    expect(block.textContent).toContain('localhost:5001')
  })

  it('copies the effective config to the clipboard', async () => {
    const { impl } = stubFetch()
    renderDetails(impl)
    const user = userEvent.setup()
    // userEvent.setup() replaces navigator.clipboard with its own stub, so the
    // spy has to be taken after setup rather than before.
    const writeText = vi.spyOn(navigator.clipboard, 'writeText')

    await screen.findByRole('heading', { name: 'users-list' })
    await user.click(screen.getByRole('tab', { name: 'Effective config' }))

    const copy = await screen.findByRole('button', { name: /copy/i })
    await user.click(copy)

    await waitFor(() => expect(writeText).toHaveBeenCalledWith(EFFECTIVE))
    expect(await screen.findByRole('button', { name: /copied/i })).toBeInTheDocument()
  })

  it('populates the history tab', async () => {
    const { impl } = stubFetch()
    renderDetails(impl)
    const user = userEvent.setup()

    await screen.findByRole('heading', { name: 'users-list' })
    await user.click(screen.getByRole('tab', { name: 'History' }))

    const createdRow = (await screen.findByText('Created')).closest('tr') as HTMLElement
    expect(within(createdRow).getByText('admin')).toBeInTheDocument()

    // 'Enabled' is both the status badge and a history action, so take the
    // candidate that sits inside a table row.
    const enabledRow = screen
      .getAllByText('Enabled')
      .map((node) => node.closest('tr'))
      .filter((row) => row !== null)[0] as HTMLElement
    expect(within(enabledRow).getByText('operator')).toBeInTheDocument()
    expect(within(enabledRow).getByText('from the console')).toBeInTheDocument()
  })

  it('says so when there is no history', async () => {
    const { impl } = stubFetch({ history: { history: [] } })
    renderDetails(impl)
    const user = userEvent.setup()

    await screen.findByRole('heading', { name: 'users-list' })
    await user.click(screen.getByRole('tab', { name: 'History' }))

    expect(await screen.findByText('No history yet')).toBeInTheDocument()
  })

  it('lists related routes and navigates to one', async () => {
    const { impl } = stubFetch()
    renderDetails(impl)
    const user = userEvent.setup()

    await screen.findByRole('heading', { name: 'users-list' })
    await user.click(screen.getByRole('tab', { name: 'Related routes' }))

    const cell = await screen.findByText('users-detail')
    await user.click(cell)

    // Navigating reuses the same route element, so the header changes.
    expect(await screen.findByRole('heading', { name: 'users-detail' })).toBeInTheDocument()
  })

  it('disables the route and reflects the new state', async () => {
    const { impl, calls } = stubFetch({ togglesIsEnabled: true })
    renderDetails(impl)
    const user = userEvent.setup()

    await screen.findByRole('heading', { name: 'users-list' })
    expect(screen.getByText('Enabled')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Disable' }))

    await waitFor(() => expect(calls).toContain('PATCH /api/v1/routes/route-1/disable'))
    // The button flips, which only happens after the refetched state arrives.
    expect(await screen.findByRole('button', { name: 'Enable' })).toBeInTheDocument()
    expect(screen.getByText('Disabled')).toBeInTheDocument()
  })

  it('enables a disabled route', async () => {
    const { impl, calls } = stubFetch({ togglesIsEnabled: true })
    renderDetails(impl)
    const user = userEvent.setup()

    await screen.findByRole('heading', { name: 'users-list' })
    await user.click(screen.getByRole('button', { name: 'Disable' }))
    await screen.findByRole('button', { name: 'Enable' })

    await user.click(screen.getByRole('button', { name: 'Enable' }))

    await waitFor(() => expect(calls).toContain('PATCH /api/v1/routes/route-1/enable'))
  })

  it('confirms before deleting, then returns to the list', async () => {
    const { impl, calls } = stubFetch()
    renderDetails(impl)
    const user = userEvent.setup()

    await screen.findByRole('heading', { name: 'users-list' })
    await user.click(screen.getByRole('button', { name: /delete/i }))

    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText('Delete this route?')).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Delete route' }))

    await waitFor(() => expect(calls).toContain('DELETE /api/v1/routes/route-1'))
    expect(await screen.findByText('routes list')).toBeInTheDocument()
  })
})
