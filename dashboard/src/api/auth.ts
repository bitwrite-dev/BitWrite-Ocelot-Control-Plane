/**
 * Access-token seam.
 *
 * The token strategy is **not yet decided** — see #433. The API configures JWT
 * bearer validation against an external authority but exposes no token-issuance
 * endpoint, so there is currently nothing for the dashboard to log in against.
 *
 * Deliberately, this module picks a side. The HTTP client depends only on
 * `AccessTokenProvider`, so whichever strategy is chosen slots in here without
 * touching `http.ts` or the resource modules.
 *
 * Two options on record:
 *  - A) add a local token endpoint (`POST /api/v1/auth/login`) to the API
 *  - B) implement OIDC/PKCE in the dashboard against an external IdP
 *
 * Whichever is chosen, implement the interface below and pass it to `createApiClient`.
 */

/** Supplies the bearer token for outgoing requests, if any. */
export interface AccessTokenProvider {
  /**
   * Returns the current access token, or `null` when unauthenticated.
   * Called before every request so a refreshed token is picked up.
   */
  getAccessToken(): string | null
}

/**
 * Default provider: sends no Authorization header.
 *
 * Fine while the API allows anonymous access, which is the case in development
 * today. It is NOT a credential store — a real implementation should keep the
 * token in memory and avoid persisting it to `localStorage`.
 */
export const anonymousTokenProvider: AccessTokenProvider = {
  getAccessToken: () => null,
}
