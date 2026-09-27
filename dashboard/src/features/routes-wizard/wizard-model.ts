/**
 * The 8 steps of the Create Route Wizard that the API can actually store.
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

export interface RouteDraft {
  key: string
  method: string
  upstreamPath: string
  host: string
  serviceId: string
  downstreamTargets: DownstreamTargetDraft[]
  /** Authentication is one field on the API: a list of allowed scopes. */
  allowedScopes: string[]
  rateLimit: RateLimitDraft
  qos: QosDraft
  cache: CacheDraft
  loadBalancer: LoadBalancerDraft
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

export const emptyRouteDraft = (): RouteDraft => ({
  key: '',
  method: 'GET',
  upstreamPath: '',
  host: '',
  serviceId: '',
  downstreamTargets: [{ host: '', port: '', scheme: 'http', path: '/' }],
  allowedScopes: [],
  rateLimit: { enabled: false, limit: 100, period: 'Minute' },
  qos: { enabled: false, timeoutSeconds: 90, circuitBreakerTimeoutSeconds: 30 },
  cache: { enabled: false, ttlSeconds: 300 },
  loadBalancer: { enabled: false, algorithm: 'RoundRobin' },
})

const isBlank = (value: string) => value.trim().length === 0

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
  description: 'Response caching and load balancing.',
  requires: [5],
  validate: (draft) => {
    const errors: string[] = []
    if (draft.cache.enabled) {
      const ttl = inRangeError('Cache TTL (seconds)', draft.cache.ttlSeconds, 1, 86400)
      if (ttl) errors.push(ttl)
    }
    if (draft.loadBalancer.enabled && isBlank(draft.loadBalancer.algorithm)) {
      errors.push('A load balancing algorithm must be selected')
    }
    return errors
  },
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
export function toCreateRequest(draft: RouteDraft) {
  return {
    key: draft.key.trim(),
    method: draft.method,
    upstreamPath: draft.upstreamPath.trim(),
    host: draft.host.trim() === '' ? null : draft.host.trim(),
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
