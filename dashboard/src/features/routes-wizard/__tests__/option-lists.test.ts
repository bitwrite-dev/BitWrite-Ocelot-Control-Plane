import { describe, expect, it } from 'vitest'

import { loadBalancerOptions, periodOptions } from '../wizard-model'

describe('option lists that keep unknown stored values', () => {
  it('offers a known algorithm unchanged', () => {
    expect(loadBalancerOptions('RoundRobin')).toEqual([
      'RoundRobin',
      'LeastConnection',
      'Random',
      'First',
    ])
  })

  it('keeps an algorithm this build does not know about', () => {
    // Otherwise the select would show nothing and saving would silently
    // rewrite the route to the first option.
    const options = loadBalancerOptions('WeightedRoundRobin')

    expect(options[0]).toBe('WeightedRoundRobin')
    expect(options).toHaveLength(5)
  })

  it('keeps an unknown period', () => {
    const options = periodOptions('Minute-ish')

    expect(options[0]).toBe('Minute-ish')
    expect(options).toContain('Minute')
  })

  it('falls back to the known list when nothing is stored', () => {
    expect(loadBalancerOptions(undefined)[0]).toBe('RoundRobin')
    expect(periodOptions(undefined)[0]).toBe('Second')
  })
})
