import type { HttpClient, RequestOptions } from './http'
import type {
  AuditListResponse,
  AuditResponse,
  GatewayListResponse,
  GatewayResponse,
  GlobalConfigurationResponse,
  LicenseListResponse,
  LicenseResponse,
  PluginListResponse,
  PluginResponse,
  PublicationListResponse,
  PublicationResponse,
  CurrentPublicationResponse,
  ReconcileResponse,
  RouteListResponse,
  RouteResponse,
  RuntimeGatewaysResponse,
  RuntimeStatusResponse,
  ServiceListResponse,
  ServiceResponse,
  ServiceRoutesResponse,
  SnapshotCompareResponse,
  SnapshotDeploymentResponse,
  SnapshotListResponse,
  SnapshotResponse,
  SnapshotValidationResponse,
} from './types'

/** Shared paging parameters accepted by the list endpoints. */
export interface PageParams {
  page?: number
  pageSize?: number
  search?: string
  signal?: AbortSignal
}

type Options = Omit<RequestOptions, 'method' | 'body'>

export interface RouteListParams extends PageParams {
  serviceId?: string
  isEnabled?: boolean
}

export function createResources(http: HttpClient) {
  const routes = {
    list: ({ signal, ...query }: RouteListParams = {}) =>
      http.get<RouteListResponse>('/api/v1/routes', { query, signal }),
    get: (id: string, o?: Options) => http.get<RouteResponse>(`/api/v1/routes/${id}`, o),
    create: (body: unknown, o?: Options) => http.post<RouteResponse>('/api/v1/routes', body, o),
    update: (id: string, body: unknown, o?: Options) =>
      http.put<RouteResponse>(`/api/v1/routes/${id}`, body, o),
    enable: (id: string, o?: Options) => http.patch<RouteResponse>(`/api/v1/routes/${id}/enable`, {}, o),
    disable: (id: string, o?: Options) => http.patch<RouteResponse>(`/api/v1/routes/${id}/disable`, {}, o),
    remove: (id: string, o?: Options) => http.delete<void>(`/api/v1/routes/${id}`, o),
    validate: (id: string, o?: Options) =>
      http.post<SnapshotValidationResponse>(`/api/v1/routes/${id}/validate`, {}, o),
    preview: (id: string, o?: Options) => http.get<string>(`/api/v1/routes/${id}/preview`, o),
    effective: (id: string, o?: Options) => http.get<string>(`/api/v1/routes/${id}/effective`, o),
    history: (id: string, o?: Options) => http.get<unknown[]>(`/api/v1/routes/${id}/history`, o),
  }

  const services = {
    list: ({ signal, ...query }: PageParams = {}) =>
      http.get<ServiceListResponse>('/api/v1/services', { query, signal }),
    get: (id: string, o?: Options) => http.get<ServiceResponse>(`/api/v1/services/${id}`, o),
    create: (body: unknown, o?: Options) => http.post<ServiceResponse>('/api/v1/services', body, o),
    update: (id: string, body: unknown, o?: Options) =>
      http.put<ServiceResponse>(`/api/v1/services/${id}`, body, o),
    remove: (id: string, o?: Options) => http.delete<void>(`/api/v1/services/${id}`, o),
    routes: (id: string, o?: Options) =>
      http.get<ServiceRoutesResponse>(`/api/v1/services/${id}/routes`, o),
  }

  const gateways = {
    list: ({ signal, ...query }: PageParams = {}) =>
      http.get<GatewayListResponse>('/api/v1/gateways', { query, signal }),
    get: (id: string, o?: Options) => http.get<GatewayResponse>(`/api/v1/gateways/${id}`, o),
    create: (body: unknown, o?: Options) => http.post<GatewayResponse>('/api/v1/gateways', body, o),
    update: (id: string, body: unknown, o?: Options) =>
      http.put<GatewayResponse>(`/api/v1/gateways/${id}`, body, o),
    updateStatus: (id: string, body: unknown, o?: Options) =>
      http.patch<GatewayResponse>(`/api/v1/gateways/${id}/status`, body, o),
  }

  const snapshots = {
    list: ({ signal, ...query }: PageParams = {}) =>
      http.get<SnapshotListResponse>('/api/v1/snapshots', { query, signal }),
    get: (version: number, o?: Options) => http.get<SnapshotResponse>(`/api/v1/snapshots/${version}`, o),
    create: (body: unknown, o?: Options) => http.post<SnapshotResponse>('/api/v1/snapshots', body, o),
    validate: (body: unknown, o?: Options) =>
      http.post<SnapshotValidationResponse>('/api/v1/snapshots/validate', body, o),
    compare: (version: number, o?: Options) =>
      http.get<SnapshotCompareResponse>(`/api/v1/snapshots/${version}/compare`, o),
    clone: (version: number, body: unknown, o?: Options) =>
      http.post<SnapshotResponse>(`/api/v1/snapshots/${version}/clone`, body, o),
    export: (version: number, o?: Options) =>
      http.get<string>(`/api/v1/snapshots/${version}/export`, o),
    publish: (version: number, body: unknown, o?: Options) =>
      http.post<PublicationResponse>(`/api/v1/snapshots/${version}/publish`, body, o),
    rollback: (version: number, body: unknown, o?: Options) =>
      http.post<unknown>(`/api/v1/snapshots/${version}/rollback`, body, o),
    deployment: (version: number, o?: Options) =>
      http.get<SnapshotDeploymentResponse>(`/api/v1/snapshots/${version}/deployment`, o),
  }

  const publications = {
    list: ({ signal, ...query }: PageParams = {}) =>
      http.get<PublicationListResponse>('/api/v1/publications', { query, signal }),
    current: (o?: Options) =>
      http.get<CurrentPublicationResponse>('/api/v1/publications/current', o),
    history: (o?: Options) => http.get<unknown[]>('/api/v1/publications/history', o),
  }

  const plugins = {
    list: ({ signal, ...query }: PageParams = {}) =>
      http.get<PluginListResponse>('/api/v1/plugins', { query, signal }),
    get: (id: string, o?: Options) => http.get<PluginResponse>(`/api/v1/plugins/${id}`, o),
    install: (body: unknown, o?: Options) => http.post<PluginResponse>('/api/v1/plugins', body, o),
    enable: (id: string, o?: Options) => http.patch<PluginResponse>(`/api/v1/plugins/${id}/enable`, {}, o),
    disable: (id: string, o?: Options) => http.patch<PluginResponse>(`/api/v1/plugins/${id}/disable`, {}, o),
    upgrade: (id: string, body: unknown, o?: Options) =>
      http.put<PluginResponse>(`/api/v1/plugins/${id}/upgrade`, body, o),
    uninstall: (id: string, o?: Options) => http.delete<void>(`/api/v1/plugins/${id}`, o),
  }

  const licenses = {
    list: ({ signal, ...query }: PageParams = {}) =>
      http.get<LicenseListResponse>('/api/v1/licenses', { query, signal }),
    get: (id: string, o?: Options) => http.get<LicenseResponse>(`/api/v1/licenses/${id}`, o),
    create: (body: unknown, o?: Options) => http.post<LicenseResponse>('/api/v1/licenses', body, o),
    update: (id: string, body: unknown, o?: Options) =>
      http.put<LicenseResponse>(`/api/v1/licenses/${id}`, body, o),
    activate: (body: unknown, o?: Options) => http.post<LicenseResponse>('/api/v1/licenses/activate', body, o),
    renew: (id: string, body: unknown, o?: Options) =>
      http.put<LicenseResponse>(`/api/v1/licenses/${id}/renew`, body, o),
    revoke: (id: string, o?: Options) => http.delete<void>(`/api/v1/licenses/${id}/revoke`, o),
  }

  const audit = {
    list: ({ signal, ...query }: PageParams = {}) =>
      http.get<AuditListResponse>('/api/v1/audit', { query, signal }),
    get: (id: string, o?: Options) => http.get<AuditResponse>(`/api/v1/audit/${id}`, o),
    stats: (o?: Options) => http.get<unknown>('/api/v1/audit/stats', o),
  }

  const runtime = {
    gateways: (o?: Options) => http.get<RuntimeGatewaysResponse>('/api/v1/runtime/gateways', o),
    gateway: (id: string, o?: Options) =>
      http.get<RuntimeStatusResponse>(`/api/v1/runtime/gateways/${id}`, o),
    reconcile: (body: unknown, o?: Options) =>
      http.post<ReconcileResponse>('/api/v1/runtime/reconcile', body, o),
  }

  const globalConfiguration = {
    get: (o?: Options) => http.get<GlobalConfigurationResponse>('/api/v1/global-configuration', o),
    update: (body: unknown, o?: Options) =>
      http.put<GlobalConfigurationResponse>('/api/v1/global-configuration', body, o),
  }

  return {
    routes,
    services,
    gateways,
    snapshots,
    publications,
    plugins,
    licenses,
    audit,
    runtime,
    globalConfiguration,
  }
}

export type ApiResources = ReturnType<typeof createResources>
