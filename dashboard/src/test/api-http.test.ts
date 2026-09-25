import { describe, expect, it, vi } from 'vitest'

import { ApiError } from '@/api/errors'
import { createHttpClient } from '@/api/http'
import { createResources } from '@/api/resources'

function jsonResponse(body: unknown, init: ResponseInit = {}): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
    ...init,
  })
}

function client(fetchImpl: ReturnType<typeof vi.fn>, baseUrl = 'http://api.test') {
  return createHttpClient({ getAccessToken: () => null }, { baseUrl, fetchImpl: fetchImpl as unknown as typeof fetch })
}

describe('createHttpClient', () => {
  it('joins the base URL and path without doubling slashes', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({ ok: true }))

    await client(fetchImpl).get('/api/v1/routes')

    expect(fetchImpl.mock.calls[0][0]).toBe('http://api.test/api/v1/routes')
  })

  it('omits empty query parameters', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({}))

    await client(fetchImpl).get('/api/v1/routes', {
      query: { page: 1, pageSize: 20, search: '', isEnabled: undefined, serviceId: null },
    })

    expect(fetchImpl.mock.calls[0][0]).toBe('http://api.test/api/v1/routes?page=1&pageSize=20')
  })

  it('serializes boolean query parameters', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({}))

    await client(fetchImpl).get('/api/v1/routes', { query: { isEnabled: false } })

    expect(fetchImpl.mock.calls[0][0]).toBe('http://api.test/api/v1/routes?isEnabled=false')
  })

  it('attaches a bearer token when the provider returns one', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({}))
    const http = createHttpClient(
      { getAccessToken: () => 'token-123' },
      { baseUrl: 'http://api.test', fetchImpl: fetchImpl as unknown as typeof fetch },
    )

    await http.get('/api/v1/routes')

    const headers = fetchImpl.mock.calls[0][1].headers as Record<string, string>
    expect(headers.Authorization).toBe('Bearer token-123')
  })

  it('sends no Authorization header when unauthenticated', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({}))

    await client(fetchImpl).get('/api/v1/routes')

    const headers = fetchImpl.mock.calls[0][1].headers as Record<string, string>
    expect(headers.Authorization).toBeUndefined()
  })

  it('picks up a token that changes between requests', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({}))
    let token: string | null = null
    const http = createHttpClient(
      { getAccessToken: () => token },
      { baseUrl: 'http://api.test', fetchImpl: fetchImpl as unknown as typeof fetch },
    )

    await http.get('/api/v1/routes')
    token = 'refreshed'
    await http.get('/api/v1/routes')

    const first = fetchImpl.mock.calls[0][1].headers as Record<string, string>
    const second = fetchImpl.mock.calls[1][1].headers as Record<string, string>
    expect(first.Authorization).toBeUndefined()
    expect(second.Authorization).toBe('Bearer refreshed')
  })

  it('sets a JSON content type only when there is a body', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({}))
    const http = client(fetchImpl)

    await http.get('/api/v1/routes')
    await http.post('/api/v1/routes', { key: 'a' })

    const getHeaders = fetchImpl.mock.calls[0][1].headers as Record<string, string>
    const postHeaders = fetchImpl.mock.calls[1][1].headers as Record<string, string>
    expect(getHeaders['Content-Type']).toBeUndefined()
    expect(postHeaders['Content-Type']).toBe('application/json')
    expect(fetchImpl.mock.calls[1][1].body).toBe(JSON.stringify({ key: 'a' }))
  })

  it('returns undefined for an empty 204 body', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => new Response(null, { status: 204 }))

    await expect(client(fetchImpl).delete('/api/v1/routes/1')).resolves.toBeUndefined()
  })

  it('throws ApiError carrying the correlation id on failure', async () => {
    const fetchImpl = vi.fn().mockResolvedValue(
      jsonResponse({ correlationId: 'abc', error: 'boom', type: 'DomainException' }, { status: 500 }),
    )

    const error = (await client(fetchImpl).get('/api/v1/routes').catch((e: unknown) => e)) as ApiError

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(500)
    expect(error.correlationId).toBe('abc')
  })

  it('invokes onUnauthorized on a 401', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({ error: 'nope' }, { status: 401 }))
    const onUnauthorized = vi.fn()

    const http = createHttpClient(
      { getAccessToken: () => null },
      { baseUrl: 'http://api.test', fetchImpl: fetchImpl as unknown as typeof fetch },
    )

    await http.get('/api/v1/routes', { onUnauthorized }).catch(() => undefined)

    expect(onUnauthorized).toHaveBeenCalledOnce()
  })

  it('reports a network failure distinctly from an HTTP error', async () => {
    const fetchImpl = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'))

    const error = (await client(fetchImpl).get('/api/v1/routes').catch((e: unknown) => e)) as ApiError

    expect(error).toBeInstanceOf(ApiError)
    expect(error.isNetworkError).toBe(true)
    expect(error.status).toBe(0)
  })

  it('rethrows an AbortError untouched so callers can detect cancellation', async () => {
    const abortError = new DOMException('The operation was aborted.', 'AbortError')
    const fetchImpl = vi.fn().mockRejectedValue(abortError)

    const error = (await client(fetchImpl).get('/api/v1/routes').catch((e: unknown) => e)) as ApiError

    expect(error).toBe(abortError)
    expect(error).not.toBeInstanceOf(ApiError)
  })

  it('forwards the abort signal', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({}))
    const controller = new AbortController()

    await client(fetchImpl).get('/api/v1/routes', { signal: controller.signal })

    expect(fetchImpl.mock.calls[0][1].signal).toBe(controller.signal)
  })
})

describe('resources', () => {
  it('builds the correct routes list query string', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({ routes: [], totalCount: 0, page: 1, pageSize: 20 }))
    const resources = createResources(client(fetchImpl))

    await resources.routes.list({ page: 2, pageSize: 50, search: 'api', isEnabled: true, serviceId: 's1' })

    expect(fetchImpl.mock.calls[0][0]).toBe(
      'http://api.test/api/v1/routes?page=2&pageSize=50&search=api&isEnabled=true&serviceId=s1',
    )
  })

  it('interpolates ids and versions into paths', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({}))
    const resources = createResources(client(fetchImpl))

    await resources.routes.get('r-1')
    await resources.snapshots.compare(7)
    await resources.snapshots.rollback(3, { reason: 'regression' })
    await resources.services.remove('s-9')

    const urls = fetchImpl.mock.calls.map((c) => c[0])
    expect(urls).toEqual([
      'http://api.test/api/v1/routes/r-1',
      'http://api.test/api/v1/snapshots/7/compare',
      'http://api.test/api/v1/snapshots/3/rollback',
      'http://api.test/api/v1/services/s-9',
    ])
    expect(fetchImpl.mock.calls[2][1].method).toBe('POST')
    expect(fetchImpl.mock.calls[3][1].method).toBe('DELETE')
  })

  it('uses PATCH for the enable/disable actions, which take no body', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => jsonResponse({}))
    const resources = createResources(client(fetchImpl))

    await resources.plugins.enable('p-1')
    await resources.routes.disable('r-1')

    expect(fetchImpl.mock.calls[0][1].method).toBe('PATCH')
    expect(fetchImpl.mock.calls[0][1].body).toBe('{}')
  })
})
