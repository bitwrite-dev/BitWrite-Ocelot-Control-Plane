import { render, screen, within } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { describe, expect, it } from 'vitest'

import { AppLayout } from '@/components/app-layout'
import { NotFoundPage, PlaceholderPage } from '@/pages/placeholders'
import { NAV_SECTIONS } from '@/navigation'
import type { Role } from '@/navigation'

function renderAt(path: string, roles?: Role[]) {
  const router = createMemoryRouter(
    [
      {
        path: '/',
        element: <AppLayout roles={roles} />,
        children: [
          { index: true, element: <PlaceholderPage path="/" /> },
          { path: 'routes', element: <PlaceholderPage path="/routes" /> },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
    { initialEntries: [path] },
  )
  return render(<RouterProvider router={router} />)
}

describe('AppLayout', () => {
  it('renders the main navigation landmark', () => {
    renderAt('/')

    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument()
  })

  it('renders all four §53 sections as headings', () => {
    renderAt('/')

    for (const section of NAV_SECTIONS) {
      expect(screen.getByRole('heading', { name: section.label })).toBeInTheDocument()
    }
  })

  it('shows no Consumers entry, per ADR-020', () => {
    renderAt('/')

    expect(screen.queryByText(/consumer/i)).not.toBeInTheDocument()
  })

  it('links every navigation item to its route', () => {
    renderAt('/')
    const nav = screen.getByRole('navigation', { name: 'Main' })

    for (const section of NAV_SECTIONS) {
      for (const item of section.items) {
        const link = within(nav).getByRole('link', { name: item.label })
        expect(link).toHaveAttribute('href', item.path)
      }
    }
  })

  it('hides items the given roles cannot reach', () => {
    renderAt('/', ['RouteManager'])
    const nav = screen.getByRole('navigation', { name: 'Main' })

    expect(within(nav).getByRole('link', { name: 'Routes' })).toBeInTheDocument()
    // SnapshotManager territory, and the whole System section.
    expect(within(nav).queryByRole('link', { name: 'Snapshots' })).not.toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Settings' })).not.toBeInTheDocument()
  })

  it('renders a decorative icon per nav item without polluting the accessible name', () => {
    renderAt('/')
    const nav = screen.getByRole('navigation', { name: 'Main' })
    const link = within(nav).getByRole('link', { name: 'Routes' })

    // The label alone is the accessible name; the icon is presentation only.
    expect(link).toHaveAccessibleName('Routes')
    expect(link.querySelector('svg')).not.toBeNull()
  })

  it('marks the active route', () => {
    renderAt('/routes')
    const nav = screen.getByRole('navigation', { name: 'Main' })

    const active = within(nav).getByRole('link', { name: 'Routes' })
    const inactive = within(nav).getByRole('link', { name: 'Services' })

    expect(active.className).toContain('bg-primary')
    expect(inactive.className).not.toContain('bg-primary')
  })
})

describe('PagePlaceholder', () => {
  it('states plainly that the page is not built yet', () => {
    renderAt('/')

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Overview')
    expect(screen.getByText('Not implemented yet')).toBeInTheDocument()
  })

  it('links to the tracking issue', () => {
    renderAt('/routes')

    const link = screen.getByRole('link', { name: /Open 436/ })
    expect(link).toHaveAttribute(
      'href',
      'https://github.com/bitwrite-dev/BitWrite-Ocelot-Control-Plane/issues/436',
    )
  })
})

describe('NotFoundPage', () => {
  it('renders a heading for an unknown path', () => {
    renderAt('/nope')

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Page not found')
  })
})
