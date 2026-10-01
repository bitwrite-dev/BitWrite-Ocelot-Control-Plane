import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { SetupGuard } from '@/components/setup-guard'
import type { SystemSettingsResponse } from '@/api'

const UNCONFIGURED: SystemSettingsResponse = {
  id: '00000000-0000-0000-0000-0000000000c0',
  ocelotVersion: null,
  ocelotVersionSelectedAt: null,
  ocelotVersionSelectedBy: null,
  pollIntervalSeconds: 30,
  auditLogRetentionDays: 90,
  snapshotRetentionCount: 0,
  isInitialised: false,
  availableOcelotVersions: ['18.0.0'],
  createdAt: '2026-09-30T19:51:49Z',
  updatedAt: '2026-09-30T19:51:49Z',
}

const CONFIGURED: SystemSettingsResponse = {
  ...UNCONFIGURED,
  ocelotVersion: '18.0.0',
  ocelotVersionSelectedBy: 'alex',
  ocelotVersionSelectedAt: '2026-09-30T20:00:00Z',
  isInitialised: true,
}

type Handler = (url: string) => Response

function renderGuard(handler: Handler, initialPath = '/snapshots') {
  const fetchImpl = vi.fn(async (input: RequestInfo | URL) => handler(String(input)))

  const client = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: fetchImpl as unknown as typeof fetch,
  })

  const guarded = (page: React.ReactNode) => <SetupGuard>{page}</SetupGuard>

  const router = createMemoryRouter(
    [
      { path: '/snapshots', element: guarded(<p>Snapshots</p>) },
      { path: '/gateways', element: guarded(<p>Gateways</p>) },
      { path: '/settings', element: guarded(<p>Setup screen</p>) },
    ],
    { initialEntries: [initialPath] },
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

  return router
}

const settings = (payload: SystemSettingsResponse): Handler => (url) =>
  url.includes('/api/v1/settings')
    ? new Response(JSON.stringify(payload), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      })
    : new Response('{}', { status: 200, headers: { 'content-type': 'application/json' } })

describe('SetupGuard', () => {
  it('sends an unconfigured install to setup instead of the page it asked for', async () => {
    const router = renderGuard(settings(UNCONFIGURED))

    // Nothing did this, so an operator whose first action was "create a snapshot"
    // was met by a page that could only report a failure, in words about a
    // configuration rather than about setup.
    expect(await screen.findByText('Setup screen')).toBeInTheDocument()
    expect(screen.queryByText('Snapshots')).not.toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/settings')
  })

  it('says why the operator is looking at setup', async () => {
    const router = renderGuard(settings(UNCONFIGURED))

    await screen.findByText('Setup screen')

    // A redirect that arrives without an explanation looks like the page was
    // chosen for them, and they cannot tell whether to trust it.
    const state = router.state.location.state as { reason?: string } | null
    expect(state?.reason).toMatch(/not been set up yet/i)
  })

  it('leaves setup reachable rather than redirecting in a loop', async () => {
    renderGuard(settings(UNCONFIGURED), '/settings')

    // A guard that redirected its own escape hatch would hang here instead.
    expect(await screen.findByText('Setup screen')).toBeInTheDocument()
  })

  it('lets a configured install through to whatever it asked for', async () => {
    const router = renderGuard(settings(CONFIGURED))

    expect(await screen.findByText('Snapshots')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/snapshots')
  })

  it('does not trap the operator when the settings cannot be read', async () => {
    // The API being down is a different problem from setup being undone, and an
    // interstitial that only offers setup would be the wrong screen for it.
    const router = renderGuard(() =>
      new Response(JSON.stringify({ error: 'the store is down' }), {
        status: 500,
        headers: { 'content-type': 'application/json' },
      }),
    )

    expect(await screen.findByText('Snapshots')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/snapshots')
  })

  it('holds back every page, not just the ones about configuration', async () => {
    renderGuard(settings(UNCONFIGURED), '/gateways')

    await screen.findByText('Setup screen')
    expect(screen.queryByText('Gateways')).not.toBeInTheDocument()
  })
})
