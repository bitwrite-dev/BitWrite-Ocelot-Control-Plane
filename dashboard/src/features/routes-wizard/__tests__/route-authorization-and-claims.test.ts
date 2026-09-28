import { describe, expect, it } from 'vitest'

import type { RouteResponse } from '@/api/types'

import {
  WIZARD_STEPS,
  draftFromRoute,
  emptyRouteDraft,
  emptyTransportDraft,
  parseRequirements,
  toCreateRequest,
} from '../wizard-model'

/**
 * The last two wizard steps the spec lists.
 *
 * The API, the domain and the emitter have held both since #466; only the
 * wizard could not reach them, so they were listed as "not yet supported"
 * instead of offered as fields. Query transformations are the exception and
 * stay unreachable: Ocelot 18 has no block for them, and the emitter refuses
 * them on purpose.
 */

const draft = () => {
  const value = emptyRouteDraft()
  value.key = 'orders-internal'
  value.method = 'GET'
  value.upstreamPath = '/api/orders'
  value.serviceId = 'svc-orders'
  value.downstreamTargets = [
    { host: 'orders.internal.example.com', port: 5001, scheme: 'https', path: '/' },
  ]
  return value
}

const advancedErrors = (value: ReturnType<typeof draft>): string[] =>
  WIZARD_STEPS.find((step) => step.id === 'advanced')!.validate(value)

/** The block shapes are optional, so these narrow what the assertions read. */
const claimRulesOf = (block: unknown): unknown =>
  (block as { add?: unknown } | undefined)?.add
const policiesOf = (block: unknown): unknown =>
  (block as { policies?: unknown } | undefined)?.policies

const response = (overrides: Partial<RouteResponse> = {}): RouteResponse =>
  ({
    id: 'r1',
    key: 'orders-internal',
    method: 'GET',
    upstreamPath: '/api/orders',
    host: null,
    priority: 0,
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
    claimTransformations: null,
    queryTransformations: null,
    authorizationOptions: null,
    serviceId: 'svc-orders',
    isEnabled: true,
    downstreamTargets: [
      { host: 'orders.internal.example.com', port: 5001, scheme: 'https', path: '/' },
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

describe('claims added downstream', () => {
  it('sends no block when nothing is configured', () => {
    expect(toCreateRequest(draft())).not.toHaveProperty('claimTransformations')
  })

  it('carries the add half', () => {
    const value = draft()
    value.claims = { add: 'customerId: {Claims[sub]}\ntenant: {Claims[tenant]}', transform: '' }

    expect(toCreateRequest(value).claimTransformations).toEqual({
      add: [
        { key: 'customerId', value: '{Claims[sub]}' },
        { key: 'tenant', value: '{Claims[tenant]}' },
      ],
    })
  })

  it('keeps a claim placeholder intact', () => {
    // A value filled per request from the caller's token. Stripping the braces
    // would send a literal instead.
    const value = draft()
    value.claims = { add: 'sub: {Claims[sub]}', transform: '' }

    expect(claimRulesOf(toCreateRequest(value).claimTransformations)).toEqual([
      { key: 'sub', value: '{Claims[sub]}' },
    ])
  })

  it('refuses a claim transform rather than dropping it silently', () => {
    // Ocelot 18 has only AddClaimsToRequest, so the transform half has no
    // block. Showing a rule that never reaches the gateway is worse than saying so.
    const value = draft()
    value.claims = { add: 'sub: {Claims[sub]}', transform: 'role: admin' }

    expect(advancedErrors(value).join(' ')).toContain('no block for rewriting a claim')
  })

  it('round-trips', () => {
    const value = draftFromRoute(
      response({
        claimTransformations: {
          add: [{ key: 'customerId', value: '{Claims[sub]}' }],
          transform: null,
        },
      }),
    )

    expect(value.claims?.add).toBe('customerId: {Claims[sub]}')
  })
})

describe('authorization', () => {
  it('sends no block when nothing is configured', () => {
    expect(toCreateRequest(draft())).not.toHaveProperty('authorizationOptions')
  })

  it('carries policies and claim requirements', () => {
    const value = draft()
    value.authorization = {
      policies: ['orders:read', 'orders:write'],
      requirements: { role: 'admin,staff', region: 'eu-west-1' },
    }

    expect(toCreateRequest(value).authorizationOptions).toEqual({
      policies: ['orders:read', 'orders:write'],
      requirements: { role: 'admin,staff', region: 'eu-west-1' },
    })
  })

  it('sends the block when only one half is set', () => {
    const onlyPolicies = draft()
    onlyPolicies.authorization = { policies: ['orders:read'], requirements: {} }
    expect(toCreateRequest(onlyPolicies).authorizationOptions).toEqual({
      policies: ['orders:read'],
      requirements: {},
    })

    const onlyClaims = draft()
    onlyClaims.authorization = { policies: [], requirements: { role: 'admin' } }
    expect(toCreateRequest(onlyClaims).authorizationOptions).toEqual({
      policies: [],
      requirements: { role: 'admin' },
    })
  })

  it('drops blank policy lines', () => {
    const value = draft()
    value.authorization = { policies: ['orders:read', '', '   '], requirements: {} }

    expect(policiesOf(toCreateRequest(value).authorizationOptions)).toEqual(['orders:read'])
  })

  it('rejects a duplicated policy regardless of case', () => {
    // Ocelot would register the same policy twice.
    const value = draft()
    value.authorization = { policies: ['orders:read', 'ORDERS:READ'], requirements: {} }

    expect(advancedErrors(value).join(' ')).toContain('listed twice')
  })

  it('rejects a blank policy name', () => {
    const value = draft()
    value.authorization = { policies: ['  '], requirements: {} }

    expect(advancedErrors(value).join(' ')).toContain('policy name cannot be empty')
  })

  it('rejects a claim requirement with no values', () => {
    // It matches nothing, which reads as a rule that is set.
    const value = draft()
    value.authorization = { policies: [], requirements: { role: '  ' } }

    expect(advancedErrors(value).join(' ')).toContain('at least one required value')
  })

  it('rejects a blank claim name', () => {
    const value = draft()
    value.authorization = { policies: [], requirements: { '  ': 'admin' } }

    expect(advancedErrors(value).join(' ')).toContain('claim name cannot be empty')
  })

  it('accepts an untouched authorization block', () => {
    expect(advancedErrors(draft())).toEqual([])
  })

  it('round-trips both halves', () => {
    const value = draftFromRoute(
      response({
        authorizationOptions: {
          policies: ['orders:read', 'orders:write'],
          scopes: null,
          requirements: { role: 'admin,staff' },
        },
      }),
    )

    expect(value.authorization?.policies).toEqual(['orders:read', 'orders:write'])
    expect(value.authorization?.requirements).toEqual({ role: 'admin,staff' })
  })

  it('copes with a response from before the field existed', () => {
    const legacy = response()
    delete (legacy as Partial<RouteResponse>).authorizationOptions
    delete (legacy as Partial<RouteResponse>).claimTransformations

    const value = draftFromRoute(legacy)

    expect(value.authorization?.policies).toEqual([])
    expect(value.claims?.add).toBe('')
    expect(advancedErrors(value)).toEqual([])
  })

  it('survives a save and reload', () => {
    const original = draft()
    original.authorization = {
      policies: ['orders:read'],
      requirements: { region: 'eu-west-1' },
    }
    original.claims = { add: 'sub: {Claims[sub]}', transform: '' }

    const reloaded = draftFromRoute(
      response({
        authorizationOptions: {
          policies: ['orders:read'],
          scopes: null,
          requirements: { region: 'eu-west-1' },
        },
        claimTransformations: { add: [{ key: 'sub', value: '{Claims[sub]}' }], transform: null },
      }),
    )

    expect(toCreateRequest(reloaded)).toEqual(toCreateRequest(original))
  })
})

describe('parsing the requirements block', () => {
  it('reads claim: value pairs', () => {
    expect(parseRequirements('role: admin\nregion: eu-west-1')).toEqual({
      role: 'admin',
      region: 'eu-west-1',
    })
  })

  it('keeps a comma-separated list intact', () => {
    expect(parseRequirements('role: admin,staff')).toEqual({ role: 'admin,staff' })
  })

  it('keeps colons inside a value', () => {
    expect(parseRequirements('url: https://a.example.com:8443/x')).toEqual({
      url: 'https://a.example.com:8443/x',
    })
  })

  it('ignores blank lines and a line with no separator', () => {
    expect(parseRequirements('\nrole: admin\nnonsense\n')).toEqual({ role: 'admin' })
  })

  it('lets a later line for the same claim win', () => {
    // The same as a duplicate key in a JSON object.
    expect(parseRequirements('role: admin\nrole: staff')).toEqual({ role: 'staff' })
  })

  it('ignores a line with no claim name', () => {
    expect(parseRequirements(': admin')).toEqual({})
  })
})

describe('the transport block is still complete', () => {
  it('keeps an absent transport block from failing validation', () => {
    // The wizard is the only caller that guarantees the block, and a partial
    // draft is still allowed to type-check.
    const value = { ...draft(), transport: undefined }

    expect(advancedErrors(value)).toEqual([])
    expect(toCreateRequest(value).downstreamMethod).toBeNull()
    expect(emptyTransportDraft().downstreamMethod).toBe('')
  })
})
