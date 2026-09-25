/**
 * Runtime configuration, sourced from Vite environment variables.
 *
 * `.env.example` documents these. Values are read through `import.meta.env` so
 * they are statically replaced at build time — do not read them dynamically.
 */

function readString(value: unknown, fallback: string): string {
  return typeof value === 'string' && value.length > 0 ? value : fallback
}

/**
 * Base URL for API calls, as seen from the browser.
 *
 * Defaults to the relative `/api`, which the Vite dev server proxies to the API
 * so the browser sees a single origin and CORS is not involved. Point this at an
 * absolute URL only for a cross-origin deployment.
 */
export function getApiBaseUrl(): string {
  const raw = readString(import.meta.env.VITE_API_BASE_URL, '/api')
  return raw.endsWith('/') ? raw.slice(0, -1) : raw
}

export interface ApiClientOptions {
  /** Overrides the base URL. Mainly for tests. */
  baseUrl?: string
  /** Injected in tests. Defaults to the global `fetch`. */
  fetchImpl?: typeof fetch
}
