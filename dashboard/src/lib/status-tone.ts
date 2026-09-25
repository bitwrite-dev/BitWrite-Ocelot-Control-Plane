/**
 * Status → tone mapping.
 *
 * Kept separate from the component so `status-badge.tsx` only exports
 * components (React Fast Refresh) and the mapping can be tested or reused on its
 * own.
 *
 * The status strings are not invented here — they are the exact values the
 * domain produces (`SnapshotStatus`, `PublicationStatus`, `RuntimeStatus`,
 * `LicenseStatus` in Domain/ValueObjects/Status/StatusValueObjects.cs). An
 * unrecognised value maps to `neutral` rather than disappearing, so a new domain
 * status shows up as visible-but-unstyled instead of rendering nothing.
 */

export type StatusTone = 'success' | 'info' | 'warning' | 'destructive' | 'neutral'

export const TONE_CLASSES: Record<StatusTone, string> = {
  success: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-100',
  info: 'bg-sky-100 text-sky-900 dark:bg-sky-950 dark:text-sky-100',
  warning: 'bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-100',
  destructive: 'bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-100',
  neutral: 'bg-muted text-muted-foreground',
}

const STATUS_TONES: Record<string, StatusTone> = {
  // SnapshotStatus
  Ready: 'info',
  Published: 'success',
  Archived: 'neutral',
  // PublicationStatus
  Pending: 'warning',
  Failed: 'destructive',
  // RuntimeStatus
  Active: 'success',
  Connecting: 'warning',
  Degraded: 'warning',
  Disconnected: 'destructive',
  // LicenseStatus
  Expired: 'destructive',
  Revoked: 'destructive',
}

export function toneForStatus(status: string): StatusTone {
  return STATUS_TONES[status] ?? 'neutral'
}
