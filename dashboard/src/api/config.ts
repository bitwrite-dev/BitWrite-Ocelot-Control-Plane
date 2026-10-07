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
 * Origin the API is served from, as seen by the browser.
 *
 * Defaults to an **empty string**, meaning "same origin". Resource paths are
 * absolute API paths (`/api/v1/...`), so the browser requests
 * `/api/v1/...` directly and the Vite dev server's `/api` proxy picks it up.
 *
 * This must stay an origin and never a path prefix. A prefix here would
 * concatenate with the `/api/...` already present in every resource path and
 * produce `/api/api/v1/...`. Set an absolute origin such as
 * `https://api.example.com` only for a cross-origin deployment.
 */
export function getApiBaseUrl(): string {
  return readString(import.meta.env.VITE_API_BASE_URL, '').replace(/\/+$/, '')
}

/**
 * The environment every request operates on, sent as `X-Environment`.
 *
 * The control plane stores routes, services and snapshots per environment, so a
 * request that names none is rejected rather than answered with some default's data.
 * That is why there is no fallback here: an unset value means the request goes out
 * without the header and the API says so, instead of the dashboard quietly showing
 * one environment's routes under another's name.
 *
 * Set `VITE_API_ENVIRONMENT` in `.env.local`. The API applies published snapshots
 * from the environment it is configured with, which has to be the same name.
 */
export function getApiEnvironment(): string {
  return readString(import.meta.env.VITE_API_ENVIRONMENT, '')
}

export interface ApiClientOptions {
  /** Overrides the base URL. Mainly for tests. */
  baseUrl?: string
  /** Overrides the environment sent as `X-Environment`. Mainly for tests. */
  environment?: string
  /** Injected in tests. Defaults to the global `fetch`. */
  fetchImpl?: typeof fetch
}
