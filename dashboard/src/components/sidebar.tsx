import { Menu } from 'lucide-react'
import { NavLink } from 'react-router-dom'

import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { NAV_SECTIONS, type Role } from '@/navigation'

/**
 * Sidebar navigation.
 *
 * Section grouping follows spec §53. There is no Consumers section by design
 * (ADR-020).
 */

function NavLinks({ roles, onNavigate }: { roles?: Role[]; onNavigate?: () => void }) {
  return (
    <nav aria-label="Main" className="flex-1 overflow-y-auto px-3 py-4">
      {NAV_SECTIONS.map((section) => {
        const items = roles?.length
          ? section.items.filter(
              (item) => !item.roles || item.roles.some((role) => roles.includes(role)),
            )
          : section.items

        if (items.length === 0) return null

        return (
          <div key={section.id} className="mb-6 last:mb-0">
            <h2 className="px-3 pb-2 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              {section.label}
            </h2>
            <ul className="space-y-1">
              {items.map((item) => (
                <li key={item.path}>
                  <NavLink
                    to={item.path}
                    end={item.path === '/'}
                    onClick={onNavigate}
                    className={({ isActive }) =>
                      [
                        'flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors',
                        isActive
                          ? 'bg-primary text-primary-foreground font-medium'
                          : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
                      ].join(' ')
                    }
                  >
                    {({ isActive }) => (
                      <>
                        <item.icon
                          className="size-4 shrink-0"
                          aria-hidden={!isActive}
                          strokeWidth={2}
                        />
                        {item.label}
                      </>
                    )}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        )
      })}
    </nav>
  )
}

function Brand() {
  return (
    <div className="px-6 py-5">
      <p className="font-heading text-sm font-semibold">Ocelot Control Plane</p>
      <p className="text-xs text-muted-foreground">Management Console</p>
    </div>
  )
}

export function Sidebar({ roles }: { roles?: Role[] }) {
  return (
    <aside className="hidden w-64 shrink-0 flex-col border-r bg-background md:flex">
      <Brand />
      <Separator />
      <NavLinks roles={roles} />
    </aside>
  )
}

/** Collapsed sidebar for narrow viewports, presented as a sheet. */
export function MobileSidebar({ roles }: { roles?: Role[] }) {
  return (
    <Sheet>
      <SheetTrigger asChild>
        <Button variant="ghost" size="icon" className="md:hidden" aria-label="Open navigation">
          <Menu className="size-5" aria-hidden="true" />
        </Button>
      </SheetTrigger>
      <SheetContent side="left" className="w-64 p-0">
        <SheetHeader>
          <SheetTitle className="px-6 py-5 text-left">
            <span className="font-heading text-sm font-semibold">Ocelot Control Plane</span>
          </SheetTitle>
        </SheetHeader>
        <Separator />
        <NavLinks roles={roles} />
      </SheetContent>
    </Sheet>
  )
}
