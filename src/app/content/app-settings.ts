/**
 * Settings for the registration, diagnostic and admin pages. None of these are secrets: they are all visible in the
 * browser. Values marked TODO must be filled in before going live.
 */
export const appSettings = {
  /**
   * Base URL of the API, without a trailing slash, e.g. https://api.engineeredtutors.co.za.
   * Leave empty to call /api on the same origin (local development uses proxy.conf.json).
   */
  apiBaseUrl: '', // TODO: API URL once deployed

  /** Cloudflare Turnstile site key (REG-06). Empty hides the widget; the API only allows that in Development. */
  turnstileSiteKey: '', // TODO: Turnstile site key

  /**
   * Entra ID sign-in for the admin area (AD-01). Empty clientId means local development: the admin page asks for the
   * API's AdminAuth__DevKey instead.
   */
  entra: {
    clientId: '', // TODO: SPA app registration (client) ID
    tenantId: '', // TODO: directory (tenant) ID
    /** Scope exposed by the API app registration, e.g. api://<api-client-id>/access_as_admin */
    apiScope: '', // TODO
  },
};
