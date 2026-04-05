import type { HttpTrafficRecord, TrafficListResponse } from '../types/traffic.js';

export class TrafficApiService {
  constructor(private readonly basePath: string) {}

  async fetchTraffic(take = 100, skip = 0): Promise<TrafficListResponse> {
    const res = await fetch(`${this.basePath}/api/traffic?skip=${skip}&take=${take}`);
    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return res.json();
  }

  async fetchById(id: string): Promise<HttpTrafficRecord | null> {
    const res = await fetch(`${this.basePath}/api/traffic/${id}`);
    if (res.status === 404) return null;
    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return res.json();
  }

  async clearAll(): Promise<void> {
    await fetch(`${this.basePath}/api/traffic`, { method: 'DELETE' });
  }
}
