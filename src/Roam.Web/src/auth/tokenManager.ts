/**
 * Owns the access/refresh token pair for the web client.
 *
 * - Tokens live in sessionStorage (never localStorage).
 * - Access tokens are refreshed shortly before they expire, so long-lived SignalR connections can
 *   reconnect with a valid token even after the page sat idle.
 * - Concurrent callers (several requests hitting 401 at once, hub reconnects) share one in-flight
 *   refresh instead of each sending their own /refresh request.
 * - The session is only ended when the server rejects the refresh token; a network blip keeps the user
 *   signed in instead of silently logging them out.
 */

export interface StorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export interface TokenResponse {
  accessToken?: string;
  token?: string;
  refreshToken?: string;
  expiresIn?: number;
}

const ACCESS_KEY = 'roam_token';
const REFRESH_KEY = 'roam_refresh_token';
const EXPIRES_KEY = 'roam_token_expires_at';
const LEGACY_KEYS = [ACCESS_KEY, REFRESH_KEY];

/** Refresh this long before the access token actually expires. */
export const REFRESH_SKEW_MS = 60_000;

export type RefreshOutcome = 'refreshed' | 'rejected' | 'unavailable';

export class TokenManager {
  private refreshInFlight: Promise<RefreshOutcome> | null = null;
  private readonly listeners = new Set<() => void>();
  private readonly apiBase: string;
  private readonly storage: StorageLike;
  private readonly fetchImpl: typeof fetch;
  private readonly now: () => number;

  constructor(
    apiBase: string,
    storage: StorageLike,
    fetchImpl: typeof fetch = (...args) => fetch(...args),
    now: () => number = () => Date.now(),
    legacyStorage?: StorageLike,
  ) {
    this.apiBase = apiBase;
    this.storage = storage;
    this.fetchImpl = fetchImpl;
    this.now = now;
    // Purge tokens persisted by older builds in localStorage.
    LEGACY_KEYS.forEach((k) => legacyStorage?.removeItem(k));
  }

  get accessToken(): string | null {
    return this.storage.getItem(ACCESS_KEY);
  }

  get isAuthenticated(): boolean {
    return !!this.accessToken || !!this.storage.getItem(REFRESH_KEY);
  }

  /** Subscribe to sign-in / refresh / sign-out changes. Returns an unsubscribe function. */
  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  /** Stores tokens from a /login or /refresh response. Returns false if no access token was present. */
  setTokens(data: TokenResponse): boolean {
    const access = data.accessToken || data.token;
    if (!access) return false;
    this.storage.setItem(ACCESS_KEY, access);
    if (data.refreshToken) this.storage.setItem(REFRESH_KEY, data.refreshToken);
    if (typeof data.expiresIn === 'number' && data.expiresIn > 0) {
      this.storage.setItem(EXPIRES_KEY, String(this.now() + data.expiresIn * 1000));
    } else {
      this.storage.removeItem(EXPIRES_KEY);
    }
    this.emit();
    return true;
  }

  clear(): void {
    const hadTokens = this.isAuthenticated;
    [ACCESS_KEY, REFRESH_KEY, EXPIRES_KEY].forEach((k) => this.storage.removeItem(k));
    if (hadTokens) this.emit();
  }

  /** True when the access token is missing or within REFRESH_SKEW_MS of expiring. */
  isAccessTokenStale(): boolean {
    if (!this.accessToken) return true;
    const expiresAt = Number(this.storage.getItem(EXPIRES_KEY));
    if (!expiresAt) return false; // Unknown expiry: rely on 401 handling.
    return this.now() >= expiresAt - REFRESH_SKEW_MS;
  }

  /** Returns an access token that is not about to expire, refreshing first if needed. */
  async getValidAccessToken(): Promise<string | null> {
    if (this.isAccessTokenStale() && this.storage.getItem(REFRESH_KEY)) {
      await this.refresh();
    }
    return this.accessToken;
  }

  /** Refreshes the token pair once, even if called concurrently. */
  refresh(): Promise<RefreshOutcome> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.doRefresh().finally(() => {
        this.refreshInFlight = null;
      });
    }
    return this.refreshInFlight;
  }

  /** fetch() with the bearer token attached, refreshing proactively and retrying once on 401. */
  async authFetch(url: string, options: RequestInit = {}): Promise<Response> {
    const send = (token: string | null) => {
      const headers = new Headers(options.headers || {});
      if (token) headers.set('Authorization', `Bearer ${token}`);
      return this.fetchImpl(url, { ...options, headers });
    };

    const response = await send(await this.getValidAccessToken());
    if (response.status !== 401 || !this.storage.getItem(REFRESH_KEY)) return response;

    const outcome = await this.refresh();
    return outcome === 'refreshed' ? send(this.accessToken) : response;
  }

  private async doRefresh(): Promise<RefreshOutcome> {
    const refreshToken = this.storage.getItem(REFRESH_KEY);
    if (!refreshToken) {
      this.clear();
      return 'rejected';
    }

    let response: Response;
    try {
      response = await this.fetchImpl(`${this.apiBase}/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
    } catch {
      return 'unavailable'; // Offline / server unreachable: keep the session.
    }

    if (response.ok) {
      const data = (await response.json().catch(() => ({}))) as TokenResponse;
      if (this.setTokens(data)) return 'refreshed';
      return 'unavailable';
    }

    // 400/401/403: the refresh token is expired or was revoked (e.g. suspension). End the session.
    if (response.status >= 400 && response.status < 500 && response.status !== 429) {
      this.clear();
      return 'rejected';
    }
    return 'unavailable';
  }

  private emit(): void {
    this.listeners.forEach((l) => l());
  }
}
