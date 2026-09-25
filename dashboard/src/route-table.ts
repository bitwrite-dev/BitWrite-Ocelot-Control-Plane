/**
 * Route table and the guard that keeps it in sync with the sidebar.
 *
 * Separate from `router.tsx` so that file only exports components, which keeps
 * React Fast Refresh working.
 */

import { NAV_PATHS } from '@/navigation'

/** Every path reachable from the sidebar, plus the dynamic ones. */
export const PLACEHOLDER_PATHS = [
  '/',
  '/gateways',
  '/routes',
  '/routes/new',
  '/services',
  '/global-configuration',
  '/snapshots',
  '/plugins',
  '/monitoring',
  '/audit',
  '/settings',
  '/publications',
  '/runtime',
  '/licenses',
] as const

/** Parameterised paths, matched after the static ones. */
export const DYNAMIC_PATHS = ['/routes/:id'] as const

export const ALL_ROUTES: readonly string[] = [...PLACEHOLDER_PATHS, ...DYNAMIC_PATHS]

/**
 * Throws if a sidebar entry has no matching route. Called at module load so the
 * mismatch surfaces immediately in development rather than as a dead link.
 */
export function assertNavigationIsRoutable(navPaths: readonly string[] = NAV_PATHS): void {
  const registered = new Set<string>(ALL_ROUTES)
  const missing = navPaths.filter((path) => !registered.has(path))

  if (missing.length > 0) {
    throw new Error(
      `Navigation entries have no route: ${missing.join(', ')}. ` +
        'Add them to PLACEHOLDER_PATHS in src/route-table.ts.',
    )
  }
}
