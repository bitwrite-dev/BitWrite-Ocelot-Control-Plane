import { describe, expect, it } from 'vitest'

import {
  WIZARD_STEPS,
  emptyRouteDraft,
  furthestReachableStep,
  toCreateRequest,
  type RouteDraft,
} from '../wizard-model'

const stepById = (id: string) => {
  const step = WIZARD_STEPS.find((candidate) => candidate.id === id)
  if (!step) throw new Error(`no step ${id}`)
  return step
}

const validDraft = (): RouteDraft => {
  const draft = emptyRouteDraft()
  draft.key = 'users-list'
  draft.upstreamPath = '/api/users'
  draft.serviceId = 'service-1'
  draft.downstreamTargets = [{ host: 'localhost', port: 5001, scheme: 'http', path: '/' }]
  return draft
}

describe('step validation', () => {
  it('requires a key and a method on the first step', () => {
    const errors = stepById('basic').validate(emptyRouteDraft())
    expect(errors).toContain('Key is required')
  })

  it('rejects an upstream path that does not start with a slash', () => {
    const draft = validDraft()
    draft.upstreamPath = 'api/users'
    expect(stepById('upstream').validate(draft)).toContain(
      'Upstream path must start with /',
    )
  })

  it('accepts placeholder paths, matching the API behaviour', () => {
    const draft = validDraft()
    draft.upstreamPath = '/api/{everything}'
    expect(stepById('upstream').validate(draft)).toEqual([])
  })

  it('requires a service and one complete downstream target', () => {
    const errors = stepById('downstream').validate(emptyRouteDraft())
    expect(errors).toContain('A service must be selected')
    expect(errors).toContain('Target 1: host is required')
    expect(errors).toContain('Target 1: Port is required')
  })

  it('rejects a port outside the allowed range', () => {
    const draft = validDraft()
    draft.downstreamTargets[0].port = 70000
    expect(stepById('downstream').validate(draft)).toContain(
      'Target 1: Port must be between 1 and 65535',
    )
  })

  it('treats authentication as optional', () => {
    expect(stepById('authentication').validate(emptyRouteDraft())).toEqual([])
  })

  it('validates the rate limit only when it is enabled', () => {
    const draft = validDraft()
    draft.rateLimit = { enabled: false, limit: '', period: 'Minute' }
    expect(stepById('rate-limiting').validate(draft)).toEqual([])

    draft.rateLimit = { enabled: true, limit: 0, period: 'Minute' }
    expect(stepById('rate-limiting').validate(draft)).toContain('Limit must be between 1 and 9007199254740991')
  })

  it('allows the circuit breaker timeout to be omitted while QoS is on', () => {
    const draft = validDraft()
    draft.qos = { enabled: true, timeoutSeconds: 30, circuitBreakerTimeoutSeconds: '' }
    expect(stepById('qos').validate(draft)).toEqual([])
  })

  it('rejects more downstream targets than the domain allows', () => {
    const draft = validDraft()
    draft.downstreamTargets = Array.from({ length: 11 }, (_, index) => ({
      host: `host-${index}`,
      port: 8080,
      scheme: 'http',
      path: '/',
    }))
    expect(stepById('downstream').validate(draft)).toContain(
      'A route cannot have more than 10 downstream targets',
    )
  })

  it('surfaces every earlier step error on the review step', () => {
    const errors = stepById('review').validate(emptyRouteDraft())
    expect(errors).toContain('Key is required')
    expect(errors).toContain('A service must be selected')
  })
})

describe('furthestReachableStep', () => {
  it('stops at the first invalid step', () => {
    expect(furthestReachableStep(emptyRouteDraft())).toBe(0)
  })

  it('advances past optional steps that are not filled in', () => {
    expect(furthestReachableStep(validDraft())).toBe(WIZARD_STEPS.length)
  })

  it('does not skip a step that has just been invalidated', () => {
    const draft = validDraft()
    draft.key = ''
    expect(furthestReachableStep(draft)).toBe(0)
  })
})

describe('toCreateRequest', () => {
  it('sends only the required fields for a minimal route', () => {
    const body = toCreateRequest(validDraft())
    expect(Object.keys(body).sort()).toEqual([
      'acceptAnyServerCertificate',
      'downstreamHttpVersion',
      'downstreamHttpVersionPolicy',
      'downstreamMethod',
      'downstreamPathTemplate',
      'downstreamTargets',
      'host',
      'key',
      'method',
      'priority',
      'routeId',
      'routeIsCaseSensitive',
      'serviceId',
      'timeoutSeconds',
      'upstreamPath',
    ])
    // Host is nullable, so it is always sent — as null rather than omitted.
    expect(body.host).toBeNull()
  })

  it('sends the transport defaults as null rather than omitting them', () => {
    // Each of these means something specific, so absence would be ambiguous:
    // a null verb keeps the upstream verb, and a null timeout means the
    // framework default rather than "no timeout", which is what 0 would mean.
    const body = toCreateRequest(validDraft())

    expect(body.downstreamMethod).toBeNull()
    expect(body.downstreamHttpVersion).toBeNull()
    expect(body.downstreamHttpVersionPolicy).toBeNull()
    expect(body.acceptAnyServerCertificate).toBe(false)
    expect(body.timeoutSeconds).toBeNull()
  })

  it('omits disabled option blocks rather than sending zero values', () => {
    const body = toCreateRequest(validDraft())
    expect(body).not.toHaveProperty('httpClientOptions')
    expect(body).not.toHaveProperty('delegatingHandlers')
    expect(body).not.toHaveProperty('rateLimitOptions')
    expect(body).not.toHaveProperty('qosOptions')
    expect(body).not.toHaveProperty('cacheOptions')
    expect(body).not.toHaveProperty('loadBalancerOptions')
    expect(body).not.toHaveProperty('authenticationOptions')
  })

  it('includes each option block once enabled', () => {
    const draft = validDraft()
    draft.allowedScopes = ['users.read']
    draft.rateLimit = { enabled: true, limit: 50, period: 'Hour' }
    draft.qos = { enabled: true, timeoutSeconds: 45, circuitBreakerTimeoutSeconds: 10 }
    draft.cache = { enabled: true, ttlSeconds: 60 }
    draft.loadBalancer = { enabled: true, algorithm: 'LeastConnection' }

    expect(toCreateRequest(draft)).toMatchObject({
      authenticationOptions: { allowedScopes: ['users.read'] },
      rateLimitOptions: { enableRateLimiting: true, period: 'Hour', limit: 50 },
      qosOptions: { timeoutSeconds: 45, circuitBreakerTimeoutSeconds: 10 },
      cacheOptions: { ttlSeconds: 60 },
      loadBalancerOptions: { algorithm: 'LeastConnection' },
    })
  })

  it('drops the circuit breaker timeout when it was left blank', () => {
    const draft = validDraft()
    draft.qos = { enabled: true, timeoutSeconds: 45, circuitBreakerTimeoutSeconds: '' }
    const body = toCreateRequest(draft)
    expect(body.qosOptions).toEqual({ timeoutSeconds: 45 })
  })

  it('trims text and converts ports to numbers', () => {
    const draft = validDraft()
    draft.key = '  users-list  '
    draft.upstreamPath = '  /api/users  '
    draft.host = '  api.example.com  '

    const body = toCreateRequest(draft)
    expect(body.key).toBe('users-list')
    expect(body.upstreamPath).toBe('/api/users')
    expect(body.host).toBe('api.example.com')
    expect(body.downstreamTargets[0].port).toBe(5001)
  })
})
