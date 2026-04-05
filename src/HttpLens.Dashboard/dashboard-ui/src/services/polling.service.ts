import { store } from '../state/store.js';
import type { TrafficApiService } from './traffic-api.service.js';

export class PollingService {
  private timerId: ReturnType<typeof setInterval> | null = null;

  constructor(
    private readonly api: TrafficApiService,
    private readonly intervalMs: number = 2000
  ) {}

  start(): void {
    store.setConnectionStatus('connected');
    this.timerId = setInterval(async () => {
      try {
        const data = await this.api.fetchTraffic();
        store.setRecords(data.records, data.total);
      } catch {
        store.setConnectionStatus('reconnecting');
      }
    }, this.intervalMs);
  }

  stop(): void {
    if (this.timerId !== null) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
    store.setConnectionStatus('disconnected');
  }
}
