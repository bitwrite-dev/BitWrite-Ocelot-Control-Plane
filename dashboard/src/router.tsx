import { createBrowserRouter, RouterProvider } from 'react-router-dom'

import { createAppProviders } from '@/app-providers'
import { AppLayout } from '@/components/app-layout'
import { NotFoundPage, PlaceholderPage } from '@/pages/placeholders'
import { OverviewPage } from '@/features/overview/overview-page'
import { RouteDetailsPage } from '@/features/routes/route-details-page'
import { RoutesPage } from '@/features/routes/routes-page'
import {
  CreateRouteWizardPage,
  EditRouteWizardPage,
} from '@/features/routes-wizard/create-route-wizard'
import { DYNAMIC_PATHS, PLACEHOLDER_PATHS, assertNavigationIsRoutable } from '@/route-table'
import type { Role } from '@/navigation'

// `/routes/new` is listed before `/routes/:id` so the wizard is not swallowed by
// the detail route.
// Widened so filtering out an implemented path does not narrow the tuple to never.
const dynamicPlaceholderPaths: readonly string[] = DYNAMIC_PATHS

const routes = [
  {
    path: '/',
    element: <AppLayout />,
    children: [
      // The first two real pages; the rest are still placeholders.
      { index: true, element: <OverviewPage /> },
      { path: 'routes', element: <RoutesPage /> },
      { path: 'routes/new', element: <CreateRouteWizardPage /> },
      // Before routes/:id, so the static segment is not read as an id.
      { path: 'routes/:id/edit', element: <EditRouteWizardPage /> },
      ...PLACEHOLDER_PATHS.filter((path) => path !== '/').map((path) => ({
        path: path.replace(/^\//, ''),
        element: <PlaceholderPage path={path} />,
      })),
      // `/routes/:id` is real now; the remaining dynamic paths are not.
      { path: 'routes/:id', element: <RouteDetailsPage /> },
      ...dynamicPlaceholderPaths.filter((path) => path !== '/routes/:id').map((path) => ({
        path: path.replace(/^\//, ''),
        element: <PlaceholderPage path={path} />,
      })),
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]

// Fail fast at module load if the sidebar and the route table disagree.
assertNavigationIsRoutable()

export function AppRouter({ roles }: { roles?: Role[] } = {}) {
  // `key` forces a remount when the resolved roles change, so the sidebar
  // re-filters. Replace with real role resolution once #433 lands auth.
  return createAppProviders({
    children: (
      <RouterProvider router={createBrowserRouter(routes)} key={roles?.join(',') ?? 'all'} />
    ),
  })
}
