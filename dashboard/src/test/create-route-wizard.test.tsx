import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { CreateRouteWizardPage } from '@/features/routes-wizard/create-route-wizard'

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

const CREATED = {
  id: 'route-new',
  key: 'users-list',
  method: 'GET',
  upstreamPath: '/api/users',
  host: null,
  serviceId: 'svc-users',
  isEnabled: true,
  downstreamTargets: [{ host: 'localhost', port: 5001, scheme: 'http', path: '/' }],
  authenticationOptions: null,
  rateLimitOptions: null,
  qoSOptions: null,
  cacheOptions: null,
  loadBalancerOptions: null,
  createdAt: '',
  updatedAt: '',
}

interface StubOptions {
  createStatus?: number
  createBody?: unknown
  /** Fails the services lookup, which blocks the wizard entirely. */
  servicesError?: boolean
}

function stubFetch(options: StubOptions = {}) {
  const posts: unknown[] = []

  const impl = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://api.test')

    if (url.pathname === '/api/v1/services') {
      if (options.servicesError) {
        return new Response(JSON.stringify({ error: 'Service store unavailable' }), {
          status: 503,
        })
      }
      return new Response(JSON.stringify(SERVICES), { status: 200 })
    }

    if (url.pathname === '/api/v1/routes' && init?.method === 'POST') {
      posts.push(JSON.parse(String(init.body)))
      if (options.createStatus) {
        return new Response(JSON.stringify(options.createBody), {
          status: options.createStatus,
        })
      }
      return new Response(JSON.stringify(CREATED), { status: 201 })
    }

    return new Response('{}', { status: 200 })
  })

  return { impl, posts }
}

function renderWizard(fetchImpl: ReturnType<typeof vi.fn>) {
  const client = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: fetchImpl as unknown as typeof fetch,
  })

  const router = createMemoryRouter(
    [
      {
        path: '/',
        children: [
          { path: 'routes/new', element: <CreateRouteWizardPage /> },
          { path: 'routes/:id', element: <p>route detail</p> },
        ],
      },
    ],
    { initialEntries: ['/routes/new'] },
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

const next = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.click(screen.getByRole('button', { name: 'Next' }))
}

/** Fills the three required steps and returns at the review step. */
async function fillRequiredSteps(user: ReturnType<typeof userEvent.setup>) {
  await user.type(await screen.findByLabelText('Key'), 'users-list')
  await next(user)

  await user.type(screen.getByLabelText('Upstream path'), '/api/users')
  await next(user)

  await user.selectOptions(screen.getByLabelText('Service'), 'svc-users')
  await user.type(screen.getByLabelText('Host 1'), 'localhost')
  await user.type(screen.getByLabelText('Port'), '5001')
  await next(user)
}

describe('CreateRouteWizardPage', () => {
  it('will not advance while the current step is invalid', async () => {
    const { impl } = stubFetch()
    renderWizard(impl)
    const user = userEvent.setup()

    await screen.findByLabelText('Key')
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()

    await user.type(screen.getByLabelText('Key'), 'users-list')
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled()
  })

  it('shows the step-specific error from validation', async () => {
    const { impl } = stubFetch()
    renderWizard(impl)
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Key'), 'a')
    await next(user)
    await user.type(screen.getByLabelText('Upstream path'), 'no-leading-slash')

    expect(screen.getByText('Upstream path must start with /')).toBeInTheDocument()
  })

  it('keeps entered state when moving back and forward', async () => {
    const { impl } = stubFetch()
    renderWizard(impl)
    const user = userEvent.setup()

    await fillRequiredSteps(user)
    // Now on step 4 (Authentication).
    expect(await screen.findByLabelText('Allowed scopes')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Back' }))
    expect(screen.getByLabelText('Host 1')).toHaveValue('localhost')
    expect(screen.getByLabelText('Service')).toHaveValue('svc-users')
  })

  it('posts the assembled configuration and moves to the route on success', async () => {
    const { impl, posts } = stubFetch()
    renderWizard(impl)
    const user = userEvent.setup()

    await fillRequiredSteps(user)

    for (let step = 0; step < 4; step += 1) await next(user)

    expect(
      await screen.findByRole('heading', { name: 'Review & Save' }),
    ).toBeInTheDocument()
    expect(screen.getByText('/api/users')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Create route' }))

    await waitFor(() => expect(posts).toHaveLength(1))
    expect(posts[0]).toEqual({
      key: 'users-list',
      method: 'GET',
      upstreamPath: '/api/users',
      host: null,
      serviceId: 'svc-users',
      downstreamTargets: [{ host: 'localhost', port: 5001, scheme: 'http', path: '/' }],
    })
    expect(await screen.findByText('route detail')).toBeInTheDocument()
  })

  it('surfaces a server rejection without losing the draft', async () => {
    const { impl } = stubFetch({
      createStatus: 400,
      createBody: {
        title: 'One or more validation errors occurred.',
        status: 400,
        correlationId: 'abc-123',
        errors: { key: ['Key already exists'] },
      },
    })
    renderWizard(impl)
    const user = userEvent.setup()

    await fillRequiredSteps(user)
    for (let step = 0; step < 4; step += 1) await next(user)

    await user.click(await screen.findByRole('button', { name: 'Create route' }))

    expect(await screen.findByText('The route was rejected')).toBeInTheDocument()
    expect(screen.getByText('key: Key already exists')).toBeInTheDocument()
    // Still on the review step, with the entered values intact.
    expect(screen.getByRole('button', { name: 'Create route' })).toBeInTheDocument()
  })

  it('blocks with a retry when the service list cannot be loaded', async () => {
    const { impl } = stubFetch({ servicesError: true })
    renderWizard(impl)

    expect(await screen.findByText('Service store unavailable')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
    // The step body is withheld rather than shown as a form that cannot proceed.
    expect(screen.queryByLabelText('Key')).not.toBeInTheDocument()
  })

  it('names the two steps the API cannot store yet', async () => {
    const { impl } = stubFetch()
    renderWizard(impl)

    expect(await screen.findByText('Authorization')).toBeInTheDocument()
    expect(screen.getByText('Transformations')).toBeInTheDocument()
  })
})
