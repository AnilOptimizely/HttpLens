import type { HttpTrafficRecord, TrafficListResponse } from '../types/traffic.js';

const SESSION_KEY = 'httplens_api_key';

/** Returns the API key stored in sessionStorage (set from the ?key= query param on page load). */
function getStoredApiKey(): string | null {
  return sessionStorage.getItem(SESSION_KEY);
}

/** Persists the API key from the current page URL's ?key= parameter into sessionStorage. */
function captureKeyFromUrl(): void {
  const params = new URLSearchParams(window.location.search);
  const key = params.get('key');
  if (key) {
    sessionStorage.setItem(SESSION_KEY, key);
  }
}

// Run once on module load so the key is available before any API calls.
captureKeyFromUrl();

/** Builds fetch request headers, injecting X-HttpLens-Key when an API key is stored. */
function buildHeaders(): HeadersInit {
  const key = getStoredApiKey();
  return key ? { 'X-HttpLens-Key': key } : {};
}

export class TrafficApiService {
  constructor(private readonly basePath: string) {}

  async fetchTraffic(take = 100, skip = 0): Promise<TrafficListResponse> {
    const res = await fetch(`${this.basePath}/api/traffic?skip=${skip}&take=${take}`, {
      headers: buildHeaders(),
    });
    if (res.status === 401) {
      throw new Error('HttpLens API key required. Navigate to /_httplens?key=<your-key>');
    }
    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return res.json();
  }

  async fetchById(id: string): Promise<HttpTrafficRecord | null> {
    const res = await fetch(`${this.basePath}/api/traffic/${id}`, {
      headers: buildHeaders(),
    });
    if (res.status === 404) return null;
    if (res.status === 401) {
      throw new Error('HttpLens API key required. Navigate to /_httplens?key=<your-key>');
    }
    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return res.json();
  }

  async clearAll(): Promise<void> {
    await fetch(`${this.basePath}/api/traffic`, {
      method: 'DELETE',
      headers: buildHeaders(),
    });
  }
}
