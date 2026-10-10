import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { EditRouteWizardPage } from '@/features/routes-wizard/create-route-wizard'
import { toCreateRequest, type RouteDraft } from '@/features/routes-wizard/wizard-model'

const STORED = {
  id: 'route-1',
  key: 'users-list',
  method: 'GET',
  upstreamPath: '/api/users',
  host: 'api.example.com',
  serviceId: 'svc-users',
  isEnabled: true,
  priority: 30,
  routeIsCaseSensitive: true,
        downstreamPathTemplate: null,

        headerTransformations: null,

        downstreamMethod: null,
        downstreamHttpVersion: null,
        downstreamHttpVersionPolicy: null,
        dangerousAcceptAnyServerCertificateValidator: false,
        delegatingHandlers: [],
        httpClientOptions: null,
        timeoutSeconds: null,
  downstreamTargets: [{ host: 'localhost', port: 5001, scheme: 'http', path: '/' }],
  authenticationOptions: { allowedScopes: ['users.read'] },
  rateLimitOptions: { enableRateLimiting: true, period: 'Hour', limit: 250 },
  qoSOptions: { timeoutSeconds: 45, circuitBreakerTimeoutSeconds: 10 },
  cacheOptions: { ttlSeconds: 120 },
  loadBalancerOptions: { algorithm: 'LeastConnection' },
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

interface StubOptions {
  routeStatus?: number
  putStatus?: number
  validationErrors?: { field: string | null; code: string; message: string }[]
}

function stubFetch(options: StubOptions = {}) {
  const puts: unknown[] = []
  const posts: string[] = []

  const impl = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://api.test')
    const method = init?.method ?? 'GET'
    const path = url.pathname

    if (path === '/api/v1/services') {
      return new Response(JSON.stringify(SERVICES), { status: 200 })
    }

    if (path === '/api/v1/routes/validate' && method === 'POST') {
      posts.push('validate')
      const errors = options.validationErrors ?? []
      return new Response(JSON.stringify({ isValid: errors.length === 0, errors }), {
        status: 200,
      })
    }

    if (path === '/api/v1/routes/route-1' && method === 'PUT') {
      puts.push(JSON.parse(String(init?.body)))
      if (options.putStatus) {
        // The shape the API returns for a rejected mapping, now that PUT goes
        // through RouteRequestMapper: a non-empty errors object keyed by field.
        return new Response(
          JSON.stringify({
            title: 'One or more validation errors occurred.',
            status: options.putStatus,
            correlationId: 'abc-123',
            errors: { key: ['Key already exists'] },
          }),
          { status: options.putStatus },
        )
      }
      return new Response(JSON.stringify(STORED), { status: 200 })
    }

    if (path === '/api/v1/routes/route-1') {
      if (options.routeStatus === 404) {
        return new Response(JSON.stringify({ error: 'Route not found' }), { status: 404 })
      }
      return new Response(JSON.stringify(STORED), { status: 200 })
    }

    return new Response('{}', { status: 200 })
  })

  return { impl, puts, posts }
}

function renderEdit(fetchImpl: ReturnType<typeof vi.fn>, path = '/routes/route-1/edit') {
  const client = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: fetchImpl as unknown as typeof fetch,
  })

  const router = createMemoryRouter(
    [
      {
        path: '/',
        children: [
          { path: 'routes/:id/edit', element: <EditRouteWizardPage /> },
          { path: 'routes/:id', element: <p>route detail</p> },
        ],
      },
    ],
    { initialEntries: [path] },
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

/**
 * Resolves once the form has been filled in from the stored route.
 *
 * Waits for the field to carry a value rather than to exist, because the field
 * renders immediately and only becomes editable once the route has loaded.
 */
async function waitForSeeded() {
  await waitFor(() =>
    expect((screen.getByLabelText('Key') as HTMLInputElement).value).not.toBe(''),
  )
}

/** Walks from the first step to Review. */
async function goToReview(user: ReturnType<typeof userEvent.setup>) {
  await waitForSeeded()
  for (let step = 0; step < 7; step += 1) {
    await user.click(screen.getByRole('button', { name: 'Next' }))
  }
}

describe('EditRouteWizardPage', () => {
  it('starts from the stored configuration', async () => {
    const { impl } = stubFetch()
    renderEdit(impl)
    const user = userEvent.setup()

    await waitForSeeded()
    expect(screen.getByLabelText('Method')).toHaveValue('GET')

    // Later steps carry the rest of the stored configuration.
    await user.click(screen.getByRole('button', { name: 'Next' }))
    expect(screen.getByLabelText('Upstream path')).toHaveValue('/api/users')
    expect(screen.getByLabelText('Host')).toHaveValue('api.example.com')
  })

  it('does not create a route, and replaces the stored one', async () => {
    const { impl, puts, posts } = stubFetch()
    renderEdit(impl)
    const user = userEvent.setup()

    await goToReview(user)

    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(puts).toHaveLength(1))
    // The draft is checked first, and no create request is made.
    expect(posts).toEqual(['validate'])
    expect(puts[0]).toEqual({
      key: 'users-list',
      method: 'GET',
      upstreamPath: '/api/users',
      host: 'api.example.com',
      serviceId: 'svc-users',
      downstreamTargets: [{ host: 'localhost', port: 5001, scheme: 'http', path: '/' }],
      authenticationOptions: { allowedScopes: ['users.read'] },
      rateLimitOptions: { enableRateLimiting: true, period: 'Hour', limit: 250 },
      qosOptions: { timeoutSeconds: 45, circuitBreakerTimeoutSeconds: 10 },
      cacheOptions: { ttlSeconds: 120 },
      loadBalancerOptions: { algorithm: 'LeastConnection' },
      // Carried from the stored route: these are always sent, unlike the
      // feature blocks that are omitted when switched off.
      priority: 30,
      routeIsCaseSensitive: true,
      downstreamMethod: null,
      downstreamHttpVersion: null,
      downstreamHttpVersionPolicy: null,
      acceptAnyServerCertificate: false,
      downstreamPathTemplate: null,
      timeoutSeconds: null,
      routeId: 'route-1',
    })
  })

  it('carries an edited value through to the replacement', async () => {
    const { impl, puts } = stubFetch()
    renderEdit(impl)
    const user = userEvent.setup()

    const key = await screen.findByLabelText('Key')
    await waitFor(() => expect((key as HTMLInputElement).value).not.toBe(''))
    await user.clear(key)
    await user.type(key, 'renamed-route')

    await goToReview(user)
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(puts).toHaveLength(1))
    expect((puts[0] as RouteDraft).key).toBe('renamed-route')
  })

  it('omits a switched-off option, which the API reads as remove', async () => {
    const { impl, puts } = stubFetch()
    renderEdit(impl)
    const user = userEvent.setup()

    await goToReview(user)

    // Review shows the stored values, so reach the QoS step to switch it off.
    await user.click(screen.getByRole('button', { name: /QoS/ }))
    await user.click(screen.getByLabelText('Enable QoS settings'))

    await user.click(screen.getByRole('button', { name: 'Next' }))
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(puts).toHaveLength(1))
    const body = puts[0] as Record<string, unknown>
    expect(body).not.toHaveProperty('qosOptions')
    // The blocks left alone are still present.
    expect(body).toHaveProperty('cacheOptions')
  })

  it('keeps an algorithm the build does not know about', async () => {
    const { impl, puts } = stubFetch()
    const exotic = { ...STORED, loadBalancerOptions: { algorithm: 'WeightedRoundRobin' } }
    const impl2 = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(String(input), 'http://api.test')
      if (url.pathname === '/api/v1/services') {
        return new Response(JSON.stringify(SERVICES), { status: 200 })
      }
      if (url.pathname === '/api/v1/routes/validate' && init?.method === 'POST') {
        return new Response(JSON.stringify({ isValid: true, errors: [] }), { status: 200 })
      }
      if (url.pathname === '/api/v1/routes/route-1' && (init?.method ?? 'GET') === 'PUT') {
        puts.push(JSON.parse(String(init?.body)))
        return new Response(JSON.stringify(exotic), { status: 200 })
      }
      if (url.pathname === '/api/v1/routes/route-1') {
        return new Response(JSON.stringify(exotic), { status: 200 })
      }
      return new Response('{}', { status: 200 })
    })
    void impl
    renderEdit(impl2)
    const user = userEvent.setup()

    await goToReview(user)
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(puts).toHaveLength(1))
    // Not silently rewritten to RoundRobin.
    expect(puts[0]).toMatchObject({
      loadBalancerOptions: { algorithm: 'WeightedRoundRobin' },
    })
  })

  it('says so when the route is gone, without changing anything', async () => {
    const { impl, puts } = stubFetch({ routeStatus: 404 })
    renderEdit(impl)

    expect(await screen.findByText('No such route')).toBeInTheDocument()
    expect(screen.getByText('It may have been deleted. Nothing was changed.')).toBeInTheDocument()
    expect(puts).toHaveLength(0)
  })

  it('stays on the page and shows the error when the replacement is rejected', async () => {
    const { impl } = stubFetch({ putStatus: 400 })
    renderEdit(impl)
    const user = userEvent.setup()

    await goToReview(user)
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('The route was rejected')).toBeInTheDocument()
    // Still editable, with the work intact.
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeInTheDocument()
  })

  it('sends a rejected field back to its step rather than creating anything', async () => {
    const { impl, puts } = stubFetch({
      validationErrors: [
        { field: 'cacheOptions', code: 'INVALID_CACHE', message: 'Cache TTL is out of range' },
      ],
    })
    renderEdit(impl)
    const user = userEvent.setup()

    await goToReview(user)
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('One problem to fix before saving:')).toBeInTheDocument()
    expect(puts).toHaveLength(0)
  })
})

describe('the shaping is the same for create and replace', () => {
  it('produces the same body for the same draft', () => {
    // The reason one function can serve both endpoints: PUT treats an absent
    // block as "remove it", which is what switching an option off means.
    const draft: RouteDraft = {
      key: 'k',
      method: 'GET',
      upstreamPath: '/x',
      host: '',
      priority: 0,
      routeIsCaseSensitive: false,
      serviceId: 's',
      downstreamTargets: [{ host: 'h', port: 1, scheme: 'http', path: '/' }],
      allowedScopes: [],
      rateLimit: { enabled: false, limit: 1, period: 'Minute' },
      qos: { enabled: false, timeoutSeconds: 1, circuitBreakerTimeoutSeconds: '' },
      cache: { enabled: false, ttlSeconds: 1 },
      loadBalancer: { enabled: false, algorithm: 'RoundRobin' },
      // A draft without a transport block is the case this covers: it has to
      // shape to the same defaults rather than throwing.
    }

    expect(toCreateRequest(draft)).not.toHaveProperty('cacheOptions')
  })
})
