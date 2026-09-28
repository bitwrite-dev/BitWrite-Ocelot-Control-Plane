import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'

/**
 * Light / Dark / System.
 *
 * `System` follows the OS and keeps following it: the listener stays attached, so
 * a machine that switches to dark at sunset does so without a reload. The other
 * two pin a choice and are persisted, so a reload does not silently revert to
 * whatever the OS says.
 */
export const THEMES = ['light', 'dark', 'system'] as const

export type Theme = (typeof THEMES)[number]

/** The two the UI can actually render; `system` resolves to one of them. */
export type ResolvedTheme = 'light' | 'dark'

const STORAGE_KEY = 'ocelot-control-theme'

const ThemeContext = createContext<{
  theme: Theme
  resolvedTheme: ResolvedTheme
  setTheme: (theme: Theme) => void
} | null>(null)

/**
 * Reads the stored choice, falling back to System.
 *
 * Anything unrecognised is treated as System rather than trusted, so a corrupted
 * or hand-edited value cannot leave the page in a theme it has no styles for.
 */
function readStoredTheme(): Theme {
  if (typeof window === 'undefined') return 'system'

  const stored = window.localStorage.getItem(STORAGE_KEY)
  return THEMES.includes(stored as Theme) ? (stored as Theme) : 'system'
}

/** localStorage throws in some privacy modes, and a theme is not worth a crash. */
function persist(theme: Theme): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, theme)
  } catch {
    // The choice lasts for the session instead.
  }
}

function prefersDark(): boolean {
  return (
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-color-scheme: dark)').matches
  )
}

/**
 * Applies the class the palette is written against.
 *
 * The `.dark` variables already existed and every component already styles off
 * them, so this only has to put the class on the root.
 */
function applyTheme(resolved: ResolvedTheme): void {
  if (typeof document === 'undefined') return
  document.documentElement.classList.toggle('dark', resolved === 'dark')
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(readStoredTheme)
  const [systemDark, setSystemDark] = useState<boolean>(prefersDark)

  // Only meaningful for `system`, but the listener stays attached either way so
  // switching to system is instant rather than picking up a stale value.
  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return

    const query = window.matchMedia('(prefers-color-scheme: dark)')
    const onChange = (event: MediaQueryListEvent) => setSystemDark(event.matches)

    query.addEventListener('change', onChange)
    return () => query.removeEventListener('change', onChange)
  }, [])

  const resolvedTheme: ResolvedTheme =
    theme === 'system' ? (systemDark ? 'dark' : 'light') : theme

  useEffect(() => {
    applyTheme(resolvedTheme)
  }, [resolvedTheme])

  const setTheme = useCallback((next: Theme) => {
    setThemeState(next)
    persist(next)
  }, [])

  const value = useMemo(
    () => ({ theme, resolvedTheme, setTheme }),
    [theme, resolvedTheme, setTheme],
  )

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}

export function useTheme() {
  const context = useContext(ThemeContext)
  if (!context) {
    // Every consumer is inside the provider, so this is a wiring mistake rather
    // than a runtime condition to handle.
    throw new Error('useTheme must be used inside a ThemeProvider')
  }
  return context
}
