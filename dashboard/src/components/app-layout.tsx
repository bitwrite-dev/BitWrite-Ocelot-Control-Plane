import { Suspense } from 'react'
import { Outlet } from 'react-router-dom'

import { Sidebar, MobileSidebar } from '@/components/sidebar'
import { LoadingState } from '@/components/page-state'
import type { Role } from '@/navigation'

/**
 * Application shell: sidebar, a top bar for the mobile trigger, and the routed
 * content region. The responsive collapse lives in #434's sidebar; the
 * navigation model is in `navigation.ts`.
 */
export function AppLayout({ roles }: { roles?: Role[] }) {
  return (
    <div className="flex min-h-screen bg-background">
      <Sidebar roles={roles} />

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 items-center gap-3 border-b px-4 md:hidden">
          <MobileSidebar roles={roles} />
          <span className="font-heading text-sm font-semibold">Ocelot Control Plane</span>
        </header>

        <main className="flex-1 overflow-x-hidden p-4 md:p-6">
          <Suspense fallback={<LoadingState />}>
            <Outlet />
          </Suspense>
        </main>
      </div>
    </div>
  )
}

/** Standard page frame: title, optional description, optional actions, content. */
export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string
  description?: string
  actions?: React.ReactNode
}) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div>
        {/* Level 1 so each page has exactly one top-level heading. */}
        <h1 className="font-heading text-2xl font-semibold tracking-tight">{title}</h1>
        {description ? (
          <p className="mt-1 text-sm text-muted-foreground">{description}</p>
        ) : null}
      </div>
      {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
    </div>
  )
}
