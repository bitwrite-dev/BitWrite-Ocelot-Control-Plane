import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'

import { Sidebar } from '@/components/sidebar'
import { ThemeProvider } from '@/features/theme/theme-provider'

/**
 * The sidebar, and the one rule it states.
 *
 * A snapshot page is where someone reaches for a delete button. The rule that
 * stops them belongs in the navigation itself, not only in prose at the top of
 * a page they may be looking past.
 */
describe('Sidebar', () => {
  it('states the lifecycle guardrail where the navigation is', () => {
    render(
      <ThemeProvider>
        <MemoryRouter>
          <Sidebar />
        </MemoryRouter>
      </ThemeProvider>,
    )

    expect(screen.getByText('Lifecycle guardrail')).toBeInTheDocument()
    expect(
      screen.getByText(/snapshots are immutable\. publication selects the desired runtime state/i),
    ).toBeInTheDocument()
  })

  it('lists the real pages from the navigation model', () => {
    render(
      <ThemeProvider>
        <MemoryRouter>
          <Sidebar />
        </MemoryRouter>
      </ThemeProvider>,
    )

    expect(screen.getByRole('link', { name: /snapshots/i })).toHaveAttribute('href', '/snapshots')
    expect(screen.getByRole('link', { name: /gateways/i })).toHaveAttribute('href', '/gateways')
  })

  it('hides the entries a role is not allowed to see', () => {
    render(
      <ThemeProvider>
        <MemoryRouter>
          <Sidebar roles={['Admin']} />
        </MemoryRouter>
      </ThemeProvider>,
    )

    // Admin sees everything, so the filter must not have removed a section.
    expect(screen.getByRole('link', { name: /settings/i })).toBeInTheDocument()
  })
})
