import * as signalR from '@microsoft/signalr';
import { store } from '../state/store.js';
import type { HttpTrafficRecord } from '../types/traffic.js';
import type { TrafficApiService } from './traffic-api.service.js';
import { PollingService } from './polling.service.js';

const SESSION_KEY = 'httplens_api_key';

export class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private reconnectAttempts = 0;
  private readonly pollingService: PollingService;

  constructor(
    private readonly basePath: string,
    api: TrafficApiService
  ) {
    this.pollingService = new PollingService(api);
  }

  async start(): Promise<void> {
    const hubUrl = this.buildHubUrl();
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: () => {
          this.reconnectAttempts += 1;
          const backoff = Math.min(1000 * (2 ** this.reconnectAttempts), 30000);
          return backoff;
        },
      })
      .build();

    this.connection.on('RecordAdded', (record: HttpTrafficRecord) => {
      store.prependRecord(record);
    });

    this.connection.onreconnecting(() => {
      store.setConnectionStatus('reconnecting');
      store.setConnectionMode('signalr');
    });

    this.connection.onreconnected(() => {
      this.reconnectAttempts = 0;
      store.setConnectionStatus('live');
      store.setConnectionMode('signalr');
      this.pollingService.stop();
    });

    this.connection.onclose(() => {
      if (store.getState().connectionMode === 'polling') {
        return;
      }

      this.startPollingFallback();
    });

    try {
      await this.connection.start();
      this.reconnectAttempts = 0;
      store.setConnectionStatus('live');
      store.setConnectionMode('signalr');
      this.pollingService.stop();
    } catch {
      this.startPollingFallback();
    }
  }

  async stop(): Promise<void> {
    this.pollingService.stop();
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
    store.setConnectionStatus('disconnected');
    store.setConnectionMode('none');
  }

  private buildHubUrl(): string {
    const key = sessionStorage.getItem(SESSION_KEY);
    const url = new URL(`${window.location.origin}${this.basePath}/hub`);
    if (key) {
      url.searchParams.set('key', key);
    }

    return url.toString();
  }

  private startPollingFallback(): void {
    store.setConnectionMode('polling');
    this.pollingService.start();
  }
}
