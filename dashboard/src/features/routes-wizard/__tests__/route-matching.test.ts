import { describe, expect, it } from 'vitest'

import { emptyRouteDraft, toCreateRequest } from '../wizard-model'

const draft = () => {
  const value = emptyRouteDraft()
  value.key = 'users-list'
  value.upstreamPath = '/api/users'
  value.serviceId = 'svc-users'
  value.downstreamTargets = [
    { host: 'localhost', port: 5001, scheme: 'http', path: '/' },
  ]
  return value
}

describe('route matching capabilities', () => {
  it('defaults to zero priority and case-insensitive matching', () => {
    // The Ocelot defaults, and what an untouched route should send.
    const value = draft()

    expect(value.priority).toBe(0)
    expect(value.routeIsCaseSensitive).toBe(false)

    const body = toCreateRequest(value)
    expect(body.priority).toBe(0)
    expect(body.routeIsCaseSensitive).toBe(false)
  })

  it('always sends both, because a route is built with them', () => {
    // Unlike the feature blocks, which are omitted when switched off: these
    // have no "absent" state, and omitting a priority would leave Ocelot to
    // order routes by file position.
    const body = toCreateRequest(draft())

    expect(body).toHaveProperty('priority')
    expect(body).toHaveProperty('routeIsCaseSensitive')
  })

  it('carries a priority and the case flag when set', () => {
    const value = draft()
    value.priority = 250
    value.routeIsCaseSensitive = true

    const body = toCreateRequest(value)

    expect(body.priority).toBe(250)
    expect(body.routeIsCaseSensitive).toBe(true)
  })

  it('sends a null host when unset, so upstream matching is unrestricted', () => {
    expect(toCreateRequest(draft()).host).toBeNull()
  })

  it('sends the host when one is set', () => {
    const value = draft()
    value.host = 'api.example.com'

    expect(toCreateRequest(value).host).toBe('api.example.com')
  })

  it('trims the host', () => {
    const value = draft()
    value.host = '  api.example.com  '

    expect(toCreateRequest(value).host).toBe('api.example.com')
  })
})
