import { describe, expect, it } from 'vitest'

import { WIZARD_STEPS, stepForField } from '../wizard-model'

const indexOf = (id: string) => WIZARD_STEPS.findIndex((step) => step.id === id)

describe('stepForField', () => {
  it('maps each request field to the step that owns it', () => {
    expect(stepForField('key')).toBe('basic')
    expect(stepForField('method')).toBe('basic')
    expect(stepForField('upstreamPath')).toBe('upstream')
    expect(stepForField('serviceId')).toBe('downstream')
    expect(stepForField('authenticationOptions.allowedScopes')).toBe('authentication')
    expect(stepForField('rateLimitOptions')).toBe('rate-limiting')
    expect(stepForField('qosOptions')).toBe('qos')
    expect(stepForField('cacheOptions')).toBe('advanced')
    expect(stepForField('loadBalancerOptions')).toBe('advanced')
  })

  it('resolves an indexed field to the step that owns the list', () => {
    // The API reports a bad target as downstreamTargets[1].
    expect(stepForField('downstreamTargets[1]')).toBe('downstream')
    expect(stepForField('downstreamTargets[0]')).toBe('downstream')
  })

  it('falls back to review for a whole-route problem', () => {
    expect(stepForField(null)).toBe('review')
  })

  it('falls back to review for a field it does not recognise', () => {
    // Better the review step, which shows everything, than a step that cannot
    // act on the problem.
    expect(stepForField('somethingNew')).toBe('review')
  })

  it('every mapped field resolves to a real step', () => {
    const fields = [
      'key',
      'method',
      'upstreamPath',
      'host',
      'serviceId',
      'downstreamTargets',
      'authenticationOptions',
      'rateLimitOptions',
      'qosOptions',
      'cacheOptions',
      'loadBalancerOptions',
      null,
    ]

    for (const field of fields) {
      expect(indexOf(stepForField(field))).toBeGreaterThanOrEqual(0)
    }
  })
})
