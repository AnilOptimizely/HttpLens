import { store } from '../state/store.js';
import { $ } from '../utils/dom.js';

/**
 * Filter bar component that wires HTML filter inputs to the store's filter state.
 * Enables users to filter traffic by HTTP method, status code class, host, and free-text search.
 */
export class FilterBar {
  init(): void {
    const methodSelect = $<HTMLSelectElement>('#filter-method');
    const statusSelect = $<HTMLSelectElement>('#filter-status');
    const hostInput = $<HTMLInputElement>('#filter-host');
    const searchInput = $<HTMLInputElement>('#filter-search');
    const clearBtn = $('#btn-clear-filters');

    methodSelect?.addEventListener('change', () => {
      const value = methodSelect.value === 'All' ? '' : methodSelect.value;
      store.setFilters({ method: value });
    });

    statusSelect?.addEventListener('change', () => {
      const value = statusSelect.value;
      // Map "2xx" → "2", "3xx" → "3", etc. "All" → ""
      const mapped = value === 'All' ? '' : value.charAt(0);
      store.setFilters({ status: mapped });
    });

    hostInput?.addEventListener('input', () => {
      store.setFilters({ host: hostInput.value });
    });

    searchInput?.addEventListener('input', () => {
      store.setFilters({ search: searchInput.value });
    });

    clearBtn?.addEventListener('click', () => {
      if (methodSelect) methodSelect.value = 'All';
      if (statusSelect) statusSelect.value = 'All';
      if (hostInput) hostInput.value = '';
      if (searchInput) searchInput.value = '';
      store.setFilters({ method: '', status: '', host: '', search: '' });
    });
  }
}
