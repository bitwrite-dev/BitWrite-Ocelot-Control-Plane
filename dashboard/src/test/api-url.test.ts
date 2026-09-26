import { describe, expect, it, vi } from 'vitest'

import { getApiBaseUrl } from '@/api/config'
import { createHttpClient } from '@/api/http'
import { createResources } from '@/api/resources'

/**
 * These tests deliberately use the **real** same-origin base (empty string),
 * not a fake origin like `http://api.test`.
 *
 * An earlier suite set `baseUrl: 'http://api.test'` everywhere, which has no
 * path prefix and therefore hid a double-prefix bug: the base was `/api` while
 * every resource path already began with `/api/v1`, producing
 * `/api/api/v1/routes` and a 404 on every call. These use the default so that
 * class of mistake cannot come back.
 */
function sameOriginClient() {
  const fetchImpl = vi.fn().mockImplementation(async () => new Response('{}', { status: 200 }))
  const http = createHttpClient(
    { getAccessToken: () => null },
    { fetchImpl: fetchImpl as unknown as typeof fetch },
  )
  return { fetchImpl, http }
}

describe('URL construction with the real same-origin base', () => {
  it('defaults the base URL to empty, not a path prefix', () => {
    // A prefix here is what produced /api/api/v1/... in production.
    expect(getApiBaseUrl()).toBe('')
  })

  it('requests the plain API path, with no doubled prefix', async () => {
    const { fetchImpl, http } = sameOriginClient()

    await http.get('/api/v1/routes')

    expect(fetchImpl.mock.calls[0][0]).toBe('/api/v1/routes')
  })

  it('never produces a double slash for the same-origin case', async () => {
    const { fetchImpl, http } = sameOriginClient()

    await http.get('/api/v1/snapshots')

    const url = String(fetchImpl.mock.calls[0][0])
    expect(url.startsWith('//')).toBe(false)
    expect(url).toBe('/api/v1/snapshots')
  })

  it('builds every resource path without prefixing it twice', async () => {
    const { fetchImpl, http } = sameOriginClient()
    const resources = createResources(http)

    await resources.routes.list({ pageSize: 1 })
    await resources.services.list({ pageSize: 1 })
    await resources.snapshots.list({ pageSize: 20 })
    await resources.publications.current()
    await resources.runtime.gateways()

    const urls = fetchImpl.mock.calls.map((c) => String(c[0]))
    expect(urls).toEqual([
      '/api/v1/routes?pageSize=1',
      '/api/v1/services?pageSize=1',
      '/api/v1/snapshots?pageSize=20',
      '/api/v1/publications/current',
      '/api/v1/runtime/gateways',
    ])

    for (const url of urls) {
      expect(url).not.toContain('/api/api/')
    }
  })

  it('preserves the query string alongside a relative path', async () => {
    const { fetchImpl, http } = sameOriginClient()

    await http.get('/api/v1/routes', { query: { page: 2, pageSize: 50, search: 'api' } })

    expect(fetchImpl.mock.calls[0][0]).toBe('/api/v1/routes?page=2&pageSize=50&search=api')
  })
})

describe('URL construction with an absolute origin', () => {
  it('prefixes the origin without touching the API path', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => new Response('{}', { status: 200 }))
    const http = createHttpClient(
      { getAccessToken: () => null },
      { baseUrl: 'https://api.example.com', fetchImpl: fetchImpl as unknown as typeof fetch },
    )

    await http.get('/api/v1/routes')

    expect(fetchImpl.mock.calls[0][0]).toBe('https://api.example.com/api/v1/routes')
  })

  it('tolerates a trailing slash on the configured origin', async () => {
    const fetchImpl = vi.fn().mockImplementation(async () => new Response('{}', { status: 200 }))
    const http = createHttpClient(
      { getAccessToken: () => null },
      { baseUrl: 'https://api.example.com/', fetchImpl: fetchImpl as unknown as typeof fetch },
    )

    await http.get('/api/v1/routes')

    expect(fetchImpl.mock.calls[0][0]).toBe('https://api.example.com/api/v1/routes')
  })
})
