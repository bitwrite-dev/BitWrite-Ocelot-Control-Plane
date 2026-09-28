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

export interface GatewayResponse {
  id: string
  name: string
  description: string | null
  /**
   * A label the control plane records, not live health.
   *
   * The API accepts one of `Disconnected`, `Connecting`, `Synchronized`,
   * `Applying`, `Active` or `Degraded`. There is no "shut down" state: the only
   * lifecycle rule is that a gateway which has received a publication cannot be
   * deleted.
   */
  status: string
  createdAt: string
  updatedAt: string
}

export interface GatewayListResponse {
  gateways: GatewayResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface GatewayRequest {
  name: string
  description: string | null
}

/**
 * Whether a gateway can be deleted, and why not if it cannot.
 */
export interface GatewayDeletionEligibility {
  gatewayId: string
  hasBeenPublishedTo: boolean
  /** Why deletion is refused, or null when it is allowed. */
  reason: string | null
}

/**
 * The system settings, and what a first-run screen needs to render itself.
 */
export interface SystemSettingsResponse {
  id: string
  /** Null until a version is chosen, which is the first-run state. */
  ocelotVersion: string | null
  ocelotVersionSelectedAt: string | null
  ocelotVersionSelectedBy: string | null
  pollIntervalSeconds: number
  auditLogRetentionDays: number
  snapshotRetentionCount: number
  /** True once a version has been chosen and setup is complete. */
  isInitialised: boolean
  /**
   * Versions that can be chosen, newest first.
   *
   * Derived from what the API can actually emit, not from what Ocelot has
   * released. Offering a version the builder cannot shape would produce a file
   * no gateway could read.
   */
  availableOcelotVersions: string[]
  createdAt: string
  updatedAt: string
}

/**
 * The first-run request. The version is required here and has no update path.
 */
export interface CompleteFirstRunRequest {
  ocelotVersion: string
  pollIntervalSeconds?: number | null
  auditLogRetentionDays?: number | null
  snapshotRetentionCount?: number | null
}

/**
 * The settings that can change after setup.
 *
 * There is no `ocelotVersion` field, and that is deliberate: the version is
 * permanent, so this request must not offer a way to reach it.
 */
export interface UpdateSystemSettingsRequest {
  /** `null` clears the field. Omitting the key would leave it unchanged. */
  pollIntervalSeconds?: number | null
  auditLogRetentionDays?: number | null
  snapshotRetentionCount?: number | null
}

export interface AuthorizationOptionsResponse {
  policies: string[] | null
  scopes: string[] | null
  requirements: Record<string, string> | null
}

export interface TransformEntryResponse {
  key: string
  value: string
}

/** Add becomes DownstreamHeaderTransform, transform becomes UpstreamHeaderTransform. */
export interface TransformationsResponse {
  add: TransformEntryResponse[] | null
  transform: TransformEntryResponse[] | null
}

/** How the gateway's HTTP client behaves when calling downstream. */
export interface HttpClientOptionsResponse {
  allowAutoRedirect: boolean
  maxConnectionsPerServer: number
  pooledConnectionLifetimeSeconds: number
  useCookieContainer: boolean
  useProxy: boolean
  useTracing: boolean
}

export interface RouteResponse {
  id: string
  key: string
  method: string
  upstreamPath: string
  /**
   * The upstream host a route is restricted to.
   *
   * The API returned this while nothing ever read or emitted it, so a host set
   * on a route did nothing. See #485.
   */
  host: string | null
  /** Higher is matched first among overlapping routes. */
  priority: number
  routeIsCaseSensitive: boolean
  /**
   * The verb the request is rewritten to on the way downstream.
   *
   * Null keeps the upstream verb. Ocelot takes a single string here, not a
   * list, unlike the upstream verb. See #485.
   */
  downstreamMethod: string | null
  /** "1.0", "1.1" or "2.0". Null leaves the framework default in place. */
  downstreamHttpVersion: string | null
  /** How strictly the version is asked for. Needs a version to apply to. */
  downstreamHttpVersionPolicy: string | null
  /**
   * Accepts any downstream TLS certificate.
   *
   * Ocelot's own documentation calls this a security risk. For self-signed
   * certificates in local development only.
   */
  dangerousAcceptAnyServerCertificateValidator: boolean
  /** Handlers registered in the gateway. An unknown name stops it starting. */
  delegatingHandlers: string[]
  httpClientOptions: HttpClientOptionsResponse | null
  /** Seconds to wait for a downstream response, separate from the QoS timeout. */
  timeoutSeconds: number | null
  /** The path the request is rewritten to, or null to forward it unchanged. */
  downstreamPathTemplate: string | null
  headerTransformations: TransformationsResponse | null
  claimTransformations: TransformationsResponse | null
  queryTransformations: TransformationsResponse | null
  /**
   * Who may call this route, as distinct from who they are.
   *
   * Separate from `authenticationOptions.allowedScopes`: a caller can be
   * authenticated and still not be allowed here.
   */
  authorizationOptions: AuthorizationOptionsResponse | null
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

/**
 * One validation failure, tagged with the request field that caused it.
 *
 * `field` is null when the problem belongs to the route as a whole, and uses the
 * API's field names otherwise (`upstreamPath`, `downstreamTargets[0]`, ...), so
 * a wizard can send the operator back to the step that owns it.
 */
export interface RouteValidationError {
  field: string | null
  code: string
  message: string
}

export interface RouteValidationResponse {
  isValid: boolean
  errors: RouteValidationError[]
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
