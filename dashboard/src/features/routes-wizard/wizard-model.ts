import type { RouteResponse, TransformEntryResponse } from '@/api'

/**
 * The 8 steps of the route wizard that the API can actually store.
 *
 * The spec lists 10. Two are omitted because nothing in the API can accept them:
 * Authorization and Transformations. They are still modelled in the domain, but
 * absent from `CreateRouteRequest` and from `RedisRouteRepository`, so anything
 * entered there would be discarded on save. See #466.
 *
 * Ordering matters: each step declares the step indexes it depends on, so
 * "can I advance?" is answered by data rather than by hard-coded positions.
 */

export type Period = 'Second' | 'Minute' | 'Hour' | 'Day'

export interface DownstreamTargetDraft {
  host: string
  port: number | ''
  scheme: string
  path: string
}

export interface RateLimitDraft {
  enabled: boolean
  limit: number | ''
  period: Period
}

export interface QosDraft {
  enabled: boolean
  timeoutSeconds: number | ''
  circuitBreakerTimeoutSeconds: number | ''
}

export interface CacheDraft {
  enabled: boolean
  ttlSeconds: number | ''
}

export interface LoadBalancerDraft {
  enabled: boolean
  algorithm: string
}

/**
 * One header rule.
 *
 * `add` becomes DownstreamHeaderTransform and `transform` becomes
 * UpstreamHeaderTransform, which is the only split Ocelot 18 offers.
 * `remove` has no equivalent there and is refused on save.
 */
export interface HeaderRuleDraft {
  name: string
  value: string
}

export interface HeaderTransformsDraft {
  /** Rule: header: value */
  add: string
  /** Rewrite: header: value */
  transform: string
}

export interface HttpClientOptionsDraft {
  enabled: boolean
  allowAutoRedirect: boolean
  maxConnectionsPerServer: string
  pooledConnectionLifetimeSeconds: string
  useCookieContainer: boolean
  useProxy: boolean
  useTracing: boolean
}

/**
 * How the request is made downstream: the verb, the protocol version, the TLS
 * check, the handlers, and the client behind it.
 */
export interface TransportDraft {
  /** Empty keeps the upstream verb. Ocelot takes one verb, not a list. */
  downstreamMethod: string
  /** Empty forwards the path unchanged, which is Ocelot's "/{everything}". */
  downstreamTemplate: string
  /** '' leaves the framework default, which is not the same as asking for 1.1. */
  downstreamHttpVersion: string
  /** Only meaningful together with a version. */
  downstreamHttpVersionPolicy: string
  acceptAnyServerCertificate: boolean
  delegatingHandlers: string[]
  httpClient: HttpClientOptionsDraft
  /** '' means the framework default rather than no timeout. */
  timeoutSeconds: string
}

export interface RouteDraft {
  key: string
  method: string
  upstreamPath: string
  host: string
  /** Higher is matched first among overlapping routes. */
  priority: number
  routeIsCaseSensitive: boolean
  serviceId: string
  downstreamTargets: DownstreamTargetDraft[]
  /** Authentication is one field on the API: a list of allowed scopes. */
  allowedScopes: string[]
  rateLimit: RateLimitDraft
  qos: QosDraft
  cache: CacheDraft
  loadBalancer: LoadBalancerDraft
  headers?: HeaderTransformsDraft
  /**
   * Optional so a caller that builds a partial draft by hand still type-checks.
   * `toCreateRequest` and the validator both read a missing block as the
   * defaults rather than throwing.
   */
  transport?: TransportDraft
}

export type StepId =
  | 'basic'
  | 'upstream'
  | 'downstream'
  | 'authentication'
  | 'rate-limiting'
  | 'qos'
  | 'advanced'
  | 'review'

export interface WizardStep {
  id: StepId
  title: string
  description: string
  /** Indexes of steps that must be valid before this one can be reached. */
  requires: number[]
  /** Field-level validation; an empty array means valid. */
  validate: (draft: RouteDraft) => string[]
}

export const HTTP_METHODS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD', 'OPTIONS'] as const

export const PERIODS: Period[] = ['Second', 'Minute', 'Hour', 'Day']

export const LOAD_BALANCER_ALGORITHMS = ['RoundRobin', 'LeastConnection', 'Random', 'First'] as const

export const emptyTransportDraft = (): TransportDraft => ({
  downstreamMethod: '',
  downstreamTemplate: '',
  downstreamHttpVersion: '',
  downstreamHttpVersionPolicy: '',
  acceptAnyServerCertificate: false,
  delegatingHandlers: [],
  httpClient: {
    enabled: false,
    allowAutoRedirect: false,
    maxConnectionsPerServer: '',
    pooledConnectionLifetimeSeconds: '',
    useCookieContainer: false,
    useProxy: false,
    useTracing: false,
  },
  timeoutSeconds: '',
})

export const emptyRouteDraft = (): RouteDraft => ({
  key: '',
  method: 'GET',
  upstreamPath: '',
  host: '',
  // Zero and case-insensitive are the Ocelot defaults for both.
  priority: 0,
  routeIsCaseSensitive: false,
  serviceId: '',
  downstreamTargets: [{ host: '', port: '', scheme: 'http', path: '/' }],
  allowedScopes: [],
  rateLimit: { enabled: false, limit: 100, period: 'Minute' },
  qos: { enabled: false, timeoutSeconds: 90, circuitBreakerTimeoutSeconds: 30 },
  cache: { enabled: false, ttlSeconds: 300 },
  loadBalancer: { enabled: false, algorithm: 'RoundRobin' },
  transport: emptyTransportDraft(),
  headers: { add: '', transform: '' },
})

/** The versions Ocelot accepts for a downstream request. */
export const DOWNSTREAM_HTTP_VERSIONS = ['1.0', '1.1', '2.0'] as const

/** How strictly that version is asked for. Ocelot names all three. */
export const DOWNSTREAM_HTTP_VERSION_POLICIES = [
  'RequestVersionExact',
  'RequestVersionOrHigher',
  'RequestVersionOrLower',
] as const

const isBlank = (value: string) => value.trim().length === 0

/**
 * A blank numeric field means "leave the framework default alone". Sending 0
 * instead would fail the API's own range validation.
 */
function numberOrOmit(value: string): number | undefined {
  const trimmed = value.trim()
  if (trimmed === '') return undefined
  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : undefined
}

/**
 * Ocelot's own placeholders, offered so an operator does not have to remember
 * the spelling. A value containing one is a rewrite of that value, not a
 * literal, which is the distinction that matters.
 */
export const HEADER_PLACEHOLDERS = [
  'UpstreamHost',
  'BaseUrl',
  'RemoteIpAddress',
  'UpstreamMethod',
  'DownstreamPath',
  'RequestId',
] as const

/**
 * Parses a "header: value" block, one rule per line.
 *
 * A line with no colon is a rule with no value, which is legal: it sets the
 * header to empty. Anything after the first colon is the value, so a value may
 * itself contain colons — a URL, for instance.
 */
/** Renders stored rules back into the editable "header: value" block. */
function toHeaderBlock(entries: TransformEntryResponse[] | null | undefined): string {
  if (!entries || entries.length === 0) return ''
  return entries.map((entry) => `${entry.key}: ${entry.value}`).join('\n')
}

function parseHeaderRules(block: string): TransformEntryDraft[] {
  return block
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line !== '')
    .map((line) => {
      const separator = line.indexOf(':')
      return separator === -1
        ? { key: line, value: '' }
        : { key: line.slice(0, separator).trim(), value: line.slice(separator + 1).trim() }
    })
    .filter((entry) => entry.key !== '')
}

export interface TransformEntryDraft {
  key: string
  value: string
}

/** Drops keys whose value is undefined, so absence is real absence. */
/**
 * Emits a transformation block only when it has rules. A request carrying an
 * empty list is not the same as one carrying none: the API treats the first as
 * "the operator cleared this", and an absent block as "leave what is there".
 */
function headerBlock(headers: HeaderTransformsDraft | undefined): Record<string, unknown> {
  const add = parseHeaderRules(headers?.add ?? '')
  const transform = parseHeaderRules(headers?.transform ?? '')
  if (add.length === 0 && transform.length === 0) return {}
  // One block with both halves: the API takes a single transformations object
  // per kind, so they cannot be sent as two.
  return { headerTransformations: { add, transform } }
}

function compact<T extends object>(value: T): Partial<T> {
  return Object.fromEntries(
    Object.entries(value).filter(([, entry]) => entry !== undefined),
  ) as Partial<T>
}

/** A port is valid when it is a number in 1–65535; mirrors the API's Range. */
function portError(port: number | ''): string | null {
  if (port === '') return 'Port is required'
  if (!Number.isInteger(port)) return 'Port must be a whole number'
  if (port < 1 || port > 65535) return 'Port must be between 1 and 65535'
  return null
}

function inRangeError(
  label: string,
  value: number | '',
  min: number,
  max: number,
): string | null {
  if (value === '') return `${label} is required`
  if (value < min || value > max) return `${label} must be between ${min} and ${max}`
  return null
}

/**
 * The same check for an optional numeric field, where blank means "leave the
 * framework default alone" rather than "a value is missing".
 */
function optionalInRangeError(
  label: string,
  value: string,
  min: number,
  max: number,
): string | null {
  if (value.trim() === '') return null
  return inRangeError(label, Number(value), min, max)
}

const basic: WizardStep = {
  id: 'basic',
  title: 'Basic Information',
  description: 'How this route is identified in the control plane.',
  requires: [],
  validate: (draft) => {
    const errors: string[] = []
    if (isBlank(draft.key)) errors.push('Key is required')
    else if (draft.key.trim().length > 100) errors.push('Key must be 100 characters or fewer')
    if (isBlank(draft.method)) errors.push('Method is required')
    return errors
  },
}

const upstream: WizardStep = {
  id: 'upstream',
  title: 'Upstream',
  description: 'The path clients call on the gateway.',
  requires: [0],
  validate: (draft) => {
    const errors: string[] = []
    if (isBlank(draft.upstreamPath)) {
      errors.push('Upstream path is required')
    } else {
      if (!draft.upstreamPath.trim().startsWith('/')) {
        errors.push('Upstream path must start with /')
      }
      if (draft.upstreamPath.trim().length > 500) {
        errors.push('Upstream path must be 500 characters or fewer')
      }
    }
    if (draft.host.trim().length > 100) errors.push('Host must be 100 characters or fewer')
    return errors
  },
}

const downstream: WizardStep = {
  id: 'downstream',
  title: 'Downstream / Service',
  description: 'Where matching requests are forwarded, and which service this belongs to.',
  requires: [1],
  validate: (draft) => {
    const errors: string[] = []
    if (isBlank(draft.serviceId)) errors.push('A service must be selected')

    if (draft.downstreamTargets.length === 0) {
      errors.push('At least one downstream target is required')
    }

    draft.downstreamTargets.forEach((target, index) => {
      const label = `Target ${index + 1}`
      if (isBlank(target.host)) errors.push(`${label}: host is required`)
      if (target.host.trim().length > 200) errors.push(`${label}: host is too long`)
      const port = portError(target.port)
      if (port) errors.push(`${label}: ${port}`)
    })

    // The domain caps targets at 10, so reject early rather than at save time.
    if (draft.downstreamTargets.length > 10) {
      errors.push('A route cannot have more than 10 downstream targets')
    }

    return errors
  },
}

const authentication: WizardStep = {
  id: 'authentication',
  title: 'Authentication',
  description: 'Scopes the caller must present. Leave empty for an open route.',
  requires: [2],
  // Optional step: an empty scope list is valid and simply omits the field.
  validate: () => [],
}

const rateLimiting: WizardStep = {
  id: 'rate-limiting',
  title: 'Rate Limiting',
  description: 'Cap requests over a rolling window.',
  requires: [3],
  validate: (draft) => {
    if (!draft.rateLimit.enabled) return []
    const errors: string[] = []
    const limit = inRangeError('Limit', draft.rateLimit.limit, 1, Number.MAX_SAFE_INTEGER)
    if (limit) errors.push(limit)
    return errors
  },
}

const qos: WizardStep = {
  id: 'qos',
  title: 'QoS',
  description: 'Upstream timeout and circuit breaker behaviour.',
  requires: [4],
  validate: (draft) => {
    if (!draft.qos.enabled) return []
    const errors: string[] = []
    const timeout = inRangeError('Timeout (seconds)', draft.qos.timeoutSeconds, 1, 3600)
    if (timeout) errors.push(timeout)
    if (draft.qos.circuitBreakerTimeoutSeconds !== '') {
      const breaker = inRangeError(
        'Circuit breaker timeout (seconds)',
        draft.qos.circuitBreakerTimeoutSeconds,
        1,
        3600,
      )
      if (breaker) errors.push(breaker)
    }
    return errors
  },
}

const advanced: WizardStep = {
  id: 'advanced',
  title: 'Advanced Options',
  description: 'Matching behaviour, response caching and load balancing.',
  requires: [5],
  validate: (draft) => {
    const errors: string[] = []
    if (!Number.isInteger(draft.priority) || draft.priority < 0 || draft.priority > 1000) {
      errors.push('Priority must be between 0 and 1000')
    }
    if (draft.cache.enabled) {
      const ttl = inRangeError('Cache TTL (seconds)', draft.cache.ttlSeconds, 1, 86400)
      if (ttl) errors.push(ttl)
    }
    if (draft.loadBalancer.enabled && isBlank(draft.loadBalancer.algorithm)) {
      errors.push('A load balancing algorithm must be selected')
    }
    errors.push(...transportErrors(draft))
    errors.push(...headerErrors(draft))
    return errors
  },
}

/**
 * The transport rules, checked where they are entered.
 *
 * Each of these has a counterpart in the domain, but a wizard that only finds
 * out on save makes the operator guess which field was wrong.
 */
/**
 * Header rules are checked where they are entered, because a malformed one is
 * only reported by the API as a field error the operator then has to locate.
 */
function headerErrors(draft: RouteDraft): string[] {
  const errors: string[] = []
  const headers = draft.headers

  for (const [field, label] of [
    ['add', 'Downstream header rule'],
    ['transform', 'Upstream header rule'],
  ] as const) {
    const seen = new Set<string>()
    for (const entry of parseHeaderRules(headers?.[field] ?? '')) {
      if (!/^[A-Za-z0-9!#$%&'*+.^_`|~-]+$/.test(entry.key)) {
        errors.push(`${label} '${entry.key}' is not a valid header name`)
        continue
      }
      const key = entry.key.toLowerCase()
      if (seen.has(key)) {
        // The later rule would win silently.
        errors.push(`Header '${entry.key}' is set twice in the same block`)
      }
      seen.add(key)
    }
  }

  return errors
}

function transportErrors(draft: RouteDraft): string[] {
  const errors: string[] = []
  const transport = draft.transport ?? emptyTransportDraft()

  if (!isBlank(transport.downstreamMethod) && !HTTP_METHODS.includes(transport.downstreamMethod as never)) {
    errors.push(`Downstream method must be one of ${HTTP_METHODS.join(', ')}`)
  }

  if (!isBlank(transport.downstreamHttpVersion) && !DOWNSTREAM_HTTP_VERSIONS.includes(transport.downstreamHttpVersion as never)) {
    errors.push(`Downstream HTTP version must be one of ${DOWNSTREAM_HTTP_VERSIONS.join(', ')}`)
  }

  // A policy with nothing to apply it to would read as configured while
  // changing nothing.
  if (!isBlank(transport.downstreamHttpVersionPolicy)) {
    if (isBlank(transport.downstreamHttpVersion)) {
      errors.push('An HTTP version policy needs a version to apply to')
    } else if (!DOWNSTREAM_HTTP_VERSION_POLICIES.includes(transport.downstreamHttpVersionPolicy as never)) {
      errors.push(
        `HTTP version policy must be one of ${DOWNSTREAM_HTTP_VERSION_POLICIES.join(', ')}`,
      )
    }
  }

  const seen = new Set<string>()
  for (const handler of transport.delegatingHandlers) {
    const name = handler.trim()
    if (name === '') {
      errors.push('A delegating handler name cannot be empty')
      continue
    }
    const key = name.toLowerCase()
    if (seen.has(key)) {
      // Ocelot would register the same handler twice.
      errors.push(`Delegating handler '${name}' is listed twice`)
    }
    seen.add(key)
  }

  if (transport.httpClient.enabled) {
    const connections = optionalInRangeError(
      'Max connections per server',
      transport.httpClient.maxConnectionsPerServer,
      1,
      Number.MAX_SAFE_INTEGER,
    )
    if (connections) errors.push(connections)

    const lifetime = optionalInRangeError(
      'Pooled connection lifetime (seconds)',
      transport.httpClient.pooledConnectionLifetimeSeconds,
      1,
      Number.MAX_SAFE_INTEGER,
    )
    if (lifetime) errors.push(lifetime)
  }

  if (!isBlank(transport.timeoutSeconds)) {
    // Ocelot reads zero or less as "no timeout", which is a quiet way to wait
    // forever.
    const timeout = optionalInRangeError('Timeout (seconds)', transport.timeoutSeconds, 1, 86400)
    if (timeout) errors.push(timeout)
  }

  return errors
}

const review: WizardStep = {
  id: 'review',
  title: 'Review & Save',
  description: 'Check the configuration before creating the route.',
  // Depends on everything, since this is where the route is actually built.
  requires: [0, 1, 2, 3, 4, 5, 6],
  validate: (draft) =>
    WIZARD_STEPS.slice(0, 7).flatMap((step) => step.validate(draft)),
}

export const WIZARD_STEPS: WizardStep[] = [
  basic,
  upstream,
  downstream,
  authentication,
  rateLimiting,
  qos,
  advanced,
  review,
]

/** Steps named in the spec that cannot be stored yet. Surfaced, not silently dropped. */
export const UNSUPPORTED_STEPS = [
  { title: 'Authorization', issue: 466 },
  { title: 'Transformations', issue: 466 },
] as const

/** Index of the furthest step the draft can reach, given everything before it. */
export function furthestReachableStep(draft: RouteDraft): number {
  let reachable = 0
  for (let index = 0; index < WIZARD_STEPS.length; index += 1) {
    const step = WIZARD_STEPS[index]
    if (step.validate(draft).length > 0) break
    if (step.requires.some((requirement) => requirement > reachable)) break
    reachable = index + 1
  }
  return reachable
}

/**
 * Builds the create request, omitting every option block that is switched off.
 *
 * Sending a disabled block as a zero-valued object would fail the API's own
 * range validation, so absence has to mean absence rather than "empty".
 */
/**
 * The wire shape, declared so a caller can read a field that is only present
 * when the operator set it. Inferred types hid the optional blocks entirely.
 */
export interface CreateRouteRequestBody {
  key: string
  method: string
  upstreamPath: string
  host: string | null
  serviceId: string
  downstreamTargets: Array<{ host: string; port: number; scheme: string; path: string }>
  priority: number
  routeIsCaseSensitive: boolean
  downstreamMethod: string | null
  downstreamPathTemplate: string | null
  downstreamHttpVersion: string | null
  downstreamHttpVersionPolicy: string | null
  acceptAnyServerCertificate: boolean
  timeoutSeconds: number | null
  /** Only when the operator configured the block. */
  headerTransformations?: {
    add: Array<{ key: string; value: string }>
    transform: Array<{ key: string; value: string }>
  }
  delegatingHandlers?: string[]
  httpClientOptions?: Record<string, unknown>
  [key: string]: unknown
}

export function toCreateRequest(draft: RouteDraft): CreateRouteRequestBody {
  // A draft built as a partial literal has no transport block. Reading it as
  // the defaults is better than throwing part-way through assembling the
  // request, and narrowing once keeps every use below non-optional.
  const transport = draft.transport ?? emptyTransportDraft()
  const headers = draft.headers ?? emptyRouteDraft().headers!

  return {
    key: draft.key.trim(),
    method: draft.method,
    upstreamPath: draft.upstreamPath.trim(),
    host: draft.host.trim() === '' ? null : draft.host.trim(),
    // Not optional: a route is always built with them, unlike the feature
    // blocks that are omitted when switched off.
    priority: draft.priority,
    routeIsCaseSensitive: draft.routeIsCaseSensitive,
    downstreamMethod:
      transport.downstreamMethod.trim() === '' ? null : transport.downstreamMethod.trim(),
    downstreamPathTemplate:
      transport.downstreamTemplate.trim() === '' ? null : transport.downstreamTemplate.trim(),
    downstreamHttpVersion:
      transport.downstreamHttpVersion.trim() === ''
        ? null
        : transport.downstreamHttpVersion.trim(),
    downstreamHttpVersionPolicy:
      transport.downstreamHttpVersionPolicy.trim() === ''
        ? null
        : transport.downstreamHttpVersionPolicy.trim(),
    acceptAnyServerCertificate: transport.acceptAnyServerCertificate,
    ...headerBlock(headers),
    ...(transport.delegatingHandlers.length > 0
      ? { delegatingHandlers: transport.delegatingHandlers.map((h) => h.trim()).filter(Boolean) }
      : {}),
    ...(transport.httpClient.enabled
      ? {
          httpClientOptions: compact({
            allowAutoRedirect: transport.httpClient.allowAutoRedirect,
            maxConnectionsPerServer: numberOrOmit(
              transport.httpClient.maxConnectionsPerServer,
            ),
            pooledConnectionLifetimeSeconds: numberOrOmit(
              transport.httpClient.pooledConnectionLifetimeSeconds,
            ),
            useCookieContainer: transport.httpClient.useCookieContainer,
            useProxy: transport.httpClient.useProxy,
            useTracing: transport.httpClient.useTracing,
          }),
        }
      : {}),
    // Null means the framework default. Zero would mean "no timeout", which is
    // a good way to end up waiting forever.
    timeoutSeconds:
      transport.timeoutSeconds.trim() === '' ? null : Number(transport.timeoutSeconds.trim()),
    serviceId: draft.serviceId,
    downstreamTargets: draft.downstreamTargets.map((target) => ({
      host: target.host.trim(),
      port: Number(target.port),
      scheme: target.scheme,
      path: target.path,
    })),
    ...(draft.allowedScopes.length > 0
      ? { authenticationOptions: { allowedScopes: draft.allowedScopes } }
      : {}),
    ...(draft.rateLimit.enabled
      ? {
          rateLimitOptions: {
            enableRateLimiting: true,
            period: draft.rateLimit.period,
            limit: Number(draft.rateLimit.limit),
          },
        }
      : {}),
    ...(draft.qos.enabled
      ? {
          qosOptions: {
            timeoutSeconds: Number(draft.qos.timeoutSeconds),
            ...(draft.qos.circuitBreakerTimeoutSeconds === ''
              ? {}
              : { circuitBreakerTimeoutSeconds: Number(draft.qos.circuitBreakerTimeoutSeconds) }),
          },
        }
      : {}),
    ...(draft.cache.enabled
      ? { cacheOptions: { ttlSeconds: Number(draft.cache.ttlSeconds) } }
      : {}),
    ...(draft.loadBalancer.enabled
      ? { loadBalancerOptions: { algorithm: draft.loadBalancer.algorithm } }
      : {}),
  }
}

/**
 * Maps a server field name to the step that owns it.
 *
 * The API reports the field it rejected, so a failure can be sent back to the
 * step that can fix it rather than shown as one flat list on Review. The field
 * names here are the request names the API uses, not the draft's field names.
 */
const FIELD_TO_STEP: Record<string, StepId> = {
  key: 'basic',
  method: 'basic',
  upstreamPath: 'upstream',
  host: 'upstream',
  serviceId: 'downstream',
  downstreamTargets: 'downstream',
  authenticationOptions: 'authentication',
  'authenticationOptions.allowedScopes': 'authentication',
  rateLimitOptions: 'rate-limiting',
  qosOptions: 'qos',
  cacheOptions: 'advanced',
  loadBalancerOptions: 'advanced',
}

/**
 * The step a server error belongs to.
 *
 * Falls back to the review step, which shows the whole configuration, so an
 * unrecognised field still surfaces somewhere the operator can act on it.
 */
export function stepForField(field: string | null): StepId {
  if (field === null) return 'review'
  // A field like `downstreamTargets[0]` points at the step that owns the list.
  const base = field.replace(/\[\d+\]$/, '')
  return FIELD_TO_STEP[field] ?? FIELD_TO_STEP[base] ?? 'review'
}

/**
 * The algorithms the select offers, plus whatever a route already stores.
 *
 * The API returns the algorithm as a free string, so a route configured with a
 * value this build does not know about would otherwise show an empty select and
 * silently be rewritten to the first option on save.
 */
export function loadBalancerOptions(stored: string | undefined): string[] {
  const known = [...LOAD_BALANCER_ALGORITHMS]
  if (stored && !known.includes(stored as (typeof LOAD_BALANCER_ALGORITHMS)[number])) {
    return [stored, ...known]
  }
  return known
}

/** The periods the select offers, plus a stored value the build does not know. */
export function periodOptions(stored: string | undefined): string[] {
  const known = [...PERIODS]
  if (stored && !known.includes(stored as Period)) {
    return [stored, ...known]
  }
  return known
}

/**
 * Fills a draft from a stored route, so the same wizard can edit it.
 *
 * Round-trips every field the API can hold. Authorization and transformations
 * have no field on the response, so a saved route never reports them and they
 * cannot be pre-filled — see #466.
 */
export function draftFromRoute(route: RouteResponse): RouteDraft {
  const draft = emptyRouteDraft()

  draft.key = route.key ?? ''
  draft.method = route.method
  draft.upstreamPath = route.upstreamPath
  draft.host = route.host ?? ''
  draft.priority = route.priority
  draft.routeIsCaseSensitive = route.routeIsCaseSensitive
  draft.serviceId = route.serviceId
  draft.downstreamTargets =
    route.downstreamTargets.length > 0
      ? route.downstreamTargets.map((target) => ({
          host: target.host,
          port: target.port,
          scheme: target.scheme,
          path: target.path,
        }))
      : draft.downstreamTargets
  draft.allowedScopes = route.authenticationOptions?.allowedScopes ?? []
  draft.headers = {
    add: toHeaderBlock(route.headerTransformations?.add),
    transform: toHeaderBlock(route.headerTransformations?.transform),
  }

  draft.transport = {
    downstreamMethod: route.downstreamMethod ?? '',
    downstreamTemplate: route.downstreamPathTemplate ?? '',
    downstreamHttpVersion: route.downstreamHttpVersion ?? '',
    downstreamHttpVersionPolicy: route.downstreamHttpVersionPolicy ?? '',
    acceptAnyServerCertificate: route.dangerousAcceptAnyServerCertificateValidator ?? false,
    delegatingHandlers: [...(route.delegatingHandlers ?? [])],
    httpClient: route.httpClientOptions
      ? {
          enabled: true,
          allowAutoRedirect: route.httpClientOptions.allowAutoRedirect,
          // int.MaxValue is what an unset limit resolves to, so it is shown as
          // blank rather than as a number nobody chose.
          maxConnectionsPerServer:
            route.httpClientOptions.maxConnectionsPerServer >= Number.MAX_SAFE_INTEGER
              ? ''
              : String(route.httpClientOptions.maxConnectionsPerServer),
          pooledConnectionLifetimeSeconds:
            route.httpClientOptions.pooledConnectionLifetimeSeconds >= Number.MAX_SAFE_INTEGER
              ? ''
              : String(route.httpClientOptions.pooledConnectionLifetimeSeconds),
          useCookieContainer: route.httpClientOptions.useCookieContainer,
          useProxy: route.httpClientOptions.useProxy,
          useTracing: route.httpClientOptions.useTracing,
        }
      : emptyTransportDraft().httpClient,
    timeoutSeconds:
      route.timeoutSeconds === null || route.timeoutSeconds === undefined
        ? ''
        : String(route.timeoutSeconds),
  }

  if (route.rateLimitOptions?.enableRateLimiting) {
    draft.rateLimit = {
      enabled: true,
      limit: route.rateLimitOptions.limit,
      // periodOptions guarantees this value is selectable, even if the build
      // does not otherwise know the period.
      period: route.rateLimitOptions.period as Period,
    }
  }

  if (route.qoSOptions) {
    draft.qos = {
      enabled: true,
      timeoutSeconds: route.qoSOptions.timeoutSeconds,
      circuitBreakerTimeoutSeconds: route.qoSOptions.circuitBreakerTimeoutSeconds ?? '',
    }
  }

  if (route.cacheOptions) {
    draft.cache = { enabled: true, ttlSeconds: route.cacheOptions.ttlSeconds }
  }

  if (route.loadBalancerOptions) {
    draft.loadBalancer = {
      enabled: true,
      algorithm: route.loadBalancerOptions.algorithm,
    }
  }

  return draft
}
