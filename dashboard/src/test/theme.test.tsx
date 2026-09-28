import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { ApiClientContext } from '@/app-providers'
import { ThemeToggle } from '@/components/theme-toggle'
import { ThemeProvider, useTheme } from '@/features/theme/theme-provider'
import { createApiClient } from '@/api'

/**
 * Theme switching is a behaviour, not a preference: the class has to land on the
 * root for the palette to apply, the choice has to survive a reload, and System
 * has to keep following the OS. Those three are asserted here.
 */

const STORAGE_KEY = 'ocelot-control-theme'

/** A controllable `matchMedia`, since jsdom has no OS preference to report. */
function stubMatchMedia(initialDark: boolean) {
  const listeners = new Set<(event: MediaQueryListEvent) => void>()
  let matches = initialDark

  const mql = {
    get matches() {
      return matches
    },
    media: '(prefers-color-scheme: dark)',
    onchange: null,
    addEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) => {
      listeners.add(listener)
    },
    removeEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) => {
      listeners.delete(listener)
    },
    dispatchEvent: () => true,
  } as unknown as MediaQueryList

  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => mql),
  )

  return {
    /** Flips the OS preference, as a machine switching at sunset would. */
    flip(nextDark: boolean) {
      matches = nextDark
      for (const listener of listeners) {
        listener({ matches: nextDark } as MediaQueryListEvent)
      }
    },
  }
}

function renderToggle() {
  const client = createApiClient({ baseUrl: 'http://api.test' })

  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <ApiClientContext.Provider value={client}>
        <ThemeProvider>
          <ThemeToggle />
          <ThemeProbe />
        </ThemeProvider>
      </ApiClientContext.Provider>
    </QueryClientProvider>,
  )
}

/** Surfaces the resolved theme where the assertions can see it. */
function ThemeProbe() {
  const { theme, resolvedTheme } = useTheme()
  return (
    <p>
      chosen:{theme} resolved:{resolvedTheme}
    </p>
  )
}

const rootIsDark = () => document.documentElement.classList.contains('dark')

beforeEach(() => {
  window.localStorage.clear()
  document.documentElement.classList.remove('dark')
})

afterEach(() => {
  vi.unstubAllGlobals()
  window.localStorage.clear()
  document.documentElement.classList.remove('dark')
})

describe('the default', () => {
  it('follows the OS when nothing has been chosen', () => {
    stubMatchMedia(true)
    renderToggle()

    // Light and dark are both available without being asked for.
    expect(screen.getByText(/resolved:dark/)).toBeInTheDocument()
    expect(rootIsDark()).toBe(true)
  })

  it('follows a light OS too', () => {
    stubMatchMedia(false)
    renderToggle()

    expect(screen.getByText(/resolved:light/)).toBeInTheDocument()
    expect(rootIsDark()).toBe(false)
  })
})

describe('choosing a theme', () => {
  it('applies dark and pins it against a light OS', async () => {
    stubMatchMedia(false)
    const user = userEvent.setup()
    renderToggle()

    await user.click(screen.getByRole('radio', { name: 'Dark' }))

    expect(rootIsDark()).toBe(true)
    expect(screen.getByText(/chosen:dark/)).toBeInTheDocument()
  })

  it('applies light and pins it against a dark OS', async () => {
    stubMatchMedia(true)
    const user = userEvent.setup()
    renderToggle()

    await user.click(screen.getByRole('radio', { name: 'Light' }))

    expect(rootIsDark()).toBe(false)
  })

  it('persists the choice so a reload does not revert to the OS', async () => {
    stubMatchMedia(false)
    const user = userEvent.setup()
    renderToggle()

    await user.click(screen.getByRole('radio', { name: 'Dark' }))

    expect(window.localStorage.getItem(STORAGE_KEY)).toBe('dark')
  })

  it('restores a persisted choice on the next load', () => {
    // The point of persisting: a reload should not silently revert.
    window.localStorage.setItem(STORAGE_KEY, 'dark')
    stubMatchMedia(false)

    renderToggle()

    expect(rootIsDark()).toBe(true)
    expect(screen.getByText(/chosen:dark/)).toBeInTheDocument()
  })

  it('ignores a stored value it does not recognise', () => {
    // A corrupted or hand-edited value must not leave the page in a theme it
    // has no styles for.
    window.localStorage.setItem(STORAGE_KEY, 'chartreuse')
    stubMatchMedia(false)

    renderToggle()

    expect(screen.getByText(/chosen:system/)).toBeInTheDocument()
    expect(rootIsDark()).toBe(false)
  })

  it('keeps working when localStorage refuses to be written', async () => {
    // Some privacy modes throw, and a theme is not worth a crash.
    const getItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('denied')
    })
    stubMatchMedia(false)
    const user = userEvent.setup()
    renderToggle()

    await user.click(screen.getByRole('radio', { name: 'Dark' }))

    expect(rootIsDark()).toBe(true)
    getItem.mockRestore()
  })
})

describe('the System option', () => {
  it('keeps following the OS once chosen', async () => {
    // A machine that switches to dark at sunset should do so without a reload,
    // which means the listener stays attached rather than sampling once.
    const media = stubMatchMedia(false)
    const user = userEvent.setup()
    renderToggle()

    await user.click(screen.getByRole('radio', { name: 'System' }))
    expect(rootIsDark()).toBe(false)

    media.flip(true)
    await screen.findByText(/resolved:dark/)
    expect(rootIsDark()).toBe(true)
  })

  it('stops following the OS once a theme is pinned', async () => {
    const media = stubMatchMedia(false)
    const user = userEvent.setup()
    renderToggle()

    await user.click(screen.getByRole('radio', { name: 'Dark' }))
    media.flip(false)

    // Pinned means pinned, in both directions.
    expect(rootIsDark()).toBe(true)
  })
})

describe('the control itself', () => {
  it('offers exactly three choices and marks the active one', () => {
    stubMatchMedia(false)
    renderToggle()

    const group = screen.getByRole('radiogroup', { name: 'Colour theme' })
    expect(group).toBeInTheDocument()
    expect(screen.getAllByRole('radio')).toHaveLength(3)
    expect(screen.getByRole('radio', { name: 'System' })).toHaveAttribute(
      'aria-checked',
      'true',
    )
  })
})
