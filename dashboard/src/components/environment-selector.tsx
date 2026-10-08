import { useState, useEffect } from 'react'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

const STORAGE_KEY = 'dashboard:environment'

/**
 * Runtime environment selector.
 *
 * The control plane stores routes, services and snapshots per environment,
 * so every request must name the environment it operates on via the
 * `X-Environment` header. This selector persists the choice in localStorage
 * so it survives page reloads.
 */
export function EnvironmentSelector() {
  const [environment, setEnvironment] = useState<string>('development')

  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) setEnvironment(stored)
  }, [])

  const handleChange = (value: string) => {
    setEnvironment(value)
    localStorage.setItem(STORAGE_KEY, value)
    // Force a page reload so the HTTP client picks up the new environment
    window.location.reload()
  }

  return (
    <div className="mx-3 mt-4">
      <label htmlFor="environment-select" className="mb-1 block text-xs font-medium text-muted-foreground">
        Environment
      </label>
      <Select value={environment} onValueChange={handleChange}>
        <SelectTrigger id="environment-select" className="w-full">
          <SelectValue placeholder="Select environment" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="development">Development</SelectItem>
          <SelectItem value="staging">Staging</SelectItem>
          <SelectItem value="production">Production</SelectItem>
        </SelectContent>
      </Select>
      <p className="mt-1 text-xs text-muted-foreground">
        Select the environment to operate on. Reloads on change.
      </p>
    </div>
  )
}