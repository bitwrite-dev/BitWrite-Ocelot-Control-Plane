import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import App from '@/App'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

describe('App', () => {
  it('exposes a top-level heading landmark', () => {
    render(<App />)

    // Guards the a11y regression where shadcn's CardTitle rendered a plain div
    // and the page had no heading at all.
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(
      /ocelot control plane/i,
    )
  })

  it('shows the bootstrap placeholder rather than real page content', () => {
    render(<App />)

    // #434 replaces this placeholder with the real shell.
    expect(
      screen.getByRole('button', { name: /action buttons/i }),
    ).toBeDisabled()
  })

  it('lists no page navigation yet', () => {
    render(<App />)

    // Navigation lands in #434. If a nav appears here the placeholder is stale.
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
  })
})

describe('shadcn/ui wiring', () => {
  it('applies the variant classes from the registry component', () => {
    render(<Button>Click me</Button>)

    const button = screen.getByRole('button', { name: 'Click me' })
    // Asserts the generated variant classes are actually applied, which proves
    // class-variance-authority and the cn() helper are wired up correctly.
    expect(button.className).toContain('inline-flex')
  })

  it('merges a custom className with the component base classes', () => {
    render(<Button className="custom-marker">Click me</Button>)

    const button = screen.getByRole('button', { name: 'Click me' })
    expect(button.className).toContain('custom-marker')
    expect(button.className).toContain('inline-flex')
  })

  it('marks a disabled button as disabled', () => {
    render(
      <Card>
        <CardHeader>
          <CardTitle>Title</CardTitle>
        </CardHeader>
        <CardContent>
          <Button disabled>Action</Button>
        </CardContent>
      </Card>,
    )

    expect(screen.getByRole('button', { name: 'Action' })).toBeDisabled()
    expect(screen.getByText('Title')).toBeInTheDocument()
  })
})
