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
