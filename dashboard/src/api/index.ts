import { anonymousTokenProvider, type AccessTokenProvider } from './auth'
import { createHttpClient, type HttpClient } from './http'
import { createResources, type ApiResources } from './resources'
import type { ApiClientOptions } from './config'

export interface ApiClient {
  http: HttpClient
  resources: ApiResources
}

export interface CreateApiClientOptions extends ApiClientOptions {
  tokenProvider?: AccessTokenProvider
  /** Invoked when the API answers 401. */
  onUnauthorized?: () => void
}

/**
 * Builds the dashboard's API client.
 *
 * Authentication is injected rather than hard-coded: pass a token provider once
 * the strategy is settled (see `auth.ts`).
 */
export function createApiClient(options: CreateApiClientOptions = {}): ApiClient {
  const { tokenProvider = anonymousTokenProvider, onUnauthorized, ...clientOptions } = options

  const http = createHttpClient(tokenProvider, clientOptions)
  // Bind the unauthorized hook into every request via the transport.
  const boundHttp: HttpClient = {
    request: (path, requestOptions) =>
      http.request(path, { ...requestOptions, onUnauthorized }),
    get: (path, requestOptions) => http.get(path, { ...requestOptions, onUnauthorized }),
    post: (path, body, requestOptions) => http.post(path, body, { ...requestOptions, onUnauthorized }),
    put: (path, body, requestOptions) => http.put(path, body, { ...requestOptions, onUnauthorized }),
    patch: (path, body, requestOptions) => http.patch(path, body, { ...requestOptions, onUnauthorized }),
    delete: (path, requestOptions) => http.delete(path, { ...requestOptions, onUnauthorized }),
  }

  return { http: boundHttp, resources: createResources(boundHttp) }
}

export { ApiError, type FieldErrors, type NormalizedApiError } from './errors'
export { anonymousTokenProvider, type AccessTokenProvider } from './auth'
export { getApiBaseUrl } from './config'
export { createHttpClient, type HttpClient, type RequestOptions, type QueryParams } from './http'
export { createResources, type ApiResources, type PageParams, type RouteListParams } from './resources'
export * from './types'
