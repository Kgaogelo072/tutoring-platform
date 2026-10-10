import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom, from, switchMap, tap } from 'rxjs';
import type { AccountInfo, PublicClientApplication } from '@azure/msal-browser';
import { appSettings } from '../content/app-settings';
import { apiUrl } from '../core/api';

const DEV_KEY_STORAGE = 'et-admin-dev-key';

/**
 * Admin sign-in (AD-01). With Entra settings filled in, uses MSAL (redirect flow) and sends a bearer token for the
 * API scope; the API only accepts the tutor's account. Without them (local development) it asks for the API's
 * AdminAuth__DevKey, which the API only honours in Development.
 */
@Injectable({ providedIn: 'root' })
export class AdminAuth {
  private readonly http = inject(HttpClient);
  readonly mode: 'entra' | 'dev' = appSettings.entra.clientId ? 'entra' : 'dev';
  readonly ready = signal(false);
  readonly signedIn = signal(false);
  readonly userName = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  private msal?: PublicClientApplication;
  private account?: AccountInfo;
  private initPromise?: Promise<void>;

  init(): Promise<void> {
    this.initPromise ??= this.doInit().finally(() => this.ready.set(true));
    return this.initPromise;
  }

  private async doInit() {
    if (this.mode === 'dev') {
      if (this.devKey()) await this.verify();
      return;
    }
    const { PublicClientApplication } = await import('@azure/msal-browser');
    this.msal = new PublicClientApplication({
      auth: {
        clientId: appSettings.entra.clientId,
        authority: `https://login.microsoftonline.com/${appSettings.entra.tenantId}`,
        redirectUri: `${location.origin}/admin`,
      },
      cache: { cacheLocation: 'sessionStorage' },
    });
    await this.msal.initialize();
    try {
      const result = await this.msal.handleRedirectPromise();
      this.account = result?.account ?? this.msal.getAllAccounts()[0];
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Sign-in failed.');
    }
    if (this.account) {
      this.msal.setActiveAccount(this.account);
      await this.verify();
    }
  }

  async signIn(devKey?: string) {
    this.error.set(null);
    if (this.mode === 'dev') {
      try {
        sessionStorage.setItem(DEV_KEY_STORAGE, devKey ?? '');
      } catch {
        /* ignore */
      }
      await this.verify();
      if (!this.signedIn()) this.error.set('That key was not accepted.');
      return;
    }
    await this.msal?.loginRedirect({ scopes: [appSettings.entra.apiScope] });
  }

  async signOut() {
    this.signedIn.set(false);
    this.userName.set(null);
    if (this.mode === 'dev') {
      try {
        sessionStorage.removeItem(DEV_KEY_STORAGE);
      } catch {
        /* ignore */
      }
      return;
    }
    await this.msal?.logoutRedirect({ account: this.account, postLogoutRedirectUri: `${location.origin}/` });
  }

  /** Headers for an admin API call. */
  async headers(): Promise<Record<string, string>> {
    if (this.mode === 'dev') {
      const key = this.devKey();
      return key ? { 'X-Dev-Admin-Key': key } : {};
    }
    if (!this.msal || !this.account) return {};
    try {
      const r = await this.msal.acquireTokenSilent({ scopes: [appSettings.entra.apiScope], account: this.account });
      return { Authorization: `Bearer ${r.accessToken}` };
    } catch {
      await this.msal.acquireTokenRedirect({ scopes: [appSettings.entra.apiScope], account: this.account });
      return {};
    }
  }

  /** Confirms with the API that this account is allowed in. */
  private async verify() {
    try {
      const me = await firstValueFrom(this.http.get<{ name: string | null }>(apiUrl('/admin/me')));
      this.userName.set(me.name);
      this.signedIn.set(true);
    } catch (e) {
      this.signedIn.set(false);
      if (e instanceof HttpErrorResponse && e.status === 403) this.error.set('This account is not allowed into admin.');
      else if (e instanceof HttpErrorResponse && e.status !== 401) this.error.set("Couldn't reach the API.");
    }
  }

  private devKey(): string | null {
    try {
      return sessionStorage.getItem(DEV_KEY_STORAGE);
    } catch {
      return null;
    }
  }

  /** Called by the interceptor when the API rejects our credentials. */
  expired() {
    this.signedIn.set(false);
  }
}

/** Adds admin credentials to /api/admin calls only, never to public or third-party requests. */
export const adminAuthInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/admin/')) return next(req);
  const auth = inject(AdminAuth);
  return from(auth.headers()).pipe(
    switchMap((headers) => next(req.clone({ setHeaders: headers }))),
    tap({
      error: (e) => {
        if (e instanceof HttpErrorResponse && e.status === 401 && !req.url.endsWith('/admin/me')) auth.expired();
      },
    }),
  );
};
