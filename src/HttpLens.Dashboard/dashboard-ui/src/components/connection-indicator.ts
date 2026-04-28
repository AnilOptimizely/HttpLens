import { store } from '../state/store.js';

export class ConnectionIndicator {
  private element: HTMLElement | null = null;

  init(): void {
    this.element = document.getElementById('connection-indicator');
    store.subscribe(() => this.render());
    this.render();
  }

  private render(): void {
    if (!this.element) return;
    const { connectionMode, connectionStatus } = store.getState();

    if (connectionMode === 'signalr' && connectionStatus === 'live') {
      this.element.textContent = '⚡ Live';
      this.element.className = 'connection-indicator mode-live';
      this.element.title = 'SignalR real-time updates are active';
      return;
    }

    if (connectionStatus === 'reconnecting') {
      this.element.textContent = '🔁 Reconnecting...';
      this.element.className = 'connection-indicator mode-reconnecting';
      this.element.title = 'SignalR is reconnecting';
      return;
    }

    if (connectionMode === 'polling') {
      this.element.textContent = '🔄 Polling';
      this.element.className = 'connection-indicator mode-polling';
      this.element.title = 'Polling fallback is active';
      return;
    }

    this.element.textContent = '⚠️ Disconnected';
    this.element.className = 'connection-indicator mode-disconnected';
    this.element.title = 'No live connection is currently available';
  }
}
