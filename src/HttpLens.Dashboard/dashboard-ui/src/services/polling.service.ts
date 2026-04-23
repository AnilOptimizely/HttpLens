import { store } from '../state/store.js';
import type { TrafficApiService } from './traffic-api.service.js';

export class PollingService {
  private timerId: ReturnType<typeof setInterval> | null = null;

  constructor(
    private readonly api: TrafficApiService,
    private readonly intervalMs: number = 2000
  ) {}

  start(): void {
    store.setConnectionMode('polling');
    store.setConnectionStatus('connected');
    this.timerId = setInterval(async () => {
      try {
        const data = await this.api.fetchTraffic();
        store.setRecords(data.records, data.total);
        store.setConnectionStatus('connected');
      } catch {
        store.setConnectionStatus('disconnected');
      }
    }, this.intervalMs);
  }

  stop(): void {
    if (this.timerId !== null) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
    if (store.getState().connectionMode === 'polling') {
      store.setConnectionStatus('disconnected');
      store.setConnectionMode('none');
    }
  }
}
