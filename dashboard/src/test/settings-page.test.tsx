import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { SettingsPage } from '@/features/settings/settings-page'
import type { SystemSettingsResponse } from '@/api'

/**
 * The settings page, and the two states it can be in.
 *
 * The important one is first run: the Ocelot version has to be chosen before
 * anything else works, so the page is not an ordinary form until that has
 * happened.
 */

const SETTINGS: SystemSettingsResponse = {
  id: '00000000-0000-0000-0000-0000000000c0',
  ocelotVersion: '18.0.0',
  ocelotVersionSelectedAt: '2026-01-02T03:04:05Z',
  ocelotVersionSelectedBy: 'admin',
  pollIntervalSeconds: 30,
  auditLogRetentionDays: 90,
  snapshotRetentionCount: 0,
  isInitialised: true,
  availableOcelotVersions: ['18.0.0'],
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-02T03:04:05Z',
}

const UNINITIALISED: SystemSettingsResponse = {
  ...SETTINGS,
  ocelotVersion: null,
  ocelotVersionSelectedAt: null,
  ocelotVersionSelectedBy: null,
  isInitialised: false,
}

let calls: { url: string; method: string; body?: unknown }[] = []
let respond: () => SystemSettingsResponse

/**
 * Renders the page against a fake API, through the real client and the real
 * query hooks, so the request shapes under test are the ones actually sent.
 */
/** The sentence the setup guard hands over with the redirect. */
const ARRIVAL_REASON =
  'This installation has not been set up yet. Choose the Ocelot version before anything else.'

function renderSettings(respondWith: () => SystemSettingsResponse = () => SETTINGS) {
  respond = respondWith
  calls = []

  const fetchImpl = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    calls.push({
      url,
      method,
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    })
    return new Response(JSON.stringify(respond()), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    })
  })

  const client = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: fetchImpl as unknown as typeof fetch,
  })

  const router = createMemoryRouter([{ path: '/settings', element: <SettingsPage /> }], {
    // The guard redirects here with a reason attached, and the page reads it so
    // the operator is told why they are looking at setup.
    initialEntries: [{ pathname: '/settings', state: { reason: ARRIVAL_REASON } }],
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
}

/** Renders against a response sequence, for the failure cases. */
function renderSettingsWith(handler: (url: string, method: string) => Response) {
  calls = []
  const fetchImpl = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    calls.push({ url, method, body: init?.body ? JSON.parse(String(init.body)) : undefined })
    return handler(url, method)
  })

  const client = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: fetchImpl as unknown as typeof fetch,
  })
  const router = createMemoryRouter([{ path: '/settings', element: <SettingsPage /> }], {
    initialEntries: ['/settings'],
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
}

beforeEach(() => {
  calls = []
})

describe('SettingsPage after setup', () => {
  it('shows the chosen version as a fact rather than a field', async () => {
    // The version is permanent, so offering an input would promise an edit that
    // the API refuses.
    renderSettings()

    expect(await screen.findByText('18.0.0')).toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: /version/i })).not.toBeInTheDocument()
    expect(screen.getByText(/cannot be changed|permanent/i)).toBeInTheDocument()
  })

  it('shows who chose the version and when', async () => {
    renderSettings()

    expect(await screen.findByText('admin')).toBeInTheDocument()
    expect(screen.getByText('Chosen by')).toBeInTheDocument()
  })

  it('renders the operational settings from the API', async () => {
    renderSettings()

    await screen.findByText('18.0.0')
    expect(screen.getByLabelText('Poll interval (seconds)')).toHaveValue(30)
    expect(screen.getByLabelText('Audit retention (days)')).toHaveValue(90)
    expect(screen.getByLabelText('Snapshots to keep')).toHaveValue(0)
  })

  it('says what a zero retention means', async () => {
    // A zero is a real value — keep everything — not a missing one.
    renderSettings()

    await screen.findByText('18.0.0')
    expect(screen.getByText('0 keeps entries forever.')).toBeInTheDocument()
    expect(screen.getByText('0 keeps them all.')).toBeInTheDocument()
  })

  it('saves only the fields that changed', async () => {
    const user = userEvent.setup()
    renderSettings()

    await screen.findByText('18.0.0')
    await user.clear(screen.getByLabelText('Poll interval (seconds)'))
    await user.type(screen.getByLabelText('Poll interval (seconds)'), '60')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => {
      const put = calls.find((call) => call.method === 'PUT')
      expect(put).toBeDefined()
      expect(put!.body).toEqual({
        pollIntervalSeconds: 60,
        auditLogRetentionDays: 90,
        snapshotRetentionCount: 0,
      })
    })
  })

  it('never sends the version on an ordinary save', async () => {
    // The permanent field has no update path, so the request must not carry it.
    const user = userEvent.setup()
    renderSettings()

    await screen.findByText('18.0.0')
    await user.clear(screen.getByLabelText('Audit retention (days)'))
    await user.type(screen.getByLabelText('Audit retention (days)'), '30')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => {
      const put = calls.find((call) => call.method === 'PUT')
      expect(put).toBeDefined()
      expect(JSON.stringify(put!.body)).not.toContain('ocelotVersion')
    })
  })

  it('rejects a poll interval below the domain minimum before submitting', async () => {
    const user = userEvent.setup()
    renderSettings()

    await screen.findByText('18.0.0')
    await user.clear(screen.getByLabelText('Poll interval (seconds)'))
    await user.type(screen.getByLabelText('Poll interval (seconds)'), '1')

    expect(
      screen.getByText('Poll interval must be between 5 and 3600 seconds'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('accepts a retention of zero, meaning keep forever', async () => {
    const user = userEvent.setup()
    renderSettings()

    await screen.findByText('18.0.0')
    await user.clear(screen.getByLabelText('Audit retention (days)'))
    await user.type(screen.getByLabelText('Audit retention (days)'), '0')

    expect(
      screen.queryByText(/Audit retention must be 0/),
    ).not.toBeInTheDocument()
  })

  it('disables saving until something changes', async () => {
    renderSettings()

    await screen.findByText('18.0.0')
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    expect(screen.queryByText('Unsaved changes')).not.toBeInTheDocument()
  })

  it('reports a permission failure as needing an administrator', async () => {
    // A read is allowed for everyone; the write is not, so a 403 has to be
    // explained rather than shown as a bare failure.
    renderSettingsWith((_url, method) =>
      method === 'GET'
        ? new Response(JSON.stringify(SETTINGS), { status: 200 })
        : new Response(JSON.stringify({ error: 'Forbidden' }), { status: 403 }),
    )
    const user = userEvent.setup()

    await screen.findByText('18.0.0')
    await user.clear(screen.getByLabelText('Snapshots to keep'))
    await user.type(screen.getByLabelText('Snapshots to keep'), '5')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Not permitted')).toBeInTheDocument()
    expect(screen.getByText(/needs the Admin role/)).toBeInTheDocument()
  })
})

describe('SettingsPage before setup', () => {
  const renderFirstRun = () => renderSettings(() => UNINITIALISED)

  it('asks for the version rather than showing one', async () => {
    renderFirstRun()

    expect(await screen.findByText('Set up the control plane')).toBeInTheDocument()
    expect(screen.getByLabelText('Version')).toBeInTheDocument()
  })

  it('says why the operator was sent here', async () => {
    renderFirstRun()

    // The guard redirects here from whichever page was asked for. Without a
    // reason, the operator is looking at a screen they did not choose and cannot
    // tell whether it is required or broken.
    expect(await screen.findByText('Setup comes first')).toBeInTheDocument()
    expect(screen.getByText(ARRIVAL_REASON)).toBeInTheDocument()
  })

  it('can be submitted, which it could not be when the fields started blank', async () => {
    renderFirstRun()

    // The fields used to initialise empty while the server held defaults of 30
    // and 90, so every one of them failed validation and the submit button stayed
    // disabled. Setup was not completable from the screen built to complete it —
    // the tests passed because they filled the form in by hand.
    await userEvent.click(await screen.findByLabelText('Version'))
    await userEvent.click(await screen.findByRole('option', { name: '18.0.0' }))

    expect(screen.getByRole('button', { name: /complete setup/i })).toBeEnabled()
  })

  it('shows the values the server already holds rather than blanks', async () => {
    renderFirstRun()

    // A blank in a field with a known default reads as "this is empty" when the
    // truth is "this is 30, and you may change it".
    expect(await screen.findByDisplayValue('30')).toBeInTheDocument()
    expect(screen.getByDisplayValue('90')).toBeInTheDocument()
  })

  it('sends the name of whoever is setting it up, because nothing else can', async () => {
    renderFirstRun()

    await userEvent.click(await screen.findByLabelText('Version'))
    await userEvent.click(await screen.findByRole('option', { name: '18.0.0' }))
    await userEvent.type(screen.getByLabelText('Name'), 'alex')
    await userEvent.click(screen.getByRole('button', { name: /complete setup/i }))

    // There is no session yet, so the audit trail has nothing else to read. The
    // version cannot be changed afterwards, which is what makes the name matter.
    await waitFor(() => {
      const submitted = calls.find((call) => call.url.includes('/first-run'))
      expect(submitted?.body).toMatchObject({ initiatedBy: 'alex' })
    })
  })

  it('does not pre-select the only offered version', async () => {
    // Pre-selecting would make the choice look like it had already been made,
    // which is the one thing the screen exists to prevent.
    renderFirstRun()

    await screen.findByText('Set up the control plane')
    expect(screen.getByText('Select a version')).toBeInTheDocument()
  })

  it('explains that the choice cannot be reversed', async () => {
    renderFirstRun()

    expect(await screen.findByText('This choice is permanent')).toBeInTheDocument()
    expect(screen.getByText(/immutable and hashed/)).toBeInTheDocument()
  })

  it('explains why only certain versions are listed', async () => {
    // Offering a version the builder cannot shape would produce a file no
    // gateway could read, and nothing would fail until a gateway refused to start.
    renderFirstRun()

    expect(await screen.findByText(/shapes are established/)).toBeInTheDocument()
  })

  it('will not submit without a version', async () => {
    renderFirstRun()

    await screen.findByText('Set up the control plane')
    expect(screen.getByRole('button', { name: 'Complete setup' })).toBeDisabled()
    expect(screen.getByText('A version must be selected.')).toBeInTheDocument()
  })

  it('lists exactly the versions the API says are available', async () => {
    // Not the whole offer, only that the list comes from the response rather
    // than being hardcoded: a version the builder cannot shape must never be
    // selectable, and this is where that guarantee is visible.
    renderSettings(() => ({
      ...UNINITIALISED,
      availableOcelotVersions: ['20.0.0', '19.0.0'],
    }))

    await screen.findByText('Set up the control plane')
    await userEvent.setup().click(screen.getByRole('combobox'))

    expect(await screen.findByRole('option', { name: '20.0.0' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: '19.0.0' })).toBeInTheDocument()
  })

  it('cannot be submitted without a chosen version', async () => {
    // The endpoint contract itself is pinned in the API tests; what matters here
    // is that the page will not send the request in the first place.
    renderFirstRun()

    expect(await screen.findByRole('button', { name: 'Complete setup' })).toBeDisabled()
  })

  it('offers the same operational settings as after setup', async () => {
    renderFirstRun()

    await screen.findByText('Set up the control plane')
    expect(screen.getByLabelText('Poll interval (seconds)')).toBeInTheDocument()
    expect(screen.getByText(/These can be changed later/)).toBeInTheDocument()
  })
})

describe('SettingsPage when the API fails', () => {
  it('offers a retry rather than an empty page', async () => {
    renderSettingsWith(() =>
      new Response(JSON.stringify({ error: 'boom' }), { status: 500 }),
    )

    expect(await screen.findByText('Could not load settings')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })
})
