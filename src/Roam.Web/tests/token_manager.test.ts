import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { TokenManager, REFRESH_SKEW_MS, type StorageLike } from '../src/auth/tokenManager.ts';

class MemoryStorage implements StorageLike {
  data = new Map<string, string>();
  getItem(k: string) { return this.data.has(k) ? this.data.get(k)! : null; }
  setItem(k: string, v: string) { this.data.set(k, v); }
  removeItem(k: string) { this.data.delete(k); }
}

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

function setup(handler: (url: string, init?: RequestInit) => Promise<Response>) {
  let now = 1_000_000;
  const calls: { url: string; auth: string | null }[] = [];
  const fetchImpl = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    calls.push({ url, auth: new Headers(init?.headers).get('Authorization') });
    return handler(url, init);
  }) as typeof fetch;
  const storage = new MemoryStorage();
  const manager = new TokenManager('https://api.test', storage, fetchImpl, () => now);
  return { manager, storage, calls, advance: (ms: number) => { now += ms; } };
}

describe('TokenManager', () => {
  it('refreshes proactively shortly before the access token expires', async () => {
    const { manager, calls, advance } = setup(async (url) =>
      url.endsWith('/refresh') ? json(200, { accessToken: 'access-2', refreshToken: 'refresh-2', expiresIn: 3600 }) : json(200, {}));
    manager.setTokens({ accessToken: 'access-1', refreshToken: 'refresh-1', expiresIn: 3600 });

    assert.equal(await manager.getValidAccessToken(), 'access-1');
    assert.equal(calls.length, 0, 'fresh token must not trigger a refresh');

    advance(3600_000 - REFRESH_SKEW_MS + 1);
    assert.equal(await manager.getValidAccessToken(), 'access-2');
    assert.equal(calls.filter((c) => c.url.endsWith('/refresh')).length, 1);
  });

  it('shares one refresh between concurrent callers', async () => {
    let refreshes = 0;
    const { manager } = setup(async (url) => {
      if (url.endsWith('/refresh')) {
        refreshes++;
        await new Promise((r) => setTimeout(r, 10));
        return json(200, { accessToken: 'access-2', refreshToken: 'refresh-2', expiresIn: 3600 });
      }
      return json(200, {});
    });
    manager.setTokens({ accessToken: 'access-1', refreshToken: 'refresh-1', expiresIn: 1 }); // already stale

    const tokens = await Promise.all([manager.getValidAccessToken(), manager.getValidAccessToken(), manager.refresh()]);
    assert.equal(refreshes, 1);
    assert.deepEqual(tokens.slice(0, 2), ['access-2', 'access-2']);
  });

  it('keeps the session when the refresh request fails due to the network', async () => {
    const { manager } = setup(async () => { throw new TypeError('Failed to fetch'); });
    manager.setTokens({ accessToken: 'access-1', refreshToken: 'refresh-1', expiresIn: 1 });

    assert.equal(await manager.refresh(), 'unavailable');
    assert.equal(manager.isAuthenticated, true);
  });

  it('ends the session when the server rejects the refresh token', async () => {
    const { manager } = setup(async () => json(401, {}));
    let notified = 0;
    manager.setTokens({ accessToken: 'access-1', refreshToken: 'refresh-1', expiresIn: 3600 });
    manager.subscribe(() => notified++);

    assert.equal(await manager.refresh(), 'rejected');
    assert.equal(manager.isAuthenticated, false);
    assert.equal(notified, 1);
  });

  it('authFetch retries once with a refreshed token after a 401', async () => {
    const { manager, calls } = setup(async (url, init) => {
      if (url.endsWith('/refresh')) return json(200, { accessToken: 'access-2', refreshToken: 'refresh-2', expiresIn: 3600 });
      const auth = new Headers(init?.headers).get('Authorization');
      return auth === 'Bearer access-2' ? json(200, { ok: true }) : json(401, {});
    });
    manager.setTokens({ accessToken: 'access-1', refreshToken: 'refresh-1', expiresIn: 3600 });

    const response = await manager.authFetch('https://api.test/api/v1/profile/me');
    assert.equal(response.status, 200);
    assert.deepEqual(calls.map((c) => c.auth), ['Bearer access-1', null, 'Bearer access-2']);
  });

  it('never writes tokens to the legacy store and purges old copies', () => {
    const legacy = new MemoryStorage();
    legacy.setItem('roam_token', 'old');
    legacy.setItem('roam_refresh_token', 'old');
    const session = new MemoryStorage();
    const manager = new TokenManager('https://api.test', session, (async () => json(200, {})) as typeof fetch, () => 0, legacy);
    manager.setTokens({ accessToken: 'a', refreshToken: 'r', expiresIn: 60 });

    assert.equal(legacy.data.size, 0);
    assert.equal(session.getItem('roam_token'), 'a');
  });
});
