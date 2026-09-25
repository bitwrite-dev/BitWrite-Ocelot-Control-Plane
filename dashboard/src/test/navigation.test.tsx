import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { NAV_PATHS, NAV_SECTIONS, visibleSections } from '@/navigation'
import { assertNavigationIsRoutable } from '@/route-table'
import { StatusBadge } from '@/components/status-badge'
import { toneForStatus } from '@/lib/status-tone'

describe('navigation model', () => {
  it('uses exactly the four sections from spec §53', () => {
    expect(NAV_SECTIONS.map((s) => s.id)).toEqual([
      'management',
      'configuration',
      'operations',
      'system',
    ])
  })

  it('omits a Consumers section per ADR-020', () => {
    const ids = NAV_SECTIONS.map((s) => s.id)
    const labels = NAV_SECTIONS.map((s) => s.label)

    expect(labels).not.toContain('Consumers')
    expect(ids).not.toContain('consumers')
    expect(JSON.stringify(NAV_SECTIONS)).not.toMatch(/consumer/i)
  })

  it('has no duplicate paths and no duplicate labels within a section', () => {
    const paths = NAV_SECTIONS.flatMap((s) => s.items.map((i) => i.path))
    expect(new Set(paths).size).toBe(paths.length)

    for (const section of NAV_SECTIONS) {
      const labels = section.items.map((i) => i.label)
      expect(new Set(labels).size).toBe(labels.length)
    }
  })

  it('has every nav path registered as a route', () => {
    expect(() => assertNavigationIsRoutable()).not.toThrow()
  })

  it('fails loudly when a nav entry has no route', () => {
    expect(() => assertNavigationIsRoutable(['/routes', '/does-not-exist'])).toThrow(
      /\/does-not-exist/,
    )
  })

  it('keeps NAV_PATHS in sync with the sections', () => {
    expect(NAV_PATHS).toEqual(NAV_SECTIONS.flatMap((s) => s.items.map((i) => i.path)))
  })
})

describe('visibleSections', () => {
  it('shows everything when no roles are resolved yet', () => {
    expect(visibleSections(undefined)).toHaveLength(4)
    expect(visibleSections([])).toHaveLength(4)
  })

  it('hides sections a role cannot reach at all', () => {
    const visible = visibleSections(['RouteManager'])
    const ids = visible.map((s) => s.id)

    // RouteManager can reach Routes and Services, but nothing in System.
    expect(ids).toContain('management')
    expect(ids).not.toContain('system')
  })

  it('never returns an empty section', () => {
    for (const sections of [
      visibleSections(['Admin']),
      visibleSections(['RouteManager']),
      visibleSections(['SnapshotManager']),
      visibleSections(['GatewayManager']),
    ]) {
      for (const section of sections) {
        expect(section.items.length).toBeGreaterThan(0)
      }
    }
  })

  it('lets a multi-role user see the union of their permissions', () => {
    const single = visibleSections(['RouteManager']).flatMap((s) => s.items.map((i) => i.path))
    const both = visibleSections(['RouteManager', 'SnapshotManager']).flatMap((s) =>
      s.items.map((i) => i.path),
    )

    expect(both.length).toBeGreaterThan(single.length)
    expect(both).toContain('/snapshots')
  })
})

describe('StatusBadge tones', () => {
  it('maps the real domain status values to tones', () => {
    // These are the exact strings produced by SnapshotStatus, PublicationStatus,
    // RuntimeStatus and LicenseStatus in the domain.
    expect(toneForStatus('Ready')).toBe('info')
    expect(toneForStatus('Published')).toBe('success')
    expect(toneForStatus('Archived')).toBe('neutral')
    expect(toneForStatus('Pending')).toBe('warning')
    expect(toneForStatus('Failed')).toBe('destructive')
    expect(toneForStatus('Active')).toBe('success')
    expect(toneForStatus('Degraded')).toBe('warning')
    expect(toneForStatus('Disconnected')).toBe('destructive')
  })

  it('falls back to neutral for an unknown status rather than hiding it', () => {
    expect(toneForStatus('SomeNewStatus')).toBe('neutral')
  })

  it('renders the raw status text so new values are visible', () => {
    render(<StatusBadge status="SomeNewStatus" />)

    expect(screen.getByText('SomeNewStatus')).toBeInTheDocument()
  })
})
