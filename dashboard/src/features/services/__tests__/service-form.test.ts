import { describe, expect, it } from 'vitest'

import { emptyServiceDraft, serviceDraftFrom, toServiceRequest } from '../queries'
import { validateServiceDraft } from '../service-form'

const validDraft = () => {
  const draft = emptyServiceDraft()
  draft.name = 'users-api'
  draft.downstreamTargets[0] = { host: 'localhost', port: 5001, weight: 1 }
  return draft
}

describe('validateServiceDraft', () => {
  it('accepts a service with one complete endpoint', () => {
    expect(validateServiceDraft(validDraft())).toEqual({})
  })

  it('requires a name', () => {
    const draft = validDraft()
    draft.name = '   '
    expect(validateServiceDraft(draft).name).toEqual(['Name is required'])
  })

  it('rejects a name over the API limit', () => {
    const draft = validDraft()
    draft.name = 'a'.repeat(201)
    expect(validateServiceDraft(draft).name).toEqual(['Name must be 200 characters or fewer'])
  })

  it('rejects a description over the API limit', () => {
    const draft = validDraft()
    draft.description = 'a'.repeat(1001)
    expect(validateServiceDraft(draft).description).toEqual([
      'Description must be 1000 characters or fewer',
    ])
  })

  it('accepts a null description', () => {
    const draft = validDraft()
    draft.description = null
    expect(validateServiceDraft(draft).description).toBeUndefined()
  })

  it('requires a host and port on every endpoint', () => {
    const errors = validateServiceDraft(emptyServiceDraft())
    expect(errors['endpoints.0.host']).toEqual(['Host is required'])
    expect(errors['endpoints.0.port']).toEqual(['Port is required'])
  })

  it('rejects a port outside the allowed range', () => {
    const draft = validDraft()
    draft.downstreamTargets[0].port = 70000
    expect(validateServiceDraft(draft)['endpoints.0.port']).toEqual([
      'Port must be between 1 and 65535',
    ])
  })

  it('rejects a duplicate endpoint, which the aggregate would refuse', () => {
    const draft = validDraft()
    draft.downstreamTargets.push({ host: 'localhost', port: 5001, weight: 1 })
    expect(validateServiceDraft(draft)['endpoints.1.host']).toEqual([
      'This endpoint is already listed',
    ])
  })

  it('treats a duplicate host case-insensitively, as the domain does', () => {
    const draft = validDraft()
    draft.downstreamTargets.push({ host: 'LOCALHOST', port: 5001, weight: 1 })
    expect(validateServiceDraft(draft)['endpoints.1.host']).toBeDefined()
  })

  it('allows several distinct endpoints', () => {
    const draft = validDraft()
    draft.downstreamTargets.push({ host: 'other.internal', port: 5001, weight: 1 })
    expect(validateServiceDraft(draft)).toEqual({})
  })

  it('requires at least one endpoint', () => {
    const draft = validDraft()
    draft.downstreamTargets = []
    expect(validateServiceDraft(draft).downstreamTargets).toEqual([
      'A service needs at least one endpoint',
    ])
  })

  it('reports every bad endpoint, not just the first', () => {
    const draft = validDraft()
    draft.downstreamTargets.push({ host: '', port: '', weight: '' })
    const errors = validateServiceDraft(draft)
    expect(errors['endpoints.1.host']).toBeDefined()
    expect(errors['endpoints.1.port']).toBeDefined()
  })
})

describe('serviceDraftFrom', () => {
  it('reads a stored service into the form', () => {
    const draft = serviceDraftFrom({
      id: 'svc-1',
      name: 'users-api',
      description: 'The users backend',
      downstreamTargets: [
        { host: 'localhost', port: 5001, weight: 3, isActive: true },
        { host: 'other', port: 5002, weight: 1, isActive: true },
      ],
      createdAt: '',
      updatedAt: '',
    })

    expect(draft.name).toBe('users-api')
    expect(draft.description).toBe('The users backend')
    expect(draft.downstreamTargets).toHaveLength(2)
  })

  it('handles a service with no description', () => {
    const draft = serviceDraftFrom({
      id: 'svc-1',
      name: 'n',
      description: null,
      downstreamTargets: [],
      createdAt: '',
      updatedAt: '',
    })

    expect(draft.description).toBeNull()
  })
})

describe('toServiceRequest', () => {
  it('trims the name and converts the port to a number', () => {
    const draft = validDraft()
    draft.name = '  users-api  '
    draft.downstreamTargets[0].host = '  localhost  '

    const body = toServiceRequest(draft)

    expect(body.name).toBe('users-api')
    expect(body.downstreamTargets[0]).toEqual({
      host: 'localhost',
      port: 5001,
      weight: 1,
    })
  })

  it('sends the weight the operator set', () => {
    // #474: the API accepted no weight at all, and the update handler matched
    // endpoints on host and port alone, so a changed weight was echoed back and
    // dropped. It is stored now, so the form has to actually send it.
    const draft = validDraft()
    draft.downstreamTargets[0].weight = 5

    const body = toServiceRequest(draft)

    expect(body.downstreamTargets[0].weight).toBe(5)
  })

  it('carries no scheme or path, because the domain stores neither', () => {
    // They used to be in the request, accepted, discarded, and echoed back as
    // "http" and "/" — so a typed path came back as "/" with nothing to say
    // whether it had been kept.
    const endpoint = toServiceRequest(validDraft()).downstreamTargets[0]

    expect(Object.keys(endpoint).sort()).toEqual(['host', 'port', 'weight'])
  })

  it('keeps a null description as null rather than an empty string', () => {
    expect(toServiceRequest(validDraft()).description).toBeNull()
  })
})
