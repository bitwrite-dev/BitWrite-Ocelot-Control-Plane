import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { fireEvent } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { GatewaysPage } from '@/features/gateways/gateways-page'
import type { GatewayResponse } from '@/api'

const GATEWAY: GatewayResponse = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'edge-eu',
  description: 'EU edge',
  status: 'Active',
  createdAt: '2026-01-02T03:04:05Z',
  updatedAt: '2026-01-03T04:05:06Z',
  lastHeartbeat: null,
}

const SECOND: GatewayResponse = {
  ...GATEWAY,
  id: '22222222-2222-2222-2222-222222222222',
  name: 'edge-us',
  description: null,
  status: 'Degraded',
}

type Handler = (url: string, method: string, body: unknown) => Response

function list(gateways: GatewayResponse[], totalCount = gateways.length, page = 1) {
  return { gateways, totalCount, page, pageSize: 20 }
}

function renderGateways(handler: Handler) {
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

  const router = createMemoryRouter([{ path: '/gateways', element: <GatewaysPage /> }], {
    initialEntries: ['/gateways'],
  })

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
    headers: { 'Content-Type': 'application/json' },
  })

beforeEach(() => {
  vi.restoreAllMocks()
})

describe('GatewaysPage', () => {
  it('lists the gateways the API returns', async () => {
    renderGateways(() => ok(list([GATEWAY, SECOND])))

    expect(await screen.findByText('edge-eu')).toBeInTheDocument()
    expect(screen.getByText('edge-us')).toBeInTheDocument()
  })

  it('shows the recorded status, and says it is recorded rather than live', async () => {
    // A stale "Active" looks like a healthy gateway, so the page has to be
    // explicit that this is the label the control plane stores.
    renderGateways(() => ok(list([GATEWAY])))

    await screen.findByText('edge-eu')
    const table = screen.getByRole('table')
    const row = within(table).getByRole('row', { name: /edge-eu/ })
    expect(within(row).getAllByText('Active').length).toBeGreaterThan(0)
    expect(screen.getByText(/the one this control plane records/i)).toBeInTheDocument()
    expect(screen.getByText(/live health/i)).toBeInTheDocument()
  })

  it('renders an absent description as a dash rather than a blank', async () => {
    renderGateways(() => ok(list([SECOND])))

    await screen.findByText('edge-us')
    expect(screen.getByText('—')).toBeInTheDocument()
  })

  it('says a gateway is needed before anything can be published', async () => {
    renderGateways(() => ok(list([])))

    expect(
      await screen.findByText(/No gateways are registered yet/),
    ).toBeInTheDocument()
  })

  it('pages server-side rather than slicing the list', async () => {
    const { calls } = renderGateways(() => ok(list([GATEWAY], 45, 1)))

    await screen.findByText('edge-eu')
    expect(screen.getByText(/45 gateways/)).toBeInTheDocument()
    expect(calls[0].url).toContain('page=1')
    expect(calls[0].url).toContain('pageSize=20')
  })

  it('does not offer a status outside the ones the API accepts', async () => {
    // RuntimeStatus.From rejects anything else, so a wider list would only
    // produce a failed save.
    renderGateways(() => ok(list([GATEWAY])))

    await screen.findByText('edge-eu')
    await userEvent.setup().click(screen.getByRole('combobox'))

    for (const status of [
      'Disconnected',
      'Connecting',
      'Synchronized',
      'Applying',
      'Active',
      'Degraded',
    ]) {
      expect(screen.getByRole('option', { name: status })).toBeInTheDocument()
    }
    expect(screen.queryByRole('option', { name: 'Shutdown' })).not.toBeInTheDocument()
    expect(screen.queryByRole('option', { name: 'Stopped' })).not.toBeInTheDocument()
  })

  it('registers a gateway with a trimmed name and a null description', async () => {
    const { calls } = renderGateways((_url, method) =>
      method === 'GET' ? ok(list([])) : ok(GATEWAY),
    )
    const user = userEvent.setup()

    await screen.findByText(/No gateways are registered yet/)
    await user.click(screen.getByRole('button', { name: /New gateway/ }))
    await user.type(await screen.findByLabelText('Name'), '  edge-ap  ')
    await user.click(screen.getByRole('button', { name: 'Register' }))

    await waitFor(() => {
      const post = calls.find((call) => call.method === 'POST')
      expect(post?.body).toEqual({ name: 'edge-ap', description: null })
    })
  })

  it('rejects an empty name before sending it', async () => {
    const { calls } = renderGateways(() => ok(list([])))
    const user = userEvent.setup()

    await screen.findByText(/No gateways are registered yet/)
    await user.click(screen.getByRole('button', { name: /New gateway/ }))
    await user.click(await screen.findByRole('button', { name: 'Register' }))

    expect(await screen.findByText('A name is required')).toBeInTheDocument()
    expect(calls.some((call) => call.method === 'POST')).toBe(false)
  })

  it('rejects a name longer than the API allows', async () => {
    renderGateways(() => ok(list([])))
    const user = userEvent.setup()

    await screen.findByText(/No gateways are registered yet/)
    await user.click(screen.getByRole('button', { name: /New gateway/ }))
    fireEvent.change(await screen.findByLabelText('Name'), {
      target: { value: 'x'.repeat(201) },
    })

    expect(
      await screen.findByText('Name must be 200 characters or fewer'),
    ).toBeInTheDocument()
  })
})

describe('deleting a gateway', () => {
  it('asks whether it can be deleted rather than assuming it can', async () => {
    // The delete is refused once a publication exists, and learning that only
    // from the refusal means the operator already confirmed the action.
    const { calls } = renderGateways((url) =>
      url.includes('deletion-eligibility')
        ? ok({ gatewayId: GATEWAY.id, hasBeenPublishedTo: false, reason: null })
        : ok(list([GATEWAY])),
    )
    const user = userEvent.setup()

    await screen.findByText('edge-eu')
    await user.click(screen.getByRole('button', { name: 'Delete edge-eu' }))

    expect(await screen.findByText(/has never received a publication/)).toBeInTheDocument()
    expect(calls.some((call) => call.url.includes('deletion-eligibility'))).toBe(true)
  })

  it('blocks the delete and explains why when one has been published to', async () => {
    renderGateways((url) =>
      url.includes('deletion-eligibility')
        ? ok({
            gatewayId: GATEWAY.id,
            hasBeenPublishedTo: true,
            reason:
              'This gateway has received a publication. A published snapshot is an ' +
              'immutable, hashed record that refers to it, so it cannot be removed.',
          })
        : ok(list([GATEWAY])),
    )
    const user = userEvent.setup()

    await screen.findByText('edge-eu')
    await user.click(screen.getByRole('button', { name: 'Delete edge-eu' }))

    expect(await screen.findByText('Cannot be deleted')).toBeInTheDocument()
    expect(screen.getByText(/immutable, hashed record/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Delete' })).toBeDisabled()
  })

  it('does not offer a delete for a published gateway, so nothing is wasted', async () => {
    const { calls } = renderGateways((url) =>
      url.includes('deletion-eligibility')
        ? ok({ gatewayId: GATEWAY.id, hasBeenPublishedTo: true, reason: 'nope' })
        : ok(list([GATEWAY])),
    )
    const user = userEvent.setup()

    await screen.findByText('edge-eu')
    await user.click(screen.getByRole('button', { name: 'Delete edge-eu' }))
    await screen.findByText('Cannot be deleted')
    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(calls.some((call) => call.method === 'DELETE')).toBe(false)
  })
})

describe('when the API fails', () => {
  it('offers a retry rather than an empty table', async () => {
    renderGateways(() => new Response(JSON.stringify({ error: 'boom' }), { status: 500 }))

    expect(await screen.findByText('Could not load gateways')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })

  it('explains a permission failure in terms of the role needed', async () => {
    renderGateways((_url, method) =>
      method === 'GET'
        ? ok(list([]))
        : new Response(JSON.stringify({ error: 'Forbidden' }), { status: 403 }),
    )
    const user = userEvent.setup()

    await screen.findByText(/No gateways are registered yet/)
    await user.click(screen.getByRole('button', { name: /New gateway/ }))
    await user.type(await screen.findByLabelText('Name'), 'edge-ap')
    await user.click(screen.getByRole('button', { name: 'Register' }))

    expect(await screen.findByText('Not permitted')).toBeInTheDocument()
    expect(screen.getByText(/GatewayManager or Admin role/)).toBeInTheDocument()
  })
})

describe('the last report column', () => {
  const minutesAgo = (minutes: number) =>
    new Date(Date.now() - minutes * 60_000).toISOString()

  it('says so plainly when a gateway has never reported', async () => {
    // A blank cell would be indistinguishable from a gateway that has not been
    // checked, which is the opposite of what an operator needs here.
    renderGateways(() => ok(list([GATEWAY])))

    await screen.findByText('edge-eu')
    const row = within(screen.getByRole('table')).getByRole('row', { name: /edge-eu/ })
    expect(within(row).getByText('Never reported')).toBeInTheDocument()
  })

  it('reports a recent beat in relative time', async () => {
    renderGateways(() =>
      ok(list([{ ...GATEWAY, lastHeartbeat: minutesAgo(2) }])),
    )

    await screen.findByText('edge-eu')
    expect(screen.getByText('2 min ago')).toBeInTheDocument()
  })

  it('reports hours and days rather than a wall of minutes', async () => {
    renderGateways(() => ok(list([{ ...GATEWAY, lastHeartbeat: minutesAgo(60 * 5) }])))
    await screen.findByText('edge-eu')
    expect(screen.getByText('5 h ago')).toBeInTheDocument()
  })

  it('marks a stale report, because the status beside it may no longer be true', async () => {
    renderGateways(() =>
      ok(list([{ ...GATEWAY, status: 'Active', lastHeartbeat: minutesAgo(60 * 24) }])),
    )

    await screen.findByText('edge-eu')
    // A gateway that has not reported in a day is not the thing "Active" says,
    // and the two are only useful together.
    const cell = screen.getByText('1 d ago')
    expect(cell).toHaveClass('text-destructive')
  })

  it('leaves a fresh report unmarked', async () => {
    renderGateways(() => ok(list([{ ...GATEWAY, lastHeartbeat: minutesAgo(1) }])))

    await screen.findByText('edge-eu')
    expect(screen.getByText('1 min ago')).not.toHaveClass('text-destructive')
  })
})
