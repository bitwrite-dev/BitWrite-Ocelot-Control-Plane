import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'

import { createApiClient } from '@/api'
import { ApiClientContext } from '@/app-providers'
import { AuditLogPage } from '@/features/audit/audit-log-page'
import { hasActiveFilters, toQuery } from '@/features/audit/queries'
import { describeRange } from '@/features/audit/paging'
import type { AuditResponse, AuditStatsResponse } from '@/api'

const ENTRY: AuditResponse = {
  id: 'audit_c4f799120af94368ba2d4b8c3d159baf',
  actor: 'alex',
  action: 'CreateService',
  resourceType: 'Service',
  resourceId: '5e05783b-732d-4ec2-ade7-df9e8297a6d9',
  result: 'Success',
  timestamp: '2026-10-02T21:30:18.4133387+00:00',
}

const FAILED: AuditResponse = {
  ...ENTRY,
  id: 'audit_other',
  action: 'DeleteService',
  result: 'Failed',
  timestamp: '2026-10-01T09:00:00Z',
}

const STATS: AuditStatsResponse = {
  totalCount: 7,
  todayCount: 3,
  thisWeekCount: 5,
  thisMonthCount: 7,
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

function renderAudit(handler: Handler) {
  const calls: string[] = []

  const fetchImpl = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input)
    calls.push(url)
    return handler(url, 'GET')
  })

  const client = createApiClient({
    baseUrl: 'http://api.test',
    fetchImpl: fetchImpl as unknown as typeof fetch,
  })

  const router = createMemoryRouter([{ path: '/audit', element: <AuditLogPage /> }], {
    initialEntries: ['/audit'],
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

const list = (audits: AuditResponse[], totalCount = audits.length, page = 1): Handler => (url) =>
  url.includes('/audit/stats')
    ? ok(STATS)
    : url.includes('/audit?')
      ? ok({ audits, totalCount, page, pageSize: 50 })
      : fail(404, `unexpected: ${url}`)

describe('AuditLogPage', () => {
  it('shows who did what, and whether it worked', async () => {
    renderAudit(list([ENTRY, FAILED]))

    const succeeded = await screen.findByRole('row', { name: /CreateService/ })
    within(succeeded).getByText('alex')
    within(succeeded).getByText('Service')
    within(succeeded).getByText('Success')

    // A failed action is the reason anyone reads this page, so it has to be
    // distinguishable at a glance rather than buried in the row.
    const failed = screen.getByRole('row', { name: /DeleteService/ })
    within(failed).getByText('Failed')
  })

  it('reports the counts across the whole log, not the page', async () => {
    renderAudit(list([ENTRY], 7))

    expect(await screen.findByText('All time')).toBeInTheDocument()
    expect(await screen.findByText('3')).toBeInTheDocument()
  })

  it('hides the counts once a filter is applied, because they describe everything', async () => {
    renderAudit(list([ENTRY], 7))

    await screen.findByText('All time')

    await userEvent.type(screen.getByLabelText('Actor'), 'alex')
    await userEvent.click(screen.getByRole('button', { name: /apply filters/i }))

    // "All time: 7" beside a table filtered to one actor is a contradiction, and
    // the two numbers look like they belong to the same table.
    await waitFor(() => {
      expect(screen.queryByText('All time')).not.toBeInTheDocument()
    })
  })

  it('sends the filters to the server rather than filtering the page in hand', async () => {
    const { calls } = renderAudit(list([ENTRY], 1))

    await userEvent.type(screen.getByLabelText('Actor'), 'alex')
    await userEvent.type(screen.getByLabelText('Action'), 'CreateService')
    await userEvent.click(screen.getByRole('button', { name: /apply filters/i }))

    await waitFor(() => {
      const filtered = calls.find(
        (url) => url.includes('actor=alex') && url.includes('action=CreateService'),
      )
      // The log grows without bound, so a total counted from the rows on screen
      // would be wrong as soon as there is more than one page.
      expect(filtered).toBeDefined();
    })
  })

  it('does not send an untouched filter as an empty one', async () => {
    const { calls } = renderAudit(list([ENTRY], 1))

    await userEvent.type(screen.getByLabelText('Actor'), 'alex')
    await userEvent.click(screen.getByRole('button', { name: /apply filters/i }))

    await waitFor(() => {
      const filtered = calls.find((url) => url.includes('actor=alex'));
      expect(filtered).toBeDefined();
      // `action=` with nothing after it filters for the empty action, which
      // matches nothing and reads as "this log has no entries".
      expect(filtered).not.toContain('action=');
    })
  })

  it('returns to the first page when the filter changes', async () => {
    const { calls } = renderAudit(list([ENTRY], 120))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled();
    })
    await userEvent.click(screen.getByRole('button', { name: 'Next' }))
    await waitFor(() => {
      expect(calls.some((url) => url.includes('page=2'))).toBe(true);
    })

    await userEvent.type(screen.getByLabelText('Actor'), 'alex')
    await userEvent.click(screen.getByRole('button', { name: /apply filters/i }))

    // Otherwise a narrow filter leaves the reader on page 7 of a list that no
    // longer has seven pages.
    await waitFor(() => {
      expect(calls.some((url) => url.includes('actor=alex') && url.includes('page=1'))).toBe(true);
    })
  })

  it('says so when a filter matches nothing, without implying the log is empty', async () => {
    renderAudit(list([], 0))

    await userEvent.type(screen.getByLabelText('Actor'), 'nobody')
    await userEvent.click(screen.getByRole('button', { name: /apply filters/i }))

    expect(
      await screen.findByText(/no entries match those filters/i),
    ).toBeInTheDocument();
  })

  it('offers to clear the filters, which is what an empty result needs', async () => {
    renderAudit(list([], 0))

    await userEvent.type(screen.getByLabelText('Actor'), 'nobody')
    await userEvent.click(screen.getByRole('button', { name: /apply filters/i }))
    await screen.findByText(/no entries match those filters/i)

    await userEvent.click(screen.getByRole('button', { name: /clear/i }))

    // The unfiltered list comes back, which is the point: the reader has to be
    // able to tell "nothing matches" from "nothing happened".
    await waitFor(() => {
      expect(screen.queryByText(/no entries match those filters/i)).not.toBeInTheDocument();
    });
  })

  it('offers a retry when the log cannot be loaded', async () => {
    let attempt = 0
    renderAudit(() => {
      attempt += 1
      return attempt === 1 ? fail(500, 'the store is down') : ok(STATS)
    })

    expect(await screen.findByText('Could not load the audit log')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /try again/i }))

    await waitFor(() => {
      expect(screen.queryByText('Could not load the audit log')).not.toBeInTheDocument();
    });
  })
})

describe('the audit helpers', () => {
  it('says which range is on screen, in words', () => {
    expect(describeRange(1, 50, 7)).toBe('1–7 of 7')
    expect(describeRange(2, 50, 120)).toBe('51–100 of 120')
    expect(describeRange(1, 50, 0)).toBe('No entries')
  })

  it('knows when a filter is set, and when nothing is', () => {
    expect(hasActiveFilters({})).toBe(false)
    expect(hasActiveFilters({ actor: '' })).toBe(false)
    expect(hasActiveFilters({ actor: 'alex' })).toBe(true);
  })

  it('carries only the filters that were set', () => {
    const query = toQuery({ actor: 'alex', action: '', resourceType: 'Service' })

    expect(query.actor).toBe('alex');
    expect(query.resourceType).toBe('Service');
    expect(query.action).toBeUndefined();
    expect(query.pageSize).toBe(50);
  })

  it('turns a chosen date into the whole day, not one instant', () => {
    // `<input type="date">` gives `2026-10-02` and nothing else. Bound as midnight
    // that day, `to=2026-10-02` would exclude everything written during the 2nd —
    // the reader picks the 2nd and gets the 1st.
    const query = toQuery({ from: '2026-10-02', to: '2026-10-02' })

    expect(query.from).toBe('2026-10-02T00:00:00.000Z');
    expect(query.to).toBe('2026-10-02T23:59:59.999Z');
  })

  it('states the day in UTC, where the records are', () => {
    // The server here runs Asia/Tehran, so `from=2026-10-03` bound to server-local
    // midnight becomes 2026-10-02T20:30Z — and an entry from 21:14Z the previous
    // evening counts as "from the 3rd". The records carry UTC timestamps, so the
    // day the reader means is the day they carry.
    expect(toQuery({ from: '2026-10-03' }).from).toBe('2026-10-03T00:00:00.000Z');
  })
})