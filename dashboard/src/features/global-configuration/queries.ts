import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { useApi } from '@/app-providers'
import type {
  GlobalConfigurationResponse,
  HttpHandlerConfigRequest,
  QoSConfigRequest,
  RateLimitConfigRequest,
  ServiceDiscoveryConfigRequest,
} from '@/api'

/**
 * The global configuration: what is published to every gateway.
 *
 * One document, read whole. There is no list and no paging, and the settings
 * nested inside it are edited in place rather than added or removed.
 */

export const globalConfigKeys = {
  detail: () => ['global-configuration', 'detail'] as const,
}

export function useGlobalConfiguration() {
  const api = useApi()

  return useQuery({
    queryKey: globalConfigKeys.detail(),
    queryFn: ({ signal }) => api.resources.globalConfiguration.get({ signal }),
  })
}

export function useGlobalConfigurationMutations() {
  const api = useApi()
  const queryClient = useQueryClient()

  return {
    update: useMutation({
      mutationFn: (body: GlobalConfigurationRequest) =>
        api.resources.globalConfiguration.update(body),
      // Every page that shows the effective configuration derives from this
      // document, so a save makes all of it stale.
      onSuccess: () => queryClient.invalidateQueries({ queryKey: globalConfigKeys.detail() }),
    }),
  }
}

/**
 * The editable state of every §10.1 property.
 *
 * Numeric fields are strings so a value can be typed into and cleared; `0` and
 * `''` are different answers and conflating them is a bug in its own right.
 * Blank means "not set", which the API stores as null rather than as an empty
 * value.
 */
export interface GlobalConfigurationDraft {
  baseUrl: string
  requestIdKey: string
  downstreamScheme: string
  timeout: string
  rateLimit: {
    enableRateLimiting: boolean
    httpStatusCode: string
  }
  qoS: {
    timeoutValue: string
    durationOfBreak: string
  }
  httpHandler: {
    useProxy: boolean
    expect100Continue: boolean
    maxConnectionsPerServer: string
  }
  serviceDiscovery: {
    provider: string
    host: string
    port: string
    type: string
    /** Free-form provider settings, one `key: value` per line. */
    configuration: string
  }
}

export const emptyGlobalConfigurationDraft = (): GlobalConfigurationDraft => ({
  baseUrl: '',
  requestIdKey: '',
  downstreamScheme: '',
  timeout: '',
  rateLimit: { enableRateLimiting: false, httpStatusCode: '' },
  // The API's own defaults, so an untouched document still round-trips the way
  // the gateway expects.
  qoS: { timeoutValue: '90000', durationOfBreak: '30000' },
  httpHandler: { useProxy: true, expect100Continue: false, maxConnectionsPerServer: '' },
  serviceDiscovery: { provider: '', host: '', port: '', type: '', configuration: '' },
})

function toDraftValue(value: number | null | undefined): string {
  return value === null || value === undefined ? '' : String(value)
}

export function draftFromConfiguration(
  config: GlobalConfigurationResponse,
): GlobalConfigurationDraft {
  return {
    baseUrl: config.baseUrl ?? '',
    requestIdKey: config.requestIdKey ?? '',
    downstreamScheme: config.downstreamScheme ?? '',
    timeout: toDraftValue(config.timeout),
    rateLimit: {
      enableRateLimiting: config.rateLimit?.enableRateLimiting ?? false,
      httpStatusCode: config.rateLimit?.httpStatusCode ?? '',
    },
    qoS: {
      timeoutValue: toDraftValue(config.qoS?.timeoutValue ?? 90000),
      durationOfBreak: toDraftValue(config.qoS?.durationOfBreak ?? 30000),
    },
    httpHandler: {
      useProxy: config.httpHandler?.useProxy ?? true,
      expect100Continue: config.httpHandler?.expect100Continue ?? false,
      maxConnectionsPerServer: toDraftValue(config.httpHandler?.maxConnectionsPerServer),
    },
    serviceDiscovery: {
      provider: config.serviceDiscovery?.provider ?? '',
      host: config.serviceDiscovery?.host ?? '',
      port: toDraftValue(config.serviceDiscovery?.port),
      type: config.serviceDiscovery?.type ?? '',
      configuration: toLines(config.serviceDiscovery?.configuration),
    },
  }
}

function toLines(configuration: Record<string, string> | null | undefined): string {
  if (!configuration) return ''
  return Object.entries(configuration)
    .map(([key, value]) => `${key}: ${value}`)
    .join('\n')
}

/** Blank is null rather than "", which the API would store as an empty value. */
function toValueOrNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

function toNumberOrNull(value: string): number | null {
  const trimmed = value.trim()
  if (trimmed === '') return null
  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

/**
 * Parses a `key: value` block back into a map.
 *
 * The split is on the first colon, because a value may itself contain one.
 */
export function parsePairs(block: string): Record<string, string> {
  const result: Record<string, string> = {}
  for (const line of block.split('\n')) {
    const trimmed = line.trim()
    if (trimmed === '') continue
    const separator = trimmed.indexOf(':')
    if (separator === -1) continue
    const key = trimmed.slice(0, separator).trim()
    if (key === '') continue
    result[key] = trimmed.slice(separator + 1).trim()
  }
  return result
}

export interface GlobalConfigurationRequest {
  baseUrl: string | null
  requestIdKey: string | null
  downstreamScheme: string | null
  timeout: number | null
  rateLimit: RateLimitConfigRequest | null
  qoS: QoSConfigRequest | null
  httpHandler: HttpHandlerConfigRequest | null
  serviceDiscovery: ServiceDiscoveryConfigRequest | null
}

export function toUpdateRequest(draft: GlobalConfigurationDraft): GlobalConfigurationRequest {
  return {
    baseUrl: toValueOrNull(draft.baseUrl),
    requestIdKey: toValueOrNull(draft.requestIdKey),
    downstreamScheme: toValueOrNull(draft.downstreamScheme),
    timeout: toNumberOrNull(draft.timeout),
    rateLimit: {
      enableRateLimiting: draft.rateLimit.enableRateLimiting,
      httpStatusCode: toValueOrNull(draft.rateLimit.httpStatusCode),
    },
    qoS: {
      timeoutValue: toNumberOrNull(draft.qoS.timeoutValue) ?? 0,
      durationOfBreak: toNumberOrNull(draft.qoS.durationOfBreak) ?? 0,
    },
    httpHandler: {
      useProxy: draft.httpHandler.useProxy,
      expect100Continue: draft.httpHandler.expect100Continue,
      maxConnectionsPerServer: toNumberOrNull(draft.httpHandler.maxConnectionsPerServer),
    },
    serviceDiscovery: {
      provider: toValueOrNull(draft.serviceDiscovery.provider),
      host: toValueOrNull(draft.serviceDiscovery.host),
      port: toNumberOrNull(draft.serviceDiscovery.port),
      type: toValueOrNull(draft.serviceDiscovery.type),
      configuration: parsePairs(draft.serviceDiscovery.configuration),
    },
  }
}

/** The URL and the scheme have to agree, or the published file is wrong. */
const SCHEMES = ['http', 'https'] as const

export function configurationErrors(
  draft: GlobalConfigurationDraft,
): Record<string, string> {
  const errors: Record<string, string> = {}

  if (!isBlank(draft.baseUrl)) {
    const url = draft.baseUrl.trim()
    if (!/^https?:\/\//i.test(url)) {
      errors.baseUrl = 'Must start with http:// or https://'
    } else if (!isBlank(draft.downstreamScheme)) {
      const scheme = draft.downstreamScheme.trim().toLowerCase()
      if (!SCHEMES.includes(scheme as (typeof SCHEMES)[number])) {
        errors.downstreamScheme = `Must be one of ${SCHEMES.join(', ')}`
      } else if (url.toLowerCase().startsWith(`${scheme}://`)) {
        // Matches, so nothing to say.
      } else {
        // Ocelot pairs the downstream scheme with the base URL, and the two
        // disagreeing is a configuration that resolves nothing.
        errors.downstreamScheme = `Does not match the base URL, which is ${url.split('://')[0]}`
      }
    }
  } else if (!isBlank(draft.downstreamScheme)) {
    const scheme = draft.downstreamScheme.trim().toLowerCase()
    if (!SCHEMES.includes(scheme as (typeof SCHEMES)[number])) {
      errors.downstreamScheme = `Must be one of ${SCHEMES.join(', ')}`
    }
  }

  // The global timeout is `int?` on the API, so blank is a real answer: it
  // leaves the timeout unset rather than storing zero. The QoS values below are
  // non-nullable and genuinely required.
  const timeout = optionalNumericError('Timeout (ms)', draft.timeout, 1, 3_600_000)
  if (timeout) errors.timeout = timeout

  if (
    !isBlank(draft.rateLimit.httpStatusCode) &&
    !/^\d{3}$/.test(draft.rateLimit.httpStatusCode.trim())
  ) {
    errors['rateLimit.httpStatusCode'] = 'Must be a three-digit HTTP status code'
  }

  const timeoutValue = numericError('QoS timeout (ms)', draft.qoS.timeoutValue, 1, 3_600_000)
  if (timeoutValue) errors['qoS.timeoutValue'] = timeoutValue

  const duration = numericError('QoS break duration (ms)', draft.qoS.durationOfBreak, 0, 86_400_000)
  if (duration) errors['qoS.durationOfBreak'] = duration

  const connections = optionalNumericError(
    'Max connections per server',
    draft.httpHandler.maxConnectionsPerServer,
    1,
    Number.MAX_SAFE_INTEGER,
  )
  if (connections) errors['httpHandler.maxConnectionsPerServer'] = connections

  if (!isBlank(draft.serviceDiscovery.port)) {
    const port = optionalNumericError('Port', draft.serviceDiscovery.port, 1, 65535)
    if (port) errors['serviceDiscovery.port'] = port
  }

  if (!isBlank(draft.serviceDiscovery.host) && isBlank(draft.serviceDiscovery.provider)) {
    // A host with no provider is configuration nothing will read.
    errors.serviceDiscovery =
      'A discovery host needs a provider, or the settings are ignored'
  }

  for (const line of draft.serviceDiscovery.configuration.split('\n')) {
    const trimmed = line.trim()
    if (trimmed === '') continue
    if (trimmed.indexOf(':') === -1) {
      errors['serviceDiscovery.configuration'] = `Each line needs a key: value — "${trimmed}" does not`
      break
    }
    if (trimmed.startsWith(':')) {
      errors['serviceDiscovery.configuration'] = `Each line needs a key — "${trimmed}" has none`
      break
    }
  }

  return errors
}

function isBlank(value: string): boolean {
  return value.trim() === ''
}

function optionalNumericError(
  label: string,
  value: string,
  min: number,
  max: number,
): string | null {
  if (isBlank(value)) return null
  return numericError(label, value, min, max)
}

function numericError(
  label: string,
  value: string,
  min: number,
  max: number,
): string | null {
  if (isBlank(value)) return `${label} is required`
  const parsed = Number(value.trim())
  if (!Number.isInteger(parsed)) return `${label} must be a whole number`
  if (parsed < min || parsed > max) {
    return `${label} must be between ${min.toLocaleString()} and ${max.toLocaleString()}`
  }
  return null
}

/**
 * Whether the draft differs from what was loaded.
 *
 * Compared through the request shape, so two drafts that produce the same
 * payload are not treated as different — otherwise clearing a field to blank
 * and leaving it blank would both read as dirty.
 */
export function isDirty(
  draft: GlobalConfigurationDraft,
  baseline: GlobalConfigurationDraft,
): boolean {
  return JSON.stringify(toUpdateRequest(draft)) !== JSON.stringify(toUpdateRequest(baseline))
}
