import { describe, expect, it } from 'vitest'

import type { RouteResponse } from '@/api/types'

import type { TransportDraft } from '../wizard-model'
import type { RouteDraft } from '../wizard-model'

import {
  DOWNSTREAM_HTTP_VERSIONS,
  DOWNSTREAM_HTTP_VERSION_POLICIES,
  WIZARD_STEPS,
  draftFromRoute,
  emptyRouteDraft,
  emptyTransportDraft,
  toCreateRequest,
} from '../wizard-model'

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

const advancedErrors = (value: ReturnType<typeof draft>): string[] =>
  WIZARD_STEPS.find((step) => step.id === 'advanced')!.validate(value)

/** The transport block of a draft, which the field marks optional. */
const transportOf = (value: RouteDraft): TransportDraft => value.transport ?? emptyTransportDraft()

const response = (overrides: Partial<RouteResponse> = {}): RouteResponse =>
  ({
    id: 'r1',
    key: 'users-list',
    method: 'GET',
    upstreamPath: '/api/users',
    host: null,
    priority: 0,
    routeIsCaseSensitive: false,
          downstreamPathTemplate: null,

          headerTransformations: null,

    downstreamMethod: null,
    downstreamHttpVersion: null,
    downstreamHttpVersionPolicy: null,
    dangerousAcceptAnyServerCertificateValidator: false,
    delegatingHandlers: [],
    httpClientOptions: null,
    timeoutSeconds: null,
    serviceId: 'svc-users',
    isEnabled: true,
    downstreamTargets: [
      { host: 'localhost', port: 5001, scheme: 'http', path: '/' },
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

describe('downstream transport capabilities', () => {
  it('defaults to keeping the upstream verb and the framework version', () => {
    const value = draft()

    expect(transportOf(value).downstreamMethod).toBe('')
    expect(transportOf(value).downstreamTemplate).toBe('')
    expect(transportOf(value).downstreamHttpVersion).toBe('')
    expect(transportOf(value).downstreamHttpVersionPolicy).toBe('')
    expect(transportOf(value).acceptAnyServerCertificate).toBe(false)
    expect(transportOf(value).delegatingHandlers).toEqual([])
    expect(transportOf(value).timeoutSeconds).toBe('')

    const body = toCreateRequest(value)
    expect(body.downstreamMethod).toBeNull()
    expect(body.downstreamPathTemplate).toBeNull()
    expect(body.downstreamHttpVersion).toBeNull()
    expect(body.downstreamHttpVersionPolicy).toBeNull()
    expect(body.acceptAnyServerCertificate).toBe(false)
    expect(body.timeoutSeconds).toBeNull()
  })

  it('omits the blocks that are switched off rather than sending zeros', () => {
    // A zeroed HttpClientOptions would fail the API's own range validation, and
    // an empty handler list would register nothing.
    const body = toCreateRequest(draft())

    expect(body).not.toHaveProperty('httpClientOptions')
    expect(body).not.toHaveProperty('delegatingHandlers')
  })

  it('carries every transport setting when set', () => {
    const value = draft()
    value.transport = {
      downstreamMethod: 'POST',
      downstreamTemplate: '',
      downstreamHttpVersion: '2.0',
      downstreamHttpVersionPolicy: 'RequestVersionExact',
      acceptAnyServerCertificate: true,
      delegatingHandlers: ['First', 'Second'],
      httpClient: {
        enabled: true,
        allowAutoRedirect: true,
        maxConnectionsPerServer: '25',
        pooledConnectionLifetimeSeconds: '400',
        useCookieContainer: true,
        useProxy: true,
        useTracing: true,
      },
      timeoutSeconds: '45',
    }

    const body = toCreateRequest(value)
    expect(body.downstreamMethod).toBe('POST')
    expect(body.downstreamHttpVersion).toBe('2.0')
    expect(body.downstreamHttpVersionPolicy).toBe('RequestVersionExact')
    expect(body.acceptAnyServerCertificate).toBe(true)
    expect(body.delegatingHandlers).toEqual(['First', 'Second'])
    expect(body.httpClientOptions).toEqual({
      allowAutoRedirect: true,
      maxConnectionsPerServer: 25,
      pooledConnectionLifetimeSeconds: 400,
      useCookieContainer: true,
      useProxy: true,
      useTracing: true,
    })
    expect(body.timeoutSeconds).toBe(45)
  })

  it('leaves a blank numeric client setting out instead of sending zero', () => {
    const value = draft()
    transportOf(value).httpClient = {
      ...transportOf(value).httpClient,
      enabled: true,
      allowAutoRedirect: true,
    }

    const body = toCreateRequest(value)
    // The server default is int.MaxValue, so a blank field has to be absent.
    expect(body.httpClientOptions).not.toHaveProperty('maxConnectionsPerServer')
    expect(body.httpClientOptions).not.toHaveProperty('pooledConnectionLifetimeSeconds')
  })

  it('drops blank handler lines rather than sending empty names', () => {
    const value = draft()
    transportOf(value).delegatingHandlers = ['Handler', '', '   ']

    expect(toCreateRequest(value).delegatingHandlers).toEqual(['Handler'])
  })

  it('offers only the versions Ocelot accepts', () => {
    expect([...DOWNSTREAM_HTTP_VERSIONS]).toEqual(['1.0', '1.1', '2.0'])
    expect([...DOWNSTREAM_HTTP_VERSION_POLICIES]).toEqual([
      'RequestVersionExact',
      'RequestVersionOrHigher',
      'RequestVersionOrLower',
    ])
  })

  it('rejects a version outside the accepted set', () => {
    const value = draft()
    transportOf(value).downstreamHttpVersion = '3.0'

    expect(advancedErrors(value).join(' ')).toContain('Downstream HTTP version')
  })

  it('rejects an unknown policy', () => {
    const value = draft()
    transportOf(value).downstreamHttpVersion = '2.0'
    transportOf(value).downstreamHttpVersionPolicy = 'RequestVersionMaybe'

    expect(advancedErrors(value).join(' ')).toContain('HTTP version policy')
  })

  it('rejects a policy with no version to apply it to', () => {
    // It would read as configured while changing nothing.
    const value = draft()
    transportOf(value).downstreamHttpVersionPolicy = 'RequestVersionExact'

    expect(advancedErrors(value).join(' ')).toContain('needs a version')
  })

  it('rejects a duplicated delegating handler', () => {
    // Ocelot would register the same handler twice.
    const value = draft()
    transportOf(value).delegatingHandlers = ['Handler', 'handler']

    expect(advancedErrors(value).join(' ')).toContain('listed twice')
  })

  it('rejects a blank delegating handler name', () => {
    const value = draft()
    transportOf(value).delegatingHandlers = ['Handler', '   ']

    expect(advancedErrors(value).join(' ')).toContain('cannot be empty')
  })

  it.each(['0', '-5'])('rejects a non-positive timeout of %s', (seconds) => {
    // Ocelot reads zero or less as "no timeout".
    const value = draft()
    transportOf(value).timeoutSeconds = seconds

    expect(advancedErrors(value).join(' ')).toContain('Timeout')
  })

  it('rejects a non-positive client connection limit', () => {
    const value = draft()
    transportOf(value).httpClient = {
      ...transportOf(value).httpClient,
      enabled: true,
      maxConnectionsPerServer: '0',
    }

    expect(advancedErrors(value).join(' ')).toContain('Max connections per server')
  })

  it('rejects a non-positive pooled connection lifetime', () => {
    const value = draft()
    transportOf(value).httpClient = {
      ...transportOf(value).httpClient,
      enabled: true,
      pooledConnectionLifetimeSeconds: '0',
    }

    expect(advancedErrors(value).join(' ')).toContain('Pooled connection lifetime')
  })

  it('accepts an untouched transport block', () => {
    expect(advancedErrors(draft())).toEqual([])
  })
})

describe('round-tripping transport settings', () => {
  it('fills the draft from what the API reports', () => {
    const value = draftFromRoute(
      response({
        downstreamMethod: 'POST',
        downstreamHttpVersion: '2.0',
        downstreamHttpVersionPolicy: 'RequestVersionOrHigher',
        dangerousAcceptAnyServerCertificateValidator: true,
        delegatingHandlers: ['First', 'Second'],
        httpClientOptions: {
          allowAutoRedirect: true,
          maxConnectionsPerServer: 25,
          pooledConnectionLifetimeSeconds: 400,
          useCookieContainer: true,
          useProxy: false,
          useTracing: true,
        },
        timeoutSeconds: 45,
      }),
    )

    expect(transportOf(value).downstreamMethod).toBe('POST')
    expect(transportOf(value).downstreamHttpVersion).toBe('2.0')
    expect(transportOf(value).downstreamHttpVersionPolicy).toBe('RequestVersionOrHigher')
    expect(transportOf(value).acceptAnyServerCertificate).toBe(true)
    expect(transportOf(value).delegatingHandlers).toEqual(['First', 'Second'])
    expect(transportOf(value).httpClient.enabled).toBe(true)
    expect(transportOf(value).httpClient.maxConnectionsPerServer).toBe('25')
    expect(transportOf(value).httpClient.pooledConnectionLifetimeSeconds).toBe('400')
    expect(transportOf(value).timeoutSeconds).toBe('45')
  })

  it('shows an unset connection limit as blank rather than as a number nobody chose', () => {
    // The server resolves an absent limit to int.MaxValue.
    const value = draftFromRoute(
      response({
        httpClientOptions: {
          allowAutoRedirect: false,
          maxConnectionsPerServer: Number.MAX_SAFE_INTEGER,
          pooledConnectionLifetimeSeconds: Number.MAX_SAFE_INTEGER,
          useCookieContainer: false,
          useProxy: false,
          useTracing: false,
        },
      }),
    )

    expect(transportOf(value).httpClient.maxConnectionsPerServer).toBe('')
    expect(transportOf(value).httpClient.pooledConnectionLifetimeSeconds).toBe('')
  })

  it('leaves the client block off when the route has none', () => {
    const value = draftFromRoute(response())

    expect(transportOf(value).httpClient.enabled).toBe(false)
    expect(transportOf(value).httpClient.maxConnectionsPerServer).toBe('')
  })

  it('handles a response from before these fields existed', () => {
    // A route stored by an earlier build reports nothing for them.
    const legacy = response()
    delete (legacy as Partial<RouteResponse>).downstreamMethod
    delete (legacy as Partial<RouteResponse>).delegatingHandlers
    delete (legacy as Partial<RouteResponse>).timeoutSeconds
    delete (legacy as Partial<RouteResponse>).httpClientOptions

    const value = draftFromRoute(legacy)

    expect(transportOf(value).downstreamMethod).toBe('')
    expect(transportOf(value).delegatingHandlers).toEqual([])
    expect(transportOf(value).timeoutSeconds).toBe('')
    expect(transportOf(value).httpClient.enabled).toBe(false)
  })

  it('survives a save-and-reload without losing anything', () => {
    const original = draft()
    original.routeId = 'r1'
    original.transport = {
      downstreamMethod: 'PUT',
      downstreamTemplate: '',
      downstreamHttpVersion: '1.1',
      downstreamHttpVersionPolicy: 'RequestVersionOrLower',
      acceptAnyServerCertificate: true,
      delegatingHandlers: ['Handler'],
      httpClient: {
        enabled: true,
        allowAutoRedirect: true,
        maxConnectionsPerServer: '10',
        pooledConnectionLifetimeSeconds: '300',
        useCookieContainer: true,
        useProxy: true,
        useTracing: false,
      },
      timeoutSeconds: '120',
    }

    // What the API would echo back after the create.
    const reloaded = draftFromRoute(
      response({
        downstreamMethod: 'PUT',
        downstreamHttpVersion: '1.1',
        downstreamHttpVersionPolicy: 'RequestVersionOrLower',
        dangerousAcceptAnyServerCertificateValidator: true,
        delegatingHandlers: ['Handler'],
        httpClientOptions: {
          allowAutoRedirect: true,
          maxConnectionsPerServer: 10,
          pooledConnectionLifetimeSeconds: 300,
          useCookieContainer: true,
          useProxy: true,
          useTracing: false,
        },
        timeoutSeconds: 120,
      }),
    )

    expect(toCreateRequest(reloaded)).toEqual(toCreateRequest(original))
  })
})
