import type { AccessTokenProvider } from './auth'
import { anonymousTokenProvider } from './auth'
import { getApiBaseUrl, type ApiClientOptions } from './config'
import { ApiError, normalizeError, normalizeNetworkError } from './errors'

export type QueryParams = Record<
  string,
  string | number | boolean | undefined | null
>

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  query?: QueryParams
  /** Serialized as a JSON body. `undefined` sends no body. */
  body?: unknown
  signal?: AbortSignal
  /** Called when the API answers 401. Left to the app to decide what to do. */
  onUnauthorized?: () => void
}

function buildUrl(baseUrl: string, path: string, query?: QueryParams): string {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  const url = new URL(`${baseUrl}${normalizedPath}`, 'http://placeholder.invalid')

  for (const [key, value] of Object.entries(query ?? {})) {
    if (value === undefined || value === null || value === '') continue
    url.searchParams.set(key, String(value))
  }

  // `URL` needs an absolute base to parse, but the app may be using a relative
  // base, so rebuild the path+search by hand.
  return `${baseUrl}${normalizedPath}${url.search}`
}

/** Parses a body as JSON, tolerating an empty or non-JSON payload. */
async function readBody(response: Response): Promise<unknown> {
  const text = await response.text()
  if (text.length === 0) return undefined
  try {
    return JSON.parse(text) as unknown
  } catch {
    return text
  }
}

export interface HttpClient {
  request<T>(path: string, options?: RequestOptions): Promise<T>
  get<T>(path: string, options?: Omit<RequestOptions, 'method' | 'body'>): Promise<T>
  post<T>(path: string, body?: unknown, options?: Omit<RequestOptions, 'method' | 'body'>): Promise<T>
  put<T>(path: string, body?: unknown, options?: Omit<RequestOptions, 'method' | 'body'>): Promise<T>
  patch<T>(path: string, body?: unknown, options?: Omit<RequestOptions, 'method' | 'body'>): Promise<T>
  delete<T>(path: string, options?: Omit<RequestOptions, 'method' | 'body'>): Promise<T>
}

export function createHttpClient(
  tokenProvider: AccessTokenProvider = anonymousTokenProvider,
  options: ApiClientOptions = {},
): HttpClient {
  const baseUrl = options.baseUrl ?? getApiBaseUrl()
  const doFetch = options.fetchImpl ?? globalThis.fetch.bind(globalThis)

  async function request<T>(path: string, requestOptions: RequestOptions = {}): Promise<T> {
    const { method = 'GET', query, body, signal, onUnauthorized } = requestOptions

    const headers: Record<string, string> = { Accept: 'application/json' }
    if (body !== undefined) headers['Content-Type'] = 'application/json'

    const token = tokenProvider.getAccessToken()
    if (token) headers.Authorization = `Bearer ${token}`

    let response: Response
    try {
      response = await doFetch(buildUrl(baseUrl, path, query), {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        signal,
      })
    } catch (cause) {
      // AbortError is a deliberate caller-initiated cancellation, so it is
      // rethrown untouched rather than dressed up as a network failure.
      if (cause instanceof DOMException && cause.name === 'AbortError') throw cause
      throw new ApiError(normalizeNetworkError(cause))
    }

    if (response.status === 401) onUnauthorized?.()

    const payload = await readBody(response)

    if (!response.ok) {
      throw new ApiError(
        normalizeError(
          response.status,
          payload,
          `${method} ${path} failed with ${response.status} ${response.statusText}`,
        ),
      )
    }

    return payload as T
  }

  const withoutBody = <T>(path: string, extra: Omit<RequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...extra, method: 'GET' })

  return {
    request,
    get: withoutBody,
    post: (path, body, extra) => request(path, { ...extra, method: 'POST', body }),
    put: (path, body, extra) => request(path, { ...extra, method: 'PUT', body }),
    patch: (path, body, extra) => request(path, { ...extra, method: 'PATCH', body }),
    delete: (path, extra) => request(path, { ...extra, method: 'DELETE' }),
  }
}
