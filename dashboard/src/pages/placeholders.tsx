import { PageHeader } from '@/components/app-layout'
import { EmptyState } from '@/components/page-state'
import { Button } from '@/components/ui/button'
import { NAV_SECTIONS, type SectionId } from '@/navigation'

/**
 * Placeholder for a page whose implementation has not landed yet.
 *
 * Each page has its own tracked issue. Rendering an explicit "not built yet"
 * state is deliberate: a blank screen would be indistinguishable from a bug,
 * and it keeps the navigation honest while the pages are filled in.
 */
export function PagePlaceholder({
  title,
  issue,
  description,
}: {
  title: string
  issue: string
  description?: string
}) {
  return (
    <>
      <PageHeader title={title} />
      <EmptyState
        title="Not implemented yet"
        description={description ?? `Tracked in ${issue}.`}
        action={
          <Button variant="outline" asChild>
            <a href={`https://github.com/bitwrite-dev/BitWrite-Ocelot-Control-Plane/issues/${issue}`}>
              Open {issue}
            </a>
          </Button>
        }
      />
    </>
  )
}

const ISSUE_BY_PATH: Record<string, { title: string; issue: string }> = {
  '/': { title: 'Overview', issue: '435' },
  '/routes': { title: 'Routes', issue: '436' },
  '/routes/new': { title: 'Create Route', issue: '437' },
  '/services': { title: 'Services', issue: '439' },
  '/global-configuration': { title: 'Global Configuration', issue: '440' },
  '/snapshots': { title: 'Snapshots', issue: '441' },
  '/plugins': { title: 'Plugins', issue: '442' },
  '/monitoring': { title: 'Monitoring', issue: '443' },
  '/audit': { title: 'Audit Log', issue: '444' },
  '/settings': { title: 'Settings', issue: '445' },
}

/** Not covered by a dedicated page issue: rendered as a simple placeholder. */
const EXTRA_PATHS: Record<string, { title: string; issue: string }> = {
  '/gateways': { title: 'Gateways', issue: '262' },
  '/publications': { title: 'Publications', issue: '262' },
  '/runtime': { title: 'Runtime', issue: '262' },
  '/licenses': { title: 'Licenses', issue: '262' },
  '/routes/:id': { title: 'Route Details', issue: '438' },
}

export function PlaceholderPage({ path }: { path: string }) {
  const meta = ISSUE_BY_PATH[path] ?? EXTRA_PATHS[path] ?? { title: path, issue: '262' }
  return <PagePlaceholder title={meta.title} issue={meta.issue} />
}

/** 404 for an unknown path. */
export function NotFoundPage() {
  return (
    <>
      <PageHeader title="Page not found" />
      <EmptyState
        title="No such page"
        description="The address you followed does not match any page in the console."
      />
    </>
  )
}

export { NAV_SECTIONS }
export type { SectionId }
