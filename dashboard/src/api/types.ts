/*
 * Generated to mirror src/BitWrite.OcelotControl.Api/DTOs. Do not hand-edit.
 *
 * The API serialises with JsonSerializerDefaults.Web (camelCase), so property
 * names are camelCased here. DateTimeOffset is a string on the wire (ISO 8601).
 *
 * Regenerate when a DTO changes; the shapes are asserted in the client tests.
 */

export interface AuditListResponse {
  audits: AuditResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface AuditResponse {
  id: string
  actor: string
  action: string
  resourceType: string
  resourceId: string
  result: string
  timestamp: string
}

export interface AuthenticationOptionsResponse {
  allowedScopes: string[]
}

export interface CacheOptionsResponse {
  ttlSeconds: number
}

export interface CurrentPublicationResponse {
  current: PublicationResponse | null
  history: PublicationResponse[]
}

export interface DownstreamTargetResponse {
  host: string
  port: number
  scheme: string
  path: string
}

export interface ErrorResponse {
  correlationId: string
  error: string
  type: string
  details: unknown | null
}

export interface GatewayDeploymentStateResponse {
  gatewayId: string
  status: string
  receivedAt: string | null
  validatedAt: string | null
  appliedAt: string | null
  healthyAt: string | null
  isValid: boolean | null
  failedAt: string | null
  failureReason: string | null
}

export interface GatewayListResponse {
  gateways: GatewayResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface GatewayResponse {
  id: string
  name: string
  description: string | null
  status: string
  createdAt: string
  updatedAt: string
}

export interface GlobalConfigurationResponse {
  id: string
  baseUrl: string | null
  requestIdKey: string | null
  downstreamScheme: string | null
  timeout: number | null
  rateLimit: RateLimitConfigResponse | null
  qoS: QoSConfigResponse | null
  httpHandler: HttpHandlerConfigResponse | null
  serviceDiscovery: ServiceDiscoveryConfigResponse | null
  updatedAt: string
}

export interface HttpHandlerConfigResponse {
  useProxy: boolean
  expect100Continue: boolean
  maxConnectionsPerServer: number | null
}

export interface LicenseListResponse {
  licenses: LicenseResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface LicenseResponse {
  id: string
  name: string
  productCode: string
  status: string
  expirationDate: string
  createdAt: string
  updatedAt: string
  isActive: boolean
  activatedAt: string
}

export interface LoadBalancerOptionsResponse {
  algorithm: string
}

export interface PluginListResponse {
  plugins: PluginResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface PluginResponse {
  id: string
  name: string
  version: string
  description: string | null
  scope: string
  isEnabled: boolean
  installedAt: string
  lastUpdated: string | null
  lastEnabled: string | null
  lastDisabled: string | null
}

export interface PublicationListResponse {
  publications: PublicationResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface PublicationResponse {
  id: string
  snapshotVersion: number
  status: string
  initiatedBy: string
  startedAt: string
  completedAt: string | null
  failureReason: string | null
  gatewayStates: GatewayDeploymentStateResponse[]
}

export interface QoSConfigResponse {
  timeoutValue: number
  durationOfBreak: number
}

export interface QoSOptionsResponse {
  timeoutSeconds: number
  circuitBreakerTimeoutSeconds: number | null
}

export interface RateLimitConfigResponse {
  enableRateLimiting: boolean
  httpStatusCode: string | null
}

export interface RateLimitOptionsResponse {
  enableRateLimiting: boolean
  period: string
  limit: number
}

export interface ReconcileResponse {
  gatewayId: string
  targetVersion: number
  success: boolean
  errorMessage: string | null
  reconciledAt: string
}

export interface RouteEffectiveResponse {
  ocelotJson: string
}

export interface RouteHistoryItem {
  timestamp: string
  action: string
  changedBy: string | null
  details: string | null
}

export interface RouteHistoryResponse {
  history: RouteHistoryItem[]
}

export interface RouteListResponse {
  routes: RouteResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface RoutePreviewResponse {
  ocelotJson: string
}

export interface RouteResponse {
  id: string
  key: string
  method: string
  upstreamPath: string
  host: string | null
  serviceId: string
  isEnabled: boolean
  downstreamTargets: DownstreamTargetResponse[]
  authenticationOptions: AuthenticationOptionsResponse | null
  rateLimitOptions: RateLimitOptionsResponse | null
  qoSOptions: QoSOptionsResponse | null
  cacheOptions: CacheOptionsResponse | null
  loadBalancerOptions: LoadBalancerOptionsResponse | null
  createdAt: string
  updatedAt: string
}

export interface RouteValidationResponse {
  isValid: boolean
  errors: string[]
}

export interface RuntimeGatewaysResponse {
  gateways: RuntimeStatusResponse[]
}

export interface RuntimeStatusResponse {
  gatewayId: string
  status: string
  currentVersion: number | null
  targetVersion: number | null
  lastHeartbeat: string | null
  lastSynchronized: string | null
  lastConfigApplied: string | null
  runtimeInfo: Record<string, string>
  capabilities: string[]
  activeRoutes: string[]
}

export interface ServiceDiscoveryConfigResponse {
  provider: string | null
  host: string | null
  port: number | null
  type: string | null
  configuration: Record<string, string>
}

export interface ServiceListResponse {
  services: ServiceResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ServiceResponse {
  id: string
  name: string
  description: string | null
  downstreamTargets: DownstreamTargetResponse[]
  createdAt: string
  updatedAt: string
}

export interface ServiceRoutesResponse {
  routes: RouteResponse[]
}

export interface SnapshotCompareResponse {
  versionA: number
  versionB: number
  differences: string[]
}

export interface SnapshotDeploymentResponse {
  publicationId: string
  snapshotVersion: number
  status: string
  startedAt: string
  completedAt: string | null
  failureReason: string | null
}

export interface SnapshotListResponse {
  snapshots: SnapshotResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface SnapshotResponse {
  version: number
  hash: string
  content: string
  status: string
  createdBy: string
  createdAt: string
  publishedAt: string | null
  archivedAt: string | null
}

export interface SnapshotValidationResponse {
  isValid: boolean
  errors: string[]
}

export interface ValidationErrorResponse {
  correlationId: string
  error: string
  type: string
  details: Record<string, string>
}
