import type { AuditListParams } from '@/api'

/**
 * Page sizes, in one place.
 *
 * Different lists have different shapes. A table of routes is read by scanning;
 * the audit log is read by looking for one row, so it carries more at a time.
 */
export const pageSize = {
  audit: 50,
  default: 20,
} as const

/** A request for one page, with whatever filters apply. */
export type PageRequest = Omit<AuditListParams, 'page' | 'signal'>

/** The visible range, as a sentence rather than two numbers to subtract. */
export function describeRange(
  page: number,
  size: number,
  totalCount: number,
): string {
  if (totalCount === 0) return 'No entries'
  const first = size * (page - 1) + 1
  const last = Math.min(size * page, totalCount)
  return `${first}–${last} of ${totalCount}`
}

/**
 * How many pages there are.
 *
 * At least one, so an empty log still has a page 1 to say "nothing here" on.
 */
export function totalPages(totalCount: number, size: number): number {
  return Math.max(1, Math.ceil(totalCount / size))
}