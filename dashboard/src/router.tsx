import { createBrowserRouter, RouterProvider } from 'react-router-dom'

import { createAppProviders } from '@/app-providers'
import { AppLayout } from '@/components/app-layout'
import { NotFoundPage, PlaceholderPage } from '@/pages/placeholders'
import { OverviewPage } from '@/features/overview/overview-page'
import { DYNAMIC_PATHS, PLACEHOLDER_PATHS, assertNavigationIsRoutable } from '@/route-table'
import type { Role } from '@/navigation'

// `/routes/new` is listed before `/routes/:id` so the wizard is not swallowed by
// the detail route.
const routes = [
  {
    path: '/',
    element: <AppLayout />,
    children: [
      // The index route is the first real page; the rest are still placeholders.
      { index: true, element: <OverviewPage /> },
      ...PLACEHOLDER_PATHS.filter((path) => path !== '/').map((path) => ({
        path: path.replace(/^\//, ''),
        element: <PlaceholderPage path={path} />,
      })),
      ...DYNAMIC_PATHS.map((path) => ({
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
