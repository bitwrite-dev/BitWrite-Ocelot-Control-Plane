import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import type { ReactNode } from 'react'
import { describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { ServicesPage } from '@/features/services/services-page'

const SERVICE = {
  id: 'svc-users',
  name: 'users-api',
  description: 'The users backend',
  downstreamTargets: [{ host: 'localhost', port: 5001, scheme: 'http', path: '/' }],
  createdAt: '2026-01-02T03:04:05+00:00',
  updatedAt: '2026-01-03T04:05:06+00:00',
}

const SECOND = {
  ...SERVICE,
  id: 'svc-orders',
  name: 'orders-api',
  description: null,
  downstreamTargets: [
    { host: 'a.internal', port: 8080, scheme: 'http', path: '/' },
    { host: 'b.internal', port: 8080, scheme: 'http', path: '/' },
  ],
}

function list(services: unknown[], totalCount = services.length, page = 1) {
  return {
    services,
    totalCount,
    page,
    pageSize: 20,
  }
}

interface StubOptions {
  services?: unknown[]
  /** Reported total, which is what decides how many pages there are. */
  totalCount?: number
  createStatus?: number
  createBody?: unknown
  putStatus?: number
  deleteStatus?: number
  dependentRoutes?: unknown[]
}

function stubFetch(options: StubOptions = {}) {
  const posts: Record<string, unknown>[] = []
  const puts: Record<string, unknown>[] = []
  const deletes: string[] = []

  const impl = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://api.test')
    const path = url.pathname
    const method = init?.method ?? 'GET'

    if (path === '/api/v1/services' && method === 'GET') {
      const page = Number(url.searchParams.get('page') ?? 1)
      return new Response(
        JSON.stringify(
          list(
            options.services ?? [SERVICE, SECOND],
            options.totalCount ?? (options.services ?? [SERVICE, SECOND]).length,
            page,
          ),
        ),
        { status: 200 },
      )
    }

    if (path === '/api/v1/services' && method === 'POST') {
      posts.push(JSON.parse(String(init?.body)))
      if (options.createStatus) {
        return new Response(JSON.stringify(options.createBody), { status: options.createStatus })
      }
      return new Response(JSON.stringify({ ...SERVICE, ...posts[0], id: 'svc-new' }), {
        status: 201,
      })
    }

    if (path === '/api/v1/services/svc-users' && method === 'PUT') {
      puts.push(JSON.parse(String(init?.body)))
      if (options.putStatus) {
        return new Response(JSON.stringify({ title: 'Rejected' }), { status: options.putStatus })
      }
      return new Response(JSON.stringify(SERVICE), { status: 200 })
    }

    if (path === '/api/v1/services/svc-users' && method === 'DELETE') {
      deletes.push(path)
      if (options.deleteStatus) {
        return new Response(JSON.stringify({ error: 'nope' }), { status: options.deleteStatus })
      }
      return new Response(null, { status: 204 })
    }

    if (path === '/api/v1/services/svc-users/routes') {
      return new Response(JSON.stringify({ routes: options.dependentRoutes ?? [] }), {
        status: 200,
      })
    }

    return new Response('{}', { status: 200 })
  })

  return { impl, posts, puts, deletes }
}

function renderServices(fetchImpl: ReturnType<typeof vi.fn>, entry = '/services') {
  const client = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: fetchImpl as unknown as typeof fetch,
  })

  const router = createMemoryRouter(
    [
      {
        path: '/',
        children: [
          { path: 'services', element: <ServicesPage /> },
          { path: 'services/:id', element: <p>service detail</p> },
        ],
      },
    ],
    { initialEntries: [entry] },
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

describe('ServicesPage', () => {
  it('lists services with their endpoints', async () => {
    const { impl } = stubFetch()
    renderServices(impl)

    const row = (await screen.findByText('users-api')).closest('tr') as HTMLElement
    expect(within(row).getByText('localhost:5001')).toBeInTheDocument()
    expect(within(row).getByText('The users backend')).toBeInTheDocument()
  })

  it('lists every endpoint of a service', async () => {
    const { impl } = stubFetch()
    renderServices(impl)

    const row = (await screen.findByText('orders-api')).closest('tr') as HTMLElement
    expect(within(row).getByText('a.internal:8080, b.internal:8080')).toBeInTheDocument()
    // No description reads as an em dash rather than a blank.
    expect(within(row).getByText('—')).toBeInTheDocument()
  })

  it('sends a create with the endpoints filled in', async () => {
    const { impl, posts } = stubFetch()
    renderServices(impl)
    const user = userEvent.setup()

    await user.click((await screen.findAllByRole('button', { name: /new service/i }))[0])

    await user.type(screen.getByLabelText('Name'), 'images-api')
    await user.type(screen.getByLabelText('Endpoint 1 host'), 'img.internal')
    await user.type(screen.getByLabelText('Endpoint 1 port'), '9000')
    await user.click(screen.getByRole('button', { name: 'Create service' }))

    await waitFor(() => expect(posts).toHaveLength(1))
    expect(posts[0]).toEqual({
      name: 'images-api',
      description: null,
      downstreamTargets: [{ host: 'img.internal', port: 9000, scheme: 'http', path: '/' }],
    })
  })

  it('will not submit while an endpoint is incomplete', async () => {
    const { impl, posts } = stubFetch()
    renderServices(impl)
    const user = userEvent.setup()

    await user.click((await screen.findAllByRole('button', { name: /new service/i }))[0])
    await user.type(screen.getByLabelText('Name'), 'images-api')
    // Host typed, port left blank.
    await user.type(screen.getByLabelText('Endpoint 1 host'), 'img.internal')
    await user.click(screen.getByRole('button', { name: 'Create service' }))

    expect(await screen.findByText('Port is required')).toBeInTheDocument()
    expect(posts).toHaveLength(0)
  })

  it('supports several endpoints on one service', async () => {
    const { impl, posts } = stubFetch()
    renderServices(impl)
    const user = userEvent.setup()

    await user.click((await screen.findAllByRole('button', { name: /new service/i }))[0])
    await user.type(screen.getByLabelText('Name'), 'images-api')
    await user.type(screen.getByLabelText('Endpoint 1 host'), 'a.internal')
    await user.type(screen.getByLabelText('Endpoint 1 port'), '9000')

    await user.click(screen.getByRole('button', { name: 'Add endpoint' }))
    await user.type(screen.getByLabelText('Endpoint 2 host'), 'b.internal')
    await user.type(screen.getByLabelText('Endpoint 2 port'), '9001')

    await user.click(screen.getByRole('button', { name: 'Create service' }))

    await waitFor(() => expect(posts).toHaveLength(1))
    expect(posts[0].downstreamTargets).toHaveLength(2)
  })

  it('refuses to remove the only endpoint, since a service needs one', async () => {
    const { impl } = stubFetch()
    renderServices(impl)
    const user = userEvent.setup()

    await user.click((await screen.findAllByRole('button', { name: /new service/i }))[0])
    expect(screen.getByRole('button', { name: 'Remove' })).toBeDisabled()
  })

  it('loads the stored values when editing', async () => {
    const { impl } = stubFetch()
    renderServices(impl)
    const user = userEvent.setup()

    const row = (await screen.findByText('users-api')).closest('tr') as HTMLElement
    await user.click(within(row).getByRole('button', { name: 'Edit' }))

    expect(await screen.findByRole('heading', { name: 'Edit service' })).toBeInTheDocument()
    expect(screen.getByLabelText('Name')).toHaveValue('users-api')
    expect(screen.getByLabelText('Endpoint 1 host')).toHaveValue('localhost')
    expect(screen.getByLabelText('Endpoint 1 port')).toHaveValue(5001)
  })

  it('sends the edit as a replacement', async () => {
    const { impl, puts } = stubFetch()
    renderServices(impl)
    const user = userEvent.setup()

    const row = (await screen.findByText('users-api')).closest('tr') as HTMLElement
    await user.click(within(row).getByRole('button', { name: 'Edit' }))

    const name = await screen.findByLabelText('Name')
    await user.clear(name)
    await user.type(name, 'renamed')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(puts).toHaveLength(1))
    expect(puts[0].name).toBe('renamed')
    expect(await screen.findByText('service detail')).toBeInTheDocument()
  })

  it('warns about routes that would be left pointing at nothing', async () => {
    const { impl } = stubFetch({
      dependentRoutes: [
        { ...SERVICE, id: 'r1', key: 'users-list', method: 'GET', upstreamPath: '/u' },
        { ...SERVICE, id: 'r2', key: 'users-detail', method: 'GET', upstreamPath: '/d' },
      ],
    })
    renderServices(impl)
    const user = userEvent.setup()

    const row = (await screen.findByText('users-api')).closest('tr') as HTMLElement
    await user.click(within(row).getByRole('button', { name: /delete/i }))

    const dialog = await screen.findByRole('alertdialog')
    expect(
      await within(dialog).findByText(/2 routes reference this service/),
    ).toBeInTheDocument()
    expect(dialog.textContent).toContain('users-list')
  })

  it('does not warn when nothing references the service', async () => {
    const { impl } = stubFetch({ dependentRoutes: [] })
    renderServices(impl)
    const user = userEvent.setup()

    const row = (await screen.findByText('users-api')).closest('tr') as HTMLElement
    await user.click(within(row).getByRole('button', { name: /delete/i }))

    const dialog = await screen.findByRole('alertdialog')
    await waitFor(() =>
      expect(dialog.textContent).not.toContain('reference this service'),
    )
  })

  it('deletes after confirmation', async () => {
    const { impl, deletes } = stubFetch()
    renderServices(impl)
    const user = userEvent.setup()

    const row = (await screen.findByText('users-api')).closest('tr') as HTMLElement
    await user.click(within(row).getByRole('button', { name: /delete/i }))

    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Delete service' }))

    await waitFor(() => expect(deletes).toHaveLength(1))
  })

  it('surfaces a server validation error per field', async () => {
    const { impl } = stubFetch({
      createStatus: 400,
      createBody: {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { Name: ['A service with that name already exists'] },
      },
    })
    renderServices(impl)
    const user = userEvent.setup()

    await user.click((await screen.findAllByRole('button', { name: /new service/i }))[0])
    await user.type(screen.getByLabelText('Name'), 'users-api')
    await user.type(screen.getByLabelText('Endpoint 1 host'), 'localhost')
    await user.type(screen.getByLabelText('Endpoint 1 port'), '5001')
    await user.click(screen.getByRole('button', { name: 'Create service' }))

    expect(await screen.findByText('The service was rejected')).toBeInTheDocument()
    expect(screen.getByText('A service with that name already exists')).toBeInTheDocument()
  })

  it('shows an empty state when there are no services', async () => {
    const { impl } = stubFetch({ services: [] })
    renderServices(impl)

    expect(await screen.findByText('No services yet')).toBeInTheDocument()
  })

  it('pages server-side', async () => {
    // 45 records at 20 per page is three pages, so Next is available.
    const { impl } = stubFetch({ totalCount: 45 })
    renderServices(impl)
    const user = userEvent.setup()

    await screen.findByText('users-api')
    await user.click(screen.getByRole('button', { name: 'Next page' }))

    await waitFor(() => {
      const calls = impl.mock.calls.map((call) => String(call[0]))
      expect(calls.some((url) => url.includes('page=2'))).toBe(true)
    })
  })
})
