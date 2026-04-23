import type { HttpTrafficRecord, FilterState, ConnectionStatus } from '../types/traffic.js';

interface AppState {
  records: HttpTrafficRecord[];
  selectedId: string | null;
  filters: FilterState;
  connectionStatus: ConnectionStatus;
  connectionMode: 'signalr' | 'polling' | 'none';
  totalServerRecords: number;
}

type Listener = () => void;

class Store {
  private state: AppState = {
    records: [],
    selectedId: null,
    filters: { method: '', status: '', host: '', search: '' },
    connectionStatus: 'disconnected',
    connectionMode: 'none',
    totalServerRecords: 0,
  };

  private listeners: Set<Listener> = new Set();

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  notify(): void {
    this.listeners.forEach(l => l());
  }

  setRecords(records: HttpTrafficRecord[], total: number): void {
    this.state.records = records;
    this.state.totalServerRecords = total;
    this.notify();
  }

  prependRecord(record: HttpTrafficRecord): void {
    this.state.records = [record, ...this.state.records];
    this.notify();
  }

  selectRecord(id: string | null): void {
    this.state.selectedId = id;
    this.notify();
  }

  setFilters(filters: Partial<FilterState>): void {
    this.state.filters = { ...this.state.filters, ...filters };
    this.notify();
  }

  setConnectionStatus(status: ConnectionStatus): void {
    this.state.connectionStatus = status;
    this.notify();
  }

  setConnectionMode(mode: 'signalr' | 'polling' | 'none'): void {
    this.state.connectionMode = mode;
    this.notify();
  }

  clearRecords(): void {
    this.state.records = [];
    this.state.selectedId = null;
    this.state.totalServerRecords = 0;
    this.notify();
  }

  getSelectedRecord(): HttpTrafficRecord | undefined {
    return this.state.records.find(r => r.id === this.state.selectedId);
  }

  getFilteredRecords(): HttpTrafficRecord[] {
    const { method, status, host, search } = this.state.filters;
    if (!method && !status && !host && !search) return this.state.records;
    return this.state.records.filter(r => {
      if (method && r.requestMethod !== method.toUpperCase()) return false;
      if (status && !String(r.responseStatusCode ?? '').startsWith(status)) return false;
      if (host && !r.requestUri.includes(host)) return false;
      if (search && !r.requestUri.toLowerCase().includes(search.toLowerCase())) return false;
      return true;
    });
  }

  getState(): Readonly<AppState> {
    return this.state;
  }
}

export const store = new Store();
