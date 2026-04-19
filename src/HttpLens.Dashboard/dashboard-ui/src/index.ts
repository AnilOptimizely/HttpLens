import { TrafficApiService } from './services/traffic-api.service.js';
import { PollingService } from './services/polling.service.js';
import { TrafficTable } from './components/traffic-table.js';
import { DetailPanel } from './components/detail-panel.js';
import { Exporters } from './components/exporters.js';
import { FilterBar } from './components/filter-bar.js';
import { store } from './state/store.js';
import { $ } from './utils/dom.js';

async function bootstrap(): Promise<void> {
  const basePath =
    document.documentElement.dataset['httplensBasePath'] ??
    window.location.pathname.replace(/\/$/, '');

  const api = new TrafficApiService(basePath);
  const polling = new PollingService(api);
  const table = new TrafficTable();
  const panel = new DetailPanel();
  const exporters = new Exporters();
  const filterBar = new FilterBar();

  table.init();
  panel.init();
  exporters.init();
  filterBar.init();

  // Theme toggle
  initTheme();

  try {
    const initial = await api.fetchTraffic();
    store.setRecords(initial.records, initial.total);
  } catch (err) {
    console.error('Initial fetch failed:', err);
  }

  polling.start();

  // Wire buttons
  $('#btn-clear')?.addEventListener('click', async () => {
    await api.clearAll();
    store.clearRecords();
  });

  $('#btn-refresh')?.addEventListener('click', async () => {
    try {
      const data = await api.fetchTraffic();
      store.setRecords(data.records, data.total);
    } catch (err) {
      console.error('Refresh failed:', err);
    }
  });

  // HAR export button
  $('#btn-export-har')?.addEventListener('click', () => {
    Exporters.downloadHar(store.getFilteredRecords());
  });

  // Single record HAR download (event delegation from detail panel)
  document.getElementById('detail-content')?.addEventListener('click', (e) => {
    const btn = (e.target as Element).closest('#btn-export-single-har');
    if (!btn) return;
    const record = store.getSelectedRecord();
    if (record) Exporters.downloadHar([record]);
  });
}

function initTheme(): void {
  const saved = localStorage.getItem('httplens-theme') ?? 'dark';
  document.documentElement.setAttribute('data-theme', saved);

  $('#btn-theme')?.addEventListener('click', () => {
    const current = document.documentElement.getAttribute('data-theme') ?? 'dark';
    const next = current === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', next);
    localStorage.setItem('httplens-theme', next);
    const btn = $('#btn-theme');
    if (btn) btn.textContent = next === 'dark' ? '🌙 Dark' : '☀️ Light';
  });
}

document.addEventListener('DOMContentLoaded', () => {
  bootstrap().catch(console.error);
});
