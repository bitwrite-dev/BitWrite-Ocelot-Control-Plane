import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { GlobalConfigurationPage } from '@/features/global-configuration/global-configuration-page'
import type { GlobalConfigurationResponse } from '@/api'

/**
 * The global configuration is one document edited as one form.
 *
 * The parts worth asserting are the ones that would lose an edit or publish a
 * file the gateway cannot use: the save payload, the reset target, and the two
 * different ways a page can be left with unsaved changes.
 */

const CONFIG: GlobalConfigurationResponse = {
  id: '00000000-0000-0000-0000-000000000001',
  baseUrl: 'https://api.example.com',
  requestIdKey: 'X-Request-Id',
  downstreamScheme: 'https',
  timeout: 90_000,
  rateLimit: { enableRateLimiting: true, httpStatusCode: '429' },
  qoS: { timeoutValue: 90_000, durationOfBreak: 30_000 },
  httpHandler: { useProxy: true, expect100Continue: false, maxConnectionsPerServer: 200 },
  serviceDiscovery: {
    provider: 'Consul',
    host: 'consul.internal',
    port: 8500,
    type: 'Http',
    configuration: { PollingInterval: '5000' },
  },
  updatedAt: '2026-01-02T03:04:05Z',
}

type Handler = (url: string, method: string, body: unknown) => Response

function ok(payload: unknown) {
  return new Response(JSON.stringify(payload), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

/**
 * Renders the page at a real route with a sibling to navigate to, because the
 * in-app guard is a router feature and testing it without a router would test
 * nothing.
 */
function renderPage(handler: Handler, siblingPath = '/routes') {
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
      {
        path: '/',
        children: [
          { index: true, element: <GlobalConfigurationPage /> },
          { path: 'routes', element: <p>Routes page</p> },
        ],
      },
    ],
    { initialEntries: ['/'] },
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

  return { calls, router, siblingPath }
}

beforeEach(() => {
  vi.restoreAllMocks()
})

describe('loading', () => {
  it('fills every property of the document', async () => {
    renderPage(() => ok(CONFIG))

    await screen.findByLabelText('Base URL')
    expect(screen.getByLabelText('Base URL')).toHaveValue('https://api.example.com')
    expect(screen.getByLabelText('Downstream scheme')).toHaveValue('https')
    expect(screen.getByLabelText('Request ID header')).toHaveValue('X-Request-Id')
    expect(screen.getByLabelText('Timeout (ms)')).toHaveValue(90_000)
    expect(screen.getByLabelText('Enable rate limiting')).toBeChecked()
    expect(screen.getByLabelText('Use a proxy')).toBeChecked()
    expect(screen.getByLabelText('Provider')).toHaveValue('Consul')
    expect(screen.getByLabelText('Provider settings')).toHaveValue('PollingInterval: 5000')
  })

  it('opens with nothing marked as changed', async () => {
    renderPage(() => ok(CONFIG))

    await screen.findByLabelText('Base URL')
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Reset' })).toBeDisabled()
    expect(screen.queryByText('Unsaved changes')).not.toBeInTheDocument()
  })

  it('offers a retry when the document cannot be loaded', async () => {
    renderPage(() => new Response(JSON.stringify({ error: 'boom' }), { status: 500 }))

    expect(
      await screen.findByText('Could not load the global configuration'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })
})

describe('saving', () => {
  it('sends the whole document, not only the edited field', async () => {
    // A snapshot is immutable once published, so a partial save would produce a
    // file that mixes two edits.
    const { calls } = renderPage((_url, method) => (method === 'GET' ? ok(CONFIG) : ok(CONFIG)))
    const user = userEvent.setup()

    await screen.findByLabelText('Base URL')
    const field = screen.getByLabelText('Base URL')
    fireEvent.change(field, { target: { value: 'https://other.example.com' } })
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => {
      const put = calls.find((call) => call.method === 'PUT')
      expect(put).toBeDefined()
      expect(put!.body).toMatchObject({
        baseUrl: 'https://other.example.com',
        requestIdKey: 'X-Request-Id',
        downstreamScheme: 'https',
        qoS: { timeoutValue: 90_000, durationOfBreak: 30_000 },
        httpHandler: { useProxy: true, expect100Continue: false, maxConnectionsPerServer: 200 },
        serviceDiscovery: { provider: 'Consul', host: 'consul.internal', port: 8500 },
      })
    })
  })

  it('marks the form changed as soon as a field is edited', async () => {
    renderPage(() => ok(CONFIG))

    await screen.findByLabelText('Base URL')
    fireEvent.change(screen.getByLabelText('Base URL'), {
      target: { value: 'https://other.example.com' },
    })

    expect(screen.getByText('Unsaved changes')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled()
  })

  it('does not mark the form changed for surrounding whitespace', async () => {
    renderPage(() => ok(CONFIG))

    await screen.findByLabelText('Base URL')
    fireEvent.change(screen.getByLabelText('Request ID header'), {
      target: { value: '  X-Request-Id  ' },
    })

    // Otherwise an accidental space costs a reload to undo.
    expect(screen.queryByText('Unsaved changes')).not.toBeInTheDocument()
  })

  it('refuses to save a value the API would reject', async () => {
    const { calls } = renderPage((_url, method) => (method === 'GET' ? ok(CONFIG) : ok(CONFIG)))

    await screen.findByLabelText('Base URL')
    // The scheme would then disagree with the base URL, which Ocelot pairs.
    fireEvent.change(screen.getByLabelText('Downstream scheme'), { target: { value: 'http' } })

    expect(screen.getByText(/Does not match the base URL/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    expect(calls.some((call) => call.method === 'PUT')).toBe(false)
  })

  it('confirms a save by returning the form to a clean state', async () => {
    renderPage((_url, method) =>
      method === 'GET' ? ok(CONFIG) : ok({ ...CONFIG, baseUrl: 'https://other.example.com' }),
    )
    const user = userEvent.setup()

    await screen.findByLabelText('Base URL')
    fireEvent.change(screen.getByLabelText('Base URL'), {
      target: { value: 'https://other.example.com' },
    })
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => {
      expect(screen.queryByText('Unsaved changes')).not.toBeInTheDocument()
    })
  })

  it('explains a permission failure in terms of the role needed', async () => {
    renderPage((_url, method) =>
      method === 'GET'
        ? ok(CONFIG)
        : new Response(JSON.stringify({ error: 'Forbidden' }), { status: 403 }),
    )
    const user = userEvent.setup()

    await screen.findByLabelText('Base URL')
    fireEvent.change(screen.getByLabelText('Base URL'), { target: { value: 'https://x.example.com' } })
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Not permitted')).toBeInTheDocument()
    expect(screen.getByText(/Admin role/)).toBeInTheDocument()
  })
})

describe('reset', () => {
  it('returns the form to the values that were loaded', async () => {
    // There is no history endpoint, so the loaded document is the only honest
    // target: anything else would be a guess.
    const user = userEvent.setup()
    renderPage(() => ok(CONFIG))

    await screen.findByLabelText('Base URL')
    fireEvent.change(screen.getByLabelText('Base URL'), { target: { value: 'https://x.example.com' } })
    fireEvent.change(screen.getByLabelText('Request ID header'), { target: { value: 'X-Other' } })

    await user.click(screen.getByRole('button', { name: 'Reset' }))

    expect(screen.getByLabelText('Base URL')).toHaveValue('https://api.example.com')
    expect(screen.getByLabelText('Request ID header')).toHaveValue('X-Request-Id')
    expect(screen.queryByText('Unsaved changes')).not.toBeInTheDocument()
  })

  it('is disabled until something has changed', async () => {
    renderPage(() => ok(CONFIG))

    await screen.findByLabelText('Base URL')
    expect(screen.getByRole('button', { name: 'Reset' })).toBeDisabled()
  })
})

describe('leaving with unsaved changes', () => {
  it('asks before navigating inside the app', async () => {
    // The router half of the guard, which is the one the browser will not do
    // for us.
    const { router } = renderPage(() => ok(CONFIG))

    await screen.findByLabelText('Base URL')
    fireEvent.change(screen.getByLabelText('Base URL'), { target: { value: 'https://x.example.com' } })
    await act(() => router.navigate('/routes'))

    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText('Unsaved changes')).toBeInTheDocument()
    expect(screen.queryByText('Routes page')).not.toBeInTheDocument()
  })

  it('stays put when the operator backs out of the prompt', async () => {
    const user = userEvent.setup()
    const { router } = renderPage(() => ok(CONFIG))

    await screen.findByLabelText('Base URL')
    fireEvent.change(screen.getByLabelText('Base URL'), { target: { value: 'https://x.example.com' } })
    await act(() => router.navigate('/routes'))
    await screen.findByRole('alertdialog')

    await user.click(screen.getByRole('button', { name: 'Stay here' }))

    expect(screen.getByLabelText('Base URL')).toBeInTheDocument()
    expect(screen.queryByText('Routes page')).not.toBeInTheDocument()
  })

  it('leaves without saving when told to', async () => {
    const user = userEvent.setup()
    const { router, calls } = renderPage(() => ok(CONFIG))

    await screen.findByLabelText('Base URL')
    fireEvent.change(screen.getByLabelText('Base URL'), { target: { value: 'https://x.example.com' } })
    await act(() => router.navigate('/routes'))
    await screen.findByRole('alertdialog')

    await user.click(screen.getByRole('button', { name: 'Leave without saving' }))

    expect(await screen.findByText('Routes page')).toBeInTheDocument()
    expect(calls.some((call) => call.method === 'PUT')).toBe(false)
  })

  it('saves before leaving when told to', async () => {
    const user = userEvent.setup()
    const { router, calls } = renderPage((_url, method) =>
      method === 'GET' ? ok(CONFIG) : ok(CONFIG),
    )

    await screen.findByLabelText('Base URL')
    fireEvent.change(screen.getByLabelText('Base URL'), { target: { value: 'https://x.example.com' } })
    await act(() => router.navigate('/routes'))
    await screen.findByRole('alertdialog')

    await user.click(screen.getByRole('button', { name: 'Save and leave' }))

    await waitFor(() => {
      expect(calls.some((call) => call.method === 'PUT')).toBe(true)
    })
    expect(await screen.findByText('Routes page')).toBeInTheDocument()
  })

  it('does not ask when nothing has changed', async () => {
    const { router } = renderPage(() => ok(CONFIG))

    await screen.findByLabelText('Base URL')
    await act(() => router.navigate('/routes'))

    // The prompt on every navigation is worse than no prompt at all.
    expect(await screen.findByText('Routes page')).toBeInTheDocument()
  })

  it('asks the browser before a reload or a closed tab', async () => {
    // beforeunload is the half of the guard the browser owns. The spy has to
    // be in place before the render, because the listener is registered from a
    // mount effect — spying afterwards would test nothing.
    const add = vi.spyOn(window, 'addEventListener')

    renderPage(() => ok(CONFIG))
    await screen.findByLabelText('Base URL')
    fireEvent.change(screen.getByLabelText('Base URL'), {
      target: { value: 'https://x.example.com' },
    })

    expect(add.mock.calls.some(([event]) => event === 'beforeunload')).toBe(true)
  })

  it('does not register the browser guard while the form is clean', async () => {
    // Otherwise every reload of an untouched page would prompt.
    const add = vi.spyOn(window, 'addEventListener')

    renderPage(() => ok(CONFIG))
    await screen.findByLabelText('Base URL')

    expect(add.mock.calls.some(([event]) => event === 'beforeunload')).toBe(false)
  })
})
