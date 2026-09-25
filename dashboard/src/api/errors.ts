/**
 * The API returns two different error shapes and the client has to flatten both.
 *
 * 1. Application/domain failures — `GlobalExceptionMiddleware` and the
 *    controllers emit this shape:
 *      { "correlationId": "...", "error": "...", "type": "DomainException" }
 *
 * 2. ASP.NET Core model-binding failures — `[ApiController]` short-circuits
 *    before the action runs and emits a `ValidationProblemDetails`:
 *      { "type": "...", "title": "...", "status": 400,
 *        "errors": { "field": ["message"] }, "traceId": "..." }
 *
 * Anything else (a proxy 502, an HTML error page from a gateway) is treated as
 * an opaque failure and reported with the status code alone.
 */

/** Field-level validation problems, keyed by field name. */
export type FieldErrors = Record<string, string[]>

export interface NormalizedApiError {
  /** HTTP status, or 0 when the request never produced a response. */
  status: number
  /** The `type` discriminator from either shape, when present. */
  type?: string
  /** Human-readable message, safe to show in the UI. */
  message: string
  /** Server-side correlation id, for support and log correlation. */
  correlationId?: string
  /** Present for `ValidationProblemDetails` responses. */
  fieldErrors?: FieldErrors
  /** True when the request failed before any HTTP response arrived. */
  isNetworkError: boolean
}

/**
 * An API call that failed. Carries the normalized shape so callers never have to
 * branch on which wire format the server happened to use.
 */
export class ApiError extends Error {
  readonly status: number
  readonly type?: string
  readonly correlationId?: string
  readonly fieldErrors?: FieldErrors
  readonly isNetworkError: boolean

  constructor(normalized: NormalizedApiError) {
    super(normalized.message)
    this.name = 'ApiError'
    this.status = normalized.status
    this.type = normalized.type
    this.correlationId = normalized.correlationId
    this.fieldErrors = normalized.fieldErrors
    this.isNetworkError = normalized.isNetworkError
  }

  /** True when re-authenticating could plausibly fix this. */
  get isUnauthorized(): boolean {
    return this.status === 401
  }

  get isNotFound(): boolean {
    return this.status === 404
  }

  /** True for `ValidationProblemDetails`, i.e. field-level errors. */
  get isValidation(): boolean {
    return this.fieldErrors !== undefined
  }
}

interface ProblemDetailsLike {
  correlationId?: unknown
  error?: unknown
  type?: unknown
  title?: unknown
  status?: unknown
  errors?: unknown
  traceId?: unknown
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function asString(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined
}

function toFieldErrors(value: unknown): FieldErrors | undefined {
  if (!isRecord(value)) return undefined

  const result: FieldErrors = {}
  for (const [field, messages] of Object.entries(value)) {
    if (Array.isArray(messages)) {
      result[field] = messages.filter((m): m is string => typeof m === 'string')
    } else if (typeof messages === 'string') {
      result[field] = [messages]
    }
  }
  return Object.keys(result).length > 0 ? result : undefined
}

/** Flattens a `ValidationProblemDetails` into a single sentence. */
function summarizeFieldErrors(fieldErrors: FieldErrors): string {
  return Object.entries(fieldErrors)
    .map(([field, messages]) => `${field}: ${messages.join(', ')}`)
    .join('; ')
}

/**
 * Turns any response body into a `NormalizedApiError`.
 *
 * `fallbackMessage` is used when the body is not JSON or carries no message, so
 * an HTML error page from a proxy still yields something displayable.
 */
export function normalizeError(
  status: number,
  body: unknown,
  fallbackMessage: string,
): NormalizedApiError {
  if (!isRecord(body)) {
    return { status, message: fallbackMessage, isNetworkError: false }
  }

  const problem = body as ProblemDetailsLike
  const fieldErrors = toFieldErrors(problem.errors)
  const type = asString(problem.type)
  const correlationId = asString(problem.correlationId) ?? asString(problem.traceId)

  if (fieldErrors) {
    return {
      status,
      type,
      correlationId,
      fieldErrors,
      message: summarizeFieldErrors(fieldErrors),
      isNetworkError: false,
    }
  }

  const message = asString(problem.error) ?? asString(problem.title) ?? fallbackMessage

  return { status, type, correlationId, message, isNetworkError: false }
}

/** Normalizes a thrown `fetch` rejection — the request never got a response. */
export function normalizeNetworkError(cause: unknown): NormalizedApiError {
  const message = cause instanceof Error ? cause.message : 'Network request failed'
  return {
    status: 0,
    message: `Could not reach the API: ${message}`,
    isNetworkError: true,
  }
}
