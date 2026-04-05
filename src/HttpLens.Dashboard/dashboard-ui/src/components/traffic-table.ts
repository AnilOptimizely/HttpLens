import { store } from '../state/store.js';
import { getStatusClass, formatTime, formatDuration, formatSize, truncateUrl } from '../utils/formatters.js';
import { escapeHtml } from '../utils/html.js';
import { $ } from '../utils/dom.js';
import type { HttpTrafficRecord } from '../types/traffic.js';

export class TrafficTable {
  private tbody: HTMLElement | null = null;

  init(): void {
    this.tbody = document.getElementById('traffic-body');
    store.subscribe(() => this.render());

    // Event delegation for row clicks
    this.tbody?.addEventListener('click', (e) => {
      const row = (e.target as Element).closest('tr[data-id]');
      if (row) {
        const id = row.getAttribute('data-id');
        store.selectRecord(id);
      }
    });
  }

  render(): void {
    if (!this.tbody) return;
    const records = store.getFilteredRecords();
    const selectedId = store.getState().selectedId;

    if (records.length === 0) {
      this.tbody.innerHTML = '<tr><td colspan="6" class="empty-state">No traffic recorded yet. Make some HTTP calls!</td></tr>';
    } else {
      const grouped = this.groupByRetry(records);
      this.tbody.innerHTML = grouped.map(r => this.renderRow(r.record, selectedId, r.isRetry)).join('');
    }

    const countEl = $('#record-count');
    if (countEl) countEl.textContent = `${records.length} records`;
  }

  private groupByRetry(records: HttpTrafficRecord[]): Array<{ record: HttpTrafficRecord; isRetry: boolean }> {
    // Build retry groups map
    const groups = new Map<string, HttpTrafficRecord[]>();
    const seen = new Set<string>();

    for (const r of records) {
      if (r.retryGroupId) {
        const existing = groups.get(r.retryGroupId) ?? [];
        existing.push(r);
        groups.set(r.retryGroupId, existing);
      }
    }

    const result: Array<{ record: HttpTrafficRecord; isRetry: boolean }> = [];

    for (const r of records) {
      if (r.retryGroupId && groups.has(r.retryGroupId)) {
        if (seen.has(r.retryGroupId)) continue;
        seen.add(r.retryGroupId);
        const group = groups.get(r.retryGroupId)!;
        // Sort by attempt number
        group.sort((a, b) => a.attemptNumber - b.attemptNumber);
        for (const attempt of group) {
          result.push({ record: attempt, isRetry: attempt.attemptNumber > 1 });
        }
      } else {
        result.push({ record: r, isRetry: false });
      }
    }

    return result;
  }

  private renderRow(r: HttpTrafficRecord, selectedId: string | null, isRetry: boolean): string {
    const sc = getStatusClass(r.responseStatusCode);
    const selected = r.id === selectedId ? ' selected' : '';
    const retryClass = isRetry ? ' retry-row' : '';
    const retryPrefix = isRetry
      ? `<span class="retry-indent">↳ RETRY attempt ${r.attemptNumber}</span> `
      : '';
    const hasRetries = !isRetry && r.retryGroupId && r.attemptNumber === 1
      ? '<span class="retry-indicator">↻</span>'
      : '';
    const retryBadge = r.attemptNumber > 1
      ? `<span class="retry-badge">↻${r.attemptNumber}</span>`
      : '';
    const inboundBadge = r.inboundRequestPath
      ? `<span class="inbound-badge">← ${escapeHtml(r.inboundRequestPath)}</span>`
      : '';

    return `
      <tr data-id="${r.id}" class="${selected}${retryClass}">
        <td>${formatTime(r.timestamp)}</td>
        <td>${retryPrefix}<span class="badge method-${r.requestMethod.toLowerCase()}">${escapeHtml(r.requestMethod)}</span>${retryBadge}${hasRetries}</td>
        <td class="url-cell" title="${escapeHtml(r.requestUri)}">${escapeHtml(truncateUrl(r.requestUri))}${inboundBadge}</td>
        <td><span class="badge status-${sc}">${r.responseStatusCode ?? 'ERR'}</span></td>
        <td>${formatDuration(r.duration)}</td>
        <td>${formatSize(r.responseBodySizeBytes)}</td>
      </tr>`;
  }
}
