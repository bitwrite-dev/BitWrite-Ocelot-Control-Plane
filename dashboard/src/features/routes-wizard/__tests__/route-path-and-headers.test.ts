import { describe, expect, it } from 'vitest'

import type { RouteResponse } from '@/api/types'

import { WIZARD_STEPS, draftFromRoute, emptyRouteDraft, toCreateRequest } from '../wizard-model'

/**
 * The two capabilities the advanced step gained after the transport work: the
 * downstream path template, and header transformations.
 *
 * Both were unreachable from the wizard before, which meant a route with either
 * had to be written as raw JSON.
 */

const draft = () => {
  const value = emptyRouteDraft()
  value.key = 'header-transformation'
  value.method = 'GET'
  value.upstreamPath = '/api/orders/{orderId}'
  value.priority = 20
  value.serviceId = 'svc-orders'
  value.downstreamTargets = [
    { host: 'orders.internal.example.com', port: 443, scheme: 'https', path: '/' },
  ]
  return value
}

const advancedErrors = (value: ReturnType<typeof draft>): string[] =>
  WIZARD_STEPS.find((step) => step.id === 'advanced')!.validate(value)

const transportOf = (value: ReturnType<typeof draft>) =>
  value.transport ?? emptyRouteDraft().transport!

const response = (overrides: Partial<RouteResponse> = {}): RouteResponse =>
  ({
    id: 'r1',
    key: 'header-transformation',
    method: 'GET',
    upstreamPath: '/api/orders/{orderId}',
    host: null,
    priority: 20,
    routeIsCaseSensitive: false,
    downstreamMethod: null,
    downstreamPathTemplate: null,
    downstreamHttpVersion: null,
    downstreamHttpVersionPolicy: null,
    dangerousAcceptAnyServerCertificateValidator: false,
    delegatingHandlers: [],
    httpClientOptions: null,
    timeoutSeconds: null,
    headerTransformations: null,
    serviceId: 'svc-orders',
    isEnabled: true,
    downstreamTargets: [
      { host: 'orders.internal.example.com', port: 443, scheme: 'https', path: '/' },
    ],
    authenticationOptions: null,
    rateLimitOptions: null,
    qoSOptions: null,
    cacheOptions: null,
    loadBalancerOptions: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }) as RouteResponse

describe('the downstream path template', () => {
  it('is omitted when the path is forwarded unchanged', () => {
    // Null is Ocelot's /{everything}, and it is the default, so it is absent
    // rather than sent as a literal.
    const body = toCreateRequest(draft())

    expect(transportOf(draft()).downstreamTemplate).toBe('')
    expect(body.downstreamPathTemplate).toBeNull()
  })

  it('is carried when set', () => {
    const value = draft()
    value.transport = { ...transportOf(value), downstreamTemplate: '/internal/orders/{orderId}' }

    expect(toCreateRequest(value).downstreamPathTemplate).toBe('/internal/orders/{orderId}')
  })

  it('trims a pasted value', () => {
    const value = draft()
    value.transport = { ...transportOf(value), downstreamTemplate: '  /internal  ' }

    expect(toCreateRequest(value).downstreamPathTemplate).toBe('/internal')
  })

  it('comes back from the API on a saved route', () => {
    const value = draftFromRoute(
      response({ downstreamPathTemplate: '/internal/orders/{orderId}' }),
    )

    expect(transportOf(value).downstreamTemplate).toBe('/internal/orders/{orderId}')
  })
})

describe('header transformations', () => {
  it('sends no block at all when nothing is configured', () => {
    // An empty list is not the same as an absent block: the API reads the first
    // as "the operator cleared this" and the second as "leave it".
    expect(toCreateRequest(draft())).not.toHaveProperty('headerTransformations')
  })

  it('keeps a placeholder value intact', () => {
    // {UpstreamHost} is filled per request. Treating it as a literal, or
    // stripping it, would send an empty header.
    const value = draft()
    value.headers = {
      transform: 'X-Original-Host: {UpstreamHost}\nX-Forwarded-For: {RemoteIpAddress}',
      add: '',
    }

    expect(toCreateRequest(value).headerTransformations).toEqual({
      add: [],
      transform: [
        { key: 'X-Original-Host', value: '{UpstreamHost}' },
        { key: 'X-Forwarded-For', value: '{RemoteIpAddress}' },
      ],
    })
  })

  it('sends both halves in one block', () => {
    const value = draft()
    value.headers = {
      transform: 'X-Original-Host: {UpstreamHost}',
      add: 'X-Downstream-Service: orders',
    }

    expect(toCreateRequest(value).headerTransformations).toEqual({
      add: [{ key: 'X-Downstream-Service', value: 'orders' }],
      transform: [{ key: 'X-Original-Host', value: '{UpstreamHost}' }],
    })
  })

  it('keeps colons inside a value', () => {
    // A URL has several, so only the first one separates.
    const value = draft()
    value.headers = {
      add: 'Location: https://orders.internal.example.com/, {BaseUrl}',
      transform: '',
    }

    expect(toCreateRequest(value).headerTransformations?.add).toEqual([
      { key: 'Location', value: 'https://orders.internal.example.com/, {BaseUrl}' },
    ])
  })

  it('accepts a rule with no value, which empties the header', () => {
    const value = draft()
    value.headers = { add: 'X-Debug', transform: '' }

    expect(toCreateRequest(value).headerTransformations?.add).toEqual([
      { key: 'X-Debug', value: '' },
    ])
  })

  it('ignores blank lines and leading indentation', () => {
    const value = draft()
    value.headers = { add: '  X-Trace: 1  \n\n\t\n  X-Request-Id: {RequestId}  ', transform: '' }

    expect(toCreateRequest(value).headerTransformations?.add).toEqual([
      { key: 'X-Trace', value: '1' },
      { key: 'X-Request-Id', value: '{RequestId}' },
    ])
  })

  it('rejects a rule with no name at all', () => {
    const value = draft()
    value.headers = { add: ': orphan', transform: '' }

    expect(toCreateRequest(value)).not.toHaveProperty('headerTransformations')
  })

  it('rejects a duplicated header in the same block', () => {
    // The later rule would win silently.
    const value = draft()
    value.headers = { add: 'X-Trace: 1\nx-trace: 2', transform: '' }

    expect(advancedErrors(value).join(' ')).toContain('set twice')
  })

  it('rejects a header name with a space in it', () => {
    const value = draft()
    value.headers = { add: 'X Trace: 1', transform: '' }

    expect(advancedErrors(value).join(' ')).toContain('not a valid header name')
  })

  it('accepts a valid untouched block', () => {
    expect(advancedErrors(draft())).toEqual([])
  })

  it('round-trips both halves', () => {
    const value = draftFromRoute(
      response({
        headerTransformations: {
          add: [{ key: 'X-Downstream-Service', value: 'orders' }],
          transform: [
            { key: 'X-Original-Host', value: '{UpstreamHost}' },
            { key: 'X-Forwarded-For', value: '{RemoteIpAddress}' },
          ],
        },
      }),
    )

    expect(value.headers?.add).toBe('X-Downstream-Service: orders')
    expect(value.headers?.transform).toBe(
      'X-Original-Host: {UpstreamHost}\nX-Forwarded-For: {RemoteIpAddress}',
    )
  })

  it('rebuilds the same request after a save and reload', () => {
    const original = draft()
    original.routeId = 'r1'
    original.headers = {
      add: 'X-Downstream-Service: orders',
      transform: 'X-Original-Host: {UpstreamHost}',
    }
    original.transport = {
      ...transportOf(original),
      downstreamTemplate: '/internal/orders/{orderId}',
    }

    const reloaded = draftFromRoute(
      response({
        downstreamPathTemplate: '/internal/orders/{orderId}',
        headerTransformations: {
          add: [{ key: 'X-Downstream-Service', value: 'orders' }],
          transform: [{ key: 'X-Original-Host', value: '{UpstreamHost}' }],
        },
      }),
    )

    expect(toCreateRequest(reloaded)).toEqual(toCreateRequest(original))
  })
})
