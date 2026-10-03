import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { MonitoringPage } from '@/features/monitoring/monitoring-page'
import { heartbeatAge, rangeSince, summarise } from '@/features/monitoring/queries'
import type { DeliveryMetricsResponse, RuntimeGatewaysResponse } from '@/api'

const ACTIVE: DeliveryMetricsResponse = {
  from: '2026-10-01T00:00:00Z',
  to: '2026-10-03T00:00:00Z',
  consideredAttempts: 172,
  totalAttempts: 172,
  successful: 2,
  failed: 170,
  successRate: 2 / 172,
  snapshotVersions: [2, 1],
  gateways: [
    {
      gatewayId: '3ba143db-8ee4-40f0-a428-3bbf941f37d2',
      attempts: 3,
      successful: 1,
      failed: 2,
      successRate: 1 / 3,
      lastAttemptAt: '2026-10-02T23:39:39.2509929Z',
      recentErrors: ['GlobalConfiguration is required'],
    },
  ],
  errors: [
    {
      message: 'GlobalConfiguration is required',
      occurrences: 166,
      lastSeenAt: '2026-10-02T23:39:39.2509929Z',
    },
  ],
}

const NOTHING_ATTEMPTED: DeliveryMetricsResponse = {
  ...ACTIVE,
  consideredAttempts: 172,
  totalAttempts: 0,
  successful: 0,
  failed: 0,
  // Null, not 1: nothing was asked of any gateway, so there is no rate to state.
  successRate: null,
  gateways: [],
  errors: [],
}

const GATEWAYS: RuntimeGatewaysResponse = {
  gateways: [
    {
      gatewayId: '3ba143db-8ee4-40f0-a428-3bbf941f37d2',
      status: 'Active',
      currentVersion: 2,
      targetVersion: null,
      lastHeartbeat: new Date(Date.now() - 5_000).toISOString(),
      lastSynchronized: null,
      lastConfigApplied: null,
      runtimeInfo: {},
      capabilities: ['Config'],
      activeRoutes: [],
    },
    {
      gatewayId: 'e6cc2784-7c3d-42d0-8351-2c642db152db',
      status: 'Disconnected',
      currentVersion: 1,
      targetVersion: 3,
      lastHeartbeat: new Date(Date.now() - 7 * 86400_000).toISOString(),
      lastSynchronized: null,
      lastConfigApplied: null,
      runtimeInfo: {},
      capabilities: [],
      activeRoutes: [],
    },
  ],
}

type Handler = (url: string, method: string) => Response

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

function renderMonitoring(
  handler: Handler,
  metrics: DeliveryMetricsResponse = ACTIVE,
  gateways: RuntimeGatewaysResponse = GATEWAYS,
) {
  const fetchImpl = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    if (url.includes('/delivery-metrics')) return handler(url, method)
    if (url.includes('/runtime/gateways')) return handler(`gateways${url}`, method)
    if (url.includes('/runtime/reconcile')) return handler(url, method)
    return fail(404, `unexpected: ${url}`)
  })

  const client = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: fetchImpl as unknown as typeof fetch,
  })

  const router = createMemoryRouter([{ path: '/monitoring', element: <MonitoringPage /> }], {
    initialEntries: ['/monitoring'],
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

  return { fetchImpl, metrics, gateways }
}

const standard: Handler = (url) =>
  url.includes('delivery-metrics')
    ? ok(ACTIVE)
    : url.startsWith('gateways')
      ? ok(GATEWAYS)
      : fail(404, `unexpected: ${url}`)

describe('monitoring page', () => {
  it('reports what gateways actually experienced, not a request rate', async () => {
    renderMonitoring(standard)

    await waitFor(() => expect(screen.getAllByText('Attempts').length).toBeGreaterThan(0))
    expect(screen.getByText('172')).toBeInTheDocument()
    expect(screen.getByText('170')).toBeInTheDocument()

    // The dominant fault is named, with how often it happened.
    await waitFor(() =>
      expect(screen.getAllByText('GlobalConfiguration is required').length).toBe(2),
    )
    expect(screen.getByText(/×166/)).toBeInTheDocument()
  })

  it('says traffic is not measured rather than showing a rate of zero', async () => {
    renderMonitoring(standard)

    await waitFor(() => expect(screen.getByText('Not measured')).toBeInTheDocument())
    expect(
      screen.getByText(/No gateway counts the requests it serves or times them/),
    ).toBeInTheDocument()
  })

  it('states no rate at all when nothing was attempted', async () => {
    // 0% and 100% would each be a claim nobody measured.
    renderMonitoring(
      (url) =>
        url.includes('delivery-metrics')
          ? ok(NOTHING_ATTEMPTED)
          : url.startsWith('gateways')
            ? ok(GATEWAYS)
            : fail(404, url),
    )

    await waitFor(() => expect(screen.getByText('No attempts in this range')).toBeInTheDocument())
    expect(screen.getByText(/That is not a success rate/)).toBeInTheDocument()
  })

  it('shows how many attempts were examined, so a bounded read is visible', async () => {
    renderMonitoring(standard)

    await waitFor(() => expect(screen.getByText(/172 attempts examined/)).toBeInTheDocument())
  })

  it('says a gateway that stopped reporting is not reporting', async () => {
    renderMonitoring(standard)

    await waitFor(() => expect(screen.getByText(/not reporting/)).toBeInTheDocument())
    // The one that is current is not described that way.
    expect(screen.queryByText(/never reported/)).not.toBeInTheDocument()
  })

  it('shows the configuration version each gateway is running', async () => {
    renderMonitoring(standard)

    await waitFor(() => expect(screen.getByText(/running v2/)).toBeInTheDocument())
  })

  it('reports a failed delivery report rather than showing empty figures', async () => {
    renderMonitoring((url) =>
      url.includes('delivery-metrics')
        ? fail(500, 'the metrics request failed')
        : url.startsWith('gateways')
          ? ok(GATEWAYS)
          : fail(404, url),
    )

    await waitFor(() =>
      expect(screen.getByText('Delivery metrics could not be read')).toBeInTheDocument(),
    )
  })

  it('narrows the range when a different one is chosen', async () => {
    const { fetchImpl } = renderMonitoring(standard)

    await waitFor(() => expect(screen.getAllByText('Attempts').length).toBeGreaterThan(0))

    await userEvent.click(screen.getByRole('combobox'))
    await userEvent.click(await screen.findByRole('option', { name: 'Last 7 days' }))

    await waitFor(() => {
      const urls = fetchImpl.mock.calls.map((call) => String(call[0]))
      expect(urls.some((url) => url.includes('from=') && !url.includes('T-'))).toBe(true)
    })
  })

  it('asks a gateway to reconcile and says what happened', async () => {
    const { fetchImpl } = renderMonitoring((url) => {
      if (url.includes('delivery-metrics')) return ok(ACTIVE)
      if (url.startsWith('gateways')) return ok(GATEWAYS)
      if (url.includes('/runtime/reconcile')) {
        return ok({
          gatewayId: 'e6cc2784-7c3d-42d0-8351-2c642db152db',
          targetVersion: 3,
          success: true,
          errorMessage: null,
          reconciledAt: '2026-10-03T09:00:00Z',
        })
      }
      return fail(404, url)
    })

    await waitFor(() => expect(screen.getAllByText('Reconcile').length).toBe(2))

    // Nothing to reconcile to, so there is nothing to ask for.
    const buttons = screen.getAllByText('Reconcile') as HTMLButtonElement[]
    expect(buttons[0].disabled).toBe(true)

    await userEvent.click(buttons[1])

    await waitFor(() => expect(fetchImpl.mock.calls.length).toBeGreaterThan(2))
    const reconcileCall = fetchImpl.mock.calls
      .map((call) => String(call[0]))
      .find((url) => url.includes('/runtime/reconcile'))

    expect(reconcileCall).toBeDefined()
    await waitFor(() => expect(screen.getByText('Reconcile accepted')).toBeInTheDocument())
  })
})

describe('delivery metrics shaping', () => {
  it('keeps a missing rate missing rather than turning it into zero', () => {
    const summary = summarise(NOTHING_ATTEMPTED)

    expect(summary.attempts).toBe(0)
    expect(summary.successRate).toBeNull()
  })

  it('counts failures by cause', () => {
    const summary = summarise(ACTIVE)

    expect(summary.failed).toBe(170)
    expect(summary.distinctFailures).toBe(1)
    expect(summary.gatewayCount).toBe(1)
  })

  it('treats a gateway that has never reported as having no data, not stale data', () => {
    expect(heartbeatAge(null)).toBeNull()
  })

  it('marks a gateway that stopped reporting as stale', () => {
    const now = new Date('2026-10-03T12:00:00Z')

    // Reporting now is not stale, and says so in seconds rather than minutes.
    const fresh = heartbeatAge('2026-10-03T11:59:30Z', now)
    expect(fresh?.stale).toBe(false)
    expect(fresh?.text).toBe('30s ago')

    // An hour is well past the 90s the runtime reports every 30s.
    const old = heartbeatAge('2026-10-03T11:00:00Z', now)
    expect(old?.stale).toBe(true)
    expect(old?.text).toBe('1h ago')

    expect(heartbeatAge('2026-09-26T12:00:00Z', now)?.stale).toBe(true)
  })

  it('expresses the range as a window ending now', () => {
    const now = new Date('2026-10-03T12:00:00Z')

    expect(rangeSince(24, now)).toBe('2026-10-02T12:00:00.000Z')
  })
})