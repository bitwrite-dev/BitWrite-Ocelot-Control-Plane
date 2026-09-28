import { describe, expect, it } from 'vitest'

import type { GlobalConfigurationResponse } from '@/api'

import {
  configurationErrors,
  draftFromConfiguration,
  emptyGlobalConfigurationDraft,
  isDirty,
  parsePairs,
  toUpdateRequest,
  type GlobalConfigurationDraft,
} from '@/features/global-configuration/queries'

/**
 * The global configuration is one document, so the interesting failures are the
 * ones that would publish a file the gateway cannot use — a scheme that
 * disagrees with the base URL, a status code that is not a status code.
 */

const CONFIG: GlobalConfigurationResponse = {
  id: '00000000-0000-0000-0000-000000000001',
  baseUrl: 'https://api.example.com',
  requestIdKey: 'X-Request-Id',
  downstreamScheme: 'https',
  timeout: 90_000,
  rateLimit: { enableRateLimiting: true, httpStatusCode: '429' },
  qoS: { timeoutValue: 90_000, durationOfBreak: 30_000 },
  httpHandler: { useProxy: true, expect100Continue: false, maxConnectionsPerServer: 200 },
  serviceDiscovery: {
    provider: 'Consul',
    host: 'consul.internal',
    port: 8500,
    type: 'Http',
    configuration: { PollingInterval: '5000' },
  },
  updatedAt: '2026-01-02T03:04:05Z',
}

const errors = (draft: GlobalConfigurationDraft) => configurationErrors(draft)

describe('loading the form', () => {
  it('fills every property from the document', () => {
    const draft = draftFromConfiguration(CONFIG)

    expect(draft.baseUrl).toBe('https://api.example.com')
    expect(draft.requestIdKey).toBe('X-Request-Id')
    expect(draft.downstreamScheme).toBe('https')
    expect(draft.timeout).toBe('90000')
    expect(draft.rateLimit.enableRateLimiting).toBe(true)
    expect(draft.rateLimit.httpStatusCode).toBe('429')
    expect(draft.qoS.timeoutValue).toBe('90000')
    expect(draft.qoS.durationOfBreak).toBe('30000')
    expect(draft.httpHandler.useProxy).toBe(true)
    expect(draft.httpHandler.maxConnectionsPerServer).toBe('200')
    expect(draft.serviceDiscovery.provider).toBe('Consul')
    expect(draft.serviceDiscovery.port).toBe('8500')
  })

  it('renders the provider settings as editable lines', () => {
    // A dictionary is not editable as a form field, and a free-text box with
    // JSON in it would be a worse experience than one line per setting.
    expect(draftFromConfiguration(CONFIG).serviceDiscovery.configuration).toBe(
      'PollingInterval: 5000',
    )
  })

  it('falls back to the API defaults when a block is absent', () => {
    // A document written before these blocks existed should still open to a
    // form that saves the values the gateway expects.
    const sparse: GlobalConfigurationResponse = {
      ...CONFIG,
      qoS: null,
      httpHandler: null,
      serviceDiscovery: null,
    }

    const draft = draftFromConfiguration(sparse)

    expect(draft.qoS.timeoutValue).toBe('90000')
    expect(draft.qoS.durationOfBreak).toBe('30000')
    // useProxy defaults to true in the domain, not to false.
    expect(draft.httpHandler.useProxy).toBe(true)
    expect(draft.serviceDiscovery.provider).toBe('')
  })
})

describe('shaping the request', () => {
  it('sends a cleared field as null rather than an empty string', () => {
    // The API stores "" as a value, which is not the same as not being set.
    const request = toUpdateRequest(draftFromConfiguration({ ...CONFIG, requestIdKey: '' }))

    expect(request.requestIdKey).toBeNull()
  })

  it('sends blank numeric fields as null', () => {
    const request = toUpdateRequest(draftFromConfiguration({ ...CONFIG, timeout: null }))

    expect(request.timeout).toBeNull()
  })

  it('round-trips the document unchanged', () => {
    const request = toUpdateRequest(draftFromConfiguration(CONFIG))

    expect(request).toEqual({
      baseUrl: 'https://api.example.com',
      requestIdKey: 'X-Request-Id',
      downstreamScheme: 'https',
      timeout: 90_000,
      rateLimit: { enableRateLimiting: true, httpStatusCode: '429' },
      qoS: { timeoutValue: 90_000, durationOfBreak: 30_000 },
      httpHandler: {
        useProxy: true,
        expect100Continue: false,
        maxConnectionsPerServer: 200,
      },
      serviceDiscovery: {
        provider: 'Consul',
        host: 'consul.internal',
        port: 8500,
        type: 'Http',
        configuration: { PollingInterval: '5000' },
      },
    })
  })

  it('parses provider settings on the first colon only', () => {
    // A value with a colon in it — a URL, say — must survive the round trip.
    const draft = draftFromConfiguration(CONFIG)
    draft.serviceDiscovery.configuration = 'Query: host:port=8080'

    expect(toUpdateRequest(draft).serviceDiscovery?.configuration).toEqual({
      Query: 'host:port=8080',
    })
  })

  it('ignores blank and separator-less lines in provider settings', () => {
    const draft = draftFromConfiguration(CONFIG)
    draft.serviceDiscovery.configuration = 'A: 1\n\nnonsense\n\nB: 2'

    expect(toUpdateRequest(draft).serviceDiscovery?.configuration).toEqual({
      A: '1',
      B: '2',
    })
  })
})

describe('validation', () => {
  it('accepts the document as loaded', () => {
    expect(errors(draftFromConfiguration(CONFIG))).toEqual({})
  })

  it('rejects a base URL with no scheme', () => {
    const draft = draftFromConfiguration(CONFIG)
    draft.baseUrl = 'api.example.com'

    expect(errors(draft).baseUrl).toContain('http://')
  })

  it('rejects a scheme that disagrees with the base URL', () => {
    // Ocelot pairs the two, and a mismatch is a configuration that resolves
    // nothing rather than one that fails loudly.
    const draft = draftFromConfiguration({ ...CONFIG, downstreamScheme: 'http' })

    expect(errors(draft).downstreamScheme).toContain('Does not match')
  })

  it('accepts a scheme that agrees with the base URL', () => {
    const draft = draftFromConfiguration({ ...CONFIG, downstreamScheme: 'HTTPS' })

    expect(errors(draft).downstreamScheme).toBeUndefined()
  })

  it('rejects an unknown scheme', () => {
    const draft = draftFromConfiguration(CONFIG)
    draft.downstreamScheme = 'ftp'

    expect(errors(draft).downstreamScheme).toContain('http, https')
  })

  it('rejects a rate limit status code that is not a status code', () => {
    const draft = draftFromConfiguration(CONFIG)
    draft.rateLimit.httpStatusCode = '42'

    expect(errors(draft)['rateLimit.httpStatusCode']).toContain('three-digit')
  })

  it.each(['0', '-1', '3600001'])('rejects a timeout of %s', (value) => {
    const draft = draftFromConfiguration(CONFIG)
    draft.timeout = value

    expect(errors(draft).timeout).toBeTruthy()
  })

  it('requires the QoS values, which have no unset state', () => {
    const draft = draftFromConfiguration(CONFIG)
    draft.qoS.timeoutValue = ''
    draft.qoS.durationOfBreak = ''

    expect(errors(draft)['qoS.timeoutValue']).toBeTruthy()
    expect(errors(draft)['qoS.durationOfBreak']).toBeTruthy()
  })

  it('accepts a zero break duration, which means stay open', () => {
    const draft = draftFromConfiguration(CONFIG)
    draft.qoS.durationOfBreak = '0'

    expect(errors(draft)['qoS.durationOfBreak']).toBeUndefined()
  })

  it('rejects a port outside 1 to 65535', () => {
    const draft = draftFromConfiguration(CONFIG)
    draft.serviceDiscovery.port = '70000'

    expect(errors(draft)['serviceDiscovery.port']).toBeTruthy()
  })

  it('rejects a discovery host with no provider', () => {
    // Configuration nothing will read is worse than no configuration, because
    // it looks configured.
    const draft = draftFromConfiguration(CONFIG)
    draft.serviceDiscovery.provider = ''

    expect(errors(draft).serviceDiscovery).toContain('needs a provider')
  })

  it('rejects a provider setting line with no separator', () => {
    const draft = draftFromConfiguration(CONFIG)
    draft.serviceDiscovery.configuration = 'PollingInterval'

    expect(errors(draft)['serviceDiscovery.configuration']).toContain('key: value')
  })

  it('rejects a provider setting line with no key', () => {
    const draft = draftFromConfiguration(CONFIG)
    draft.serviceDiscovery.configuration = ': 5000'

    expect(errors(draft)['serviceDiscovery.configuration']).toContain('has none')
  })

  it('accepts an empty form except for the values that have no unset state', () => {
    const found = errors(emptyGlobalConfigurationDraft())

    // QoS has server-side defaults and no null in its shape, so it must be set.
    expect(found.timeout).toBeUndefined()
    expect(found['qoS.timeoutValue']).toBeUndefined()
  })
})

describe('dirty tracking', () => {
  it('is clean as loaded', () => {
    const draft = draftFromConfiguration(CONFIG)

    expect(isDirty(draft, draft)).toBe(false)
  })

  it('is dirty after an edit', () => {
    const baseline = draftFromConfiguration(CONFIG)
    const draft = { ...baseline, baseUrl: 'https://other.example.com' }

    expect(isDirty(draft, baseline)).toBe(true)
  })

  it('ignores whitespace an operator typed around a value', () => {
    // Typing into a field should not mark the form dirty when the value itself
    // did not change, or every accidental space costs a reload to undo.
    const baseline = draftFromConfiguration(CONFIG)
    const draft = { ...baseline, requestIdKey: '  X-Request-Id  ' }

    expect(isDirty(draft, baseline)).toBe(false)
  })

  it('is clean when a field is cleared back to how it was loaded', () => {
    const baseline = draftFromConfiguration(CONFIG)
    const draft = { ...baseline, requestIdKey: '' }
    const cleared = { ...baseline, requestIdKey: '' }

    expect(isDirty(draft, cleared)).toBe(false)
  })

  it('is dirty when a field is cleared from a value that was set', () => {
    const baseline = draftFromConfiguration(CONFIG)
    const draft = { ...baseline, requestIdKey: '' }

    expect(isDirty(draft, baseline)).toBe(true)
  })
})

describe('the pairs parser', () => {
  it('keeps a value containing colons', () => {
    expect(parsePairs('a: b:c')).toEqual({ a: 'b:c' })
  })

  it('skips lines with no key', () => {
    expect(parsePairs('a: 1\n: 2')).toEqual({ a: '1' })
  })
})
