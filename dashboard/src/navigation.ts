/**
 * Navigation model for the dashboard.
 *
 * Section grouping follows spec §53: MANAGEMENT, CONFIGURATION, OPERATIONS,
 * SYSTEM.
 *
 * There is deliberately **no Consumers section** — see ADR-020. Consumers are
 * not exposed by the control plane.
 *
 * `roles` mirrors the authorization policies configured on the API
 * (`Admin`, `GatewayManager`, `RouteManager`, `SnapshotManager`). The shell
 * hides items the current user cannot reach; it is a usability affordance, not
 * a security boundary — the API enforces access.
 */

export type SectionId = 'management' | 'configuration' | 'operations' | 'system'

export type Role = 'Admin' | 'GatewayManager' | 'RouteManager' | 'SnapshotManager'

export interface NavItem {
  /** Route path, absolute, e.g. `/routes`. */
  path: string
  label: string
  /** Roles allowed to see this item. Undefined means all authenticated users. */
  roles?: Role[]
}

export interface NavSection {
  id: SectionId
  label: string
  items: NavItem[]
}

export const NAV_SECTIONS: NavSection[] = [
  {
    id: 'management',
    label: 'Management',
    items: [
      { path: '/', label: 'Overview' },
      { path: '/gateways', label: 'Gateways', roles: ['Admin', 'GatewayManager'] },
      { path: '/routes', label: 'Routes', roles: ['Admin', 'RouteManager'] },
      { path: '/services', label: 'Services', roles: ['Admin', 'RouteManager'] },
    ],
  },
  {
    id: 'configuration',
    label: 'Configuration',
    items: [
      { path: '/global-configuration', label: 'Global Configuration', roles: ['Admin'] },
      { path: '/plugins', label: 'Plugins', roles: ['Admin'] },
      { path: '/licenses', label: 'Licenses', roles: ['Admin'] },
    ],
  },
  {
    id: 'operations',
    label: 'Operations',
    items: [
      { path: '/snapshots', label: 'Snapshots', roles: ['Admin', 'SnapshotManager'] },
      { path: '/publications', label: 'Publications', roles: ['Admin', 'SnapshotManager'] },
      { path: '/runtime', label: 'Runtime', roles: ['Admin', 'GatewayManager'] },
      { path: '/monitoring', label: 'Monitoring', roles: ['Admin', 'GatewayManager'] },
    ],
  },
  {
    id: 'system',
    label: 'System',
    items: [
      { path: '/audit', label: 'Audit Log', roles: ['Admin'] },
      { path: '/settings', label: 'Settings', roles: ['Admin'] },
    ],
  },
]

/** Every navigable path, flattened. Used to validate routes at startup. */
export const NAV_PATHS: string[] = NAV_SECTIONS.flatMap((section) =>
  section.items.map((item) => item.path),
)

/**
 * Returns the sections with items the given roles may see. Sections left empty
 * are dropped so the sidebar does not render a heading with nothing under it.
 */
export function visibleSections(roles: Role[] | undefined): NavSection[] {
  if (!roles || roles.length === 0) {
    // No roles resolved yet (still loading, or anonymous): show everything and
    // let the API reject what the user cannot access.
    return NAV_SECTIONS
  }

  return NAV_SECTIONS.map((section) => ({
    ...section,
    items: section.items.filter(
      (item) => !item.roles || item.roles.some((role) => roles.includes(role)),
    ),
  })).filter((section) => section.items.length > 0)
}
