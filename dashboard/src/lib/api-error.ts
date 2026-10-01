import { ApiError } from '@/api'

/**
 * Whether a failure is a permission problem.
 *
 * Read from the status rather than matched against the message text: the
 * message is whatever the server chose to say, and a 403 whose body is empty
 * would otherwise be reported as an ordinary failure.
 */
export function isPermissionError(error: unknown): boolean {
  if (error instanceof ApiError) return error.status === 403
  return false
}

/**
 * The domain's own code for a rejected request, when it sent one.
 *
 * Prefer this over matching on the message. The message is written for the
 * operator reading the screen and can be reworded at any time; the code is the
 * contract. `null` means the failure was not a domain one — a network error, or
 * a fault the server declined to describe.
 */
export function domainErrorCode(error: unknown): string | null {
  if (error instanceof ApiError) return error.errorCode ?? null
  return null
}

/**
 * Whether a failure was a request the domain refused, whatever the reason.
 *
 * Distinct from a fault: a refused request has an explanation attached and
 * retrying it unchanged will be refused again. Showing "something went wrong"
 * for one tells an operator to retry something that cannot succeed.
 */
export function isRefusedRequest(error: unknown): boolean {
  if (!(error instanceof ApiError)) return false
  return error.errorCode !== undefined || (error.status >= 400 && error.status < 500)
}
