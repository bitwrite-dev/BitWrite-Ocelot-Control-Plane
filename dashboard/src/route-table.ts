/**
 * Route table and the guard that keeps it in sync with the sidebar.
 *
 * Separate from `router.tsx` so that file only exports components, which keeps
 * React Fast Refresh working.
 */

import { NAV_PATHS } from '@/navigation'

/** Every path reachable from the sidebar, plus the dynamic ones. */
/**
 * Paths that are not implemented yet and render a placeholder.
 *
 * Only genuinely unbuilt pages belong here. A path that has a real component
 * must not stay in this list: the router renders the real page *and* this list
 * still claims the path is a placeholder, so the two disagree and a reader has
 * no way to tell which is right.
 */
export const PLACEHOLDER_PATHS = [
  '/plugins',
  '/runtime',
  '/licenses',
] as const

/** Parameterised paths, matched after the static ones. */
export const DYNAMIC_PATHS = ['/routes/:id'] as const

/**
 * Paths with a real component, listed so the routability check can see them.
 *
 * Before this existed the check only knew about the placeholder list, so a
 * sidebar entry pointing at a real page that had been removed from the
 * placeholders would pass — the check was satisfied by the wrong source.
 */
export const IMPLEMENTED_PATHS = [
  '/',
  '/routes',
  '/routes/new',
  '/routes/:id',
  '/routes/:id/edit',
  '/services',
  '/settings',
  '/gateways',
  '/global-configuration',
  '/snapshots',
  '/snapshots/new',
  '/audit',
  '/monitoring',
  '/publications',
] as const

export const ALL_ROUTES: readonly string[] = [
  ...IMPLEMENTED_PATHS,
  ...PLACEHOLDER_PATHS,
  ...DYNAMIC_PATHS,
]

/** A path claimed by both lists, which would make "is this real?" unanswerable. */
export function pathsClaimedTwice(): string[] {
  const placeholders = new Set<string>(PLACEHOLDER_PATHS)
  return IMPLEMENTED_PATHS.filter((path) => placeholders.has(path))
}

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
        'Add them to PLACEHOLDER_PATHS (unbuilt) or IMPLEMENTED_PATHS (real page) ' +
        'in src/route-table.ts.',
    )
  }
}
