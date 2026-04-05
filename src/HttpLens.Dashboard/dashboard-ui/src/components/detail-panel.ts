import { store } from '../state/store.js';
import { getStatusClass, formatTime, parseDurationMs, formatSize } from '../utils/formatters.js';
import { escapeHtml, prettyPrintJson } from '../utils/html.js';
import { $, $$ } from '../utils/dom.js';
import type { HttpTrafficRecord, DetailTab } from '../types/traffic.js';

export class DetailPanel {
  private currentTab: DetailTab = 'request';

  init(): void {
    store.subscribe(() => this.render());

    // Tab click listeners
    const tabBar = document.querySelector('.tab-bar');
    tabBar?.addEventListener('click', (e) => {
      const btn = (e.target as Element).closest('.tab-btn');
      if (!btn) return;
      const tab = btn.getAttribute('data-tab') as DetailTab;
      if (tab) {
        this.currentTab = tab;
        this.render();
      }
    });
  }

  render(): void {
    const record = store.getSelectedRecord();
    const panel = $('#detail-panel');
    if (!panel) return;

    if (!record) {
      panel.classList.add('hidden');
      return;
    }

    panel.classList.remove('hidden');
    this.renderTabs(record);
  }

  private renderTabs(record: HttpTrafficRecord): void {
    $$('.tab-btn').forEach(btn => {
      const isActive = btn.getAttribute('data-tab') === this.currentTab;
      btn.classList.toggle('active', isActive);
    });

    const content = $('#detail-content');
    if (!content) return;

    switch (this.currentTab) {
      case 'request':
        content.innerHTML = this.renderRequestTab(record);
        break;
      case 'response':
        content.innerHTML = this.renderResponseTab(record);
        break;
      case 'headers':
        content.innerHTML = this.renderHeadersTab(record);
        break;
      case 'timing':
        content.innerHTML = this.renderTimingTab(record);
        break;
      case 'correlation':
        content.innerHTML = this.renderCorrelationTab(record);
        break;
      case 'export':
        content.innerHTML = this.renderExportTab(record);
        break;
    }
  }

  private renderRequestTab(r: HttpTrafficRecord): string {
    const headers = Object.entries(r.requestHeaders)
      .map(([k, v]) => `<tr><td>${escapeHtml(k)}</td><td>${escapeHtml(v.join(', '))}</td></tr>`)
      .join('');

    return `
      <div class="detail-section">
        <h3><span class="badge method-${r.requestMethod.toLowerCase()}">${escapeHtml(r.requestMethod)}</span>
        <span class="detail-uri">${escapeHtml(r.requestUri)}</span></h3>
        ${headers ? `<table class="headers-table"><thead><tr><th>Header</th><th>Value</th></tr></thead><tbody>${headers}</tbody></table>` : ''}
        ${r.requestBody ? `<pre class="body-pre"><code>${prettyPrintJson(r.requestBody)}</code></pre>` : '<p class="muted">No request body</p>'}
      </div>`;
  }

  private renderResponseTab(r: HttpTrafficRecord): string {
    const sc = getStatusClass(r.responseStatusCode);
    const exceptionBox = r.exception
      ? `<div class="error-box"><strong>Exception:</strong><pre>${escapeHtml(r.exception)}</pre></div>`
      : '';

    return `
      <div class="detail-section">
        <h3><span class="badge status-${sc}">${r.responseStatusCode ?? 'ERR'}</span>
        <span class="muted">${parseDurationMs(r.duration).toFixed(0)}ms</span></h3>
        ${exceptionBox}
        ${r.responseBody ? `<pre class="body-pre"><code>${prettyPrintJson(r.responseBody)}</code></pre>` : '<p class="muted">No response body</p>'}
      </div>`;
  }

  private renderHeadersTab(r: HttpTrafficRecord): string {
    const renderHeaders = (headers: Record<string, string[]>, title: string) => {
      const rows = Object.entries(headers)
        .map(([k, v]) => `<tr><td>${escapeHtml(k)}</td><td>${escapeHtml(v.join(', '))}</td></tr>`)
        .join('');
      return rows
        ? `<h4>${title}</h4><table class="headers-table"><thead><tr><th>Header</th><th>Value</th></tr></thead><tbody>${rows}</tbody></table>`
        : `<h4>${title}</h4><p class="muted">No headers</p>`;
    };

    return `
      <div class="detail-section">
        ${renderHeaders(r.requestHeaders, 'Request Headers')}
        ${renderHeaders(r.responseHeaders, 'Response Headers')}
      </div>`;
  }

  private renderTimingTab(r: HttpTrafficRecord): string {
    return `
      <div class="detail-section">
        <table class="timing-table">
          <tr><th>Started</th><td>${formatTime(r.timestamp)}</td></tr>
          <tr><th>Duration</th><td>${parseDurationMs(r.duration).toFixed(2)}ms</td></tr>
          <tr><th>Request Size</th><td>${formatSize(r.requestBodySizeBytes)}</td></tr>
          <tr><th>Response Size</th><td>${formatSize(r.responseBodySizeBytes)}</td></tr>
          ${r.traceId ? `<tr><th>Trace ID</th><td>${escapeHtml(r.traceId)}</td></tr>` : ''}
          ${r.inboundRequestPath ? `<tr><th>Inbound Path</th><td>${escapeHtml(r.inboundRequestPath)}</td></tr>` : ''}
        </table>
      </div>`;
  }

  private renderCorrelationTab(r: HttpTrafficRecord): string {
    const retryInfo = r.retryGroupId
      ? `<tr><th>Retry Group</th><td>${escapeHtml(r.retryGroupId)}</td></tr>
         <tr><th>Attempt</th><td>Attempt ${r.attemptNumber} of group</td></tr>`
      : '<tr><th>Retry</th><td class="muted">No retry group</td></tr>';

    return `
      <div class="detail-section">
        <h4>Correlation</h4>
        <table class="timing-table">
          ${r.traceId ? `<tr><th>Trace ID</th><td><code>${escapeHtml(r.traceId)}</code></td></tr>` : ''}
          ${r.parentSpanId ? `<tr><th>Parent Span ID</th><td><code>${escapeHtml(r.parentSpanId)}</code></td></tr>` : ''}
          ${r.inboundRequestPath ? `<tr><th>Inbound Request</th><td>${escapeHtml(r.inboundRequestPath)}</td></tr>` : ''}
          <tr><th>HttpClient Name</th><td>${escapeHtml(r.httpClientName)}</td></tr>
          ${retryInfo}
        </table>
      </div>`;
  }

  private renderExportTab(r: HttpTrafficRecord): string {
    const curlCmd = this.toCurl(r);
    const csharpCode = this.toCSharp(r);

    return `
      <div class="detail-section">
        <h4>cURL</h4>
        <div class="export-block">
          <button class="copy-btn" data-target="export-curl">📋 Copy</button>
          <pre id="export-curl" class="body-pre export-pre"><code>${escapeHtml(curlCmd)}</code></pre>
        </div>

        <h4>C# HttpClient</h4>
        <div class="export-block">
          <button class="copy-btn" data-target="export-csharp">📋 Copy</button>
          <pre id="export-csharp" class="body-pre export-pre"><code>${escapeHtml(csharpCode)}</code></pre>
        </div>

        <h4>HAR</h4>
        <button class="har-download-btn" id="btn-export-single-har">📦 Download HAR</button>
      </div>`;
  }

  private toCurl(r: HttpTrafficRecord): string {
    const parts: string[] = [`curl -X ${r.requestMethod} '${escapeSingleQuotes(r.requestUri)}'`];
    for (const [name, values] of Object.entries(r.requestHeaders)) {
      const value = values.join(', ');
      parts.push(`-H '${escapeSingleQuotes(name)}: ${escapeSingleQuotes(value)}'`);
    }
    if (r.requestBody) {
      parts.push(`-d '${escapeSingleQuotes(r.requestBody)}'`);
    }
    return parts.join(' \\\n  ');
  }

  private toCSharp(r: HttpTrafficRecord): string {
    const contentHeaders = new Set([
      'content-type', 'content-length', 'content-encoding', 'content-language',
      'content-location', 'content-md5', 'content-range', 'content-disposition', 'expires', 'last-modified'
    ]);
    const method = r.requestMethod.charAt(0).toUpperCase() + r.requestMethod.slice(1).toLowerCase();
    const lines: string[] = [];
    lines.push('using var client = new HttpClient();');
    lines.push(`var request = new HttpRequestMessage(HttpMethod.${method}, "${escapeString(r.requestUri)}");`);
    for (const [name, values] of Object.entries(r.requestHeaders)) {
      if (contentHeaders.has(name.toLowerCase())) continue;
      const value = values.join(', ');
      lines.push(`request.Headers.TryAddWithoutValidation("${escapeString(name)}", "${escapeString(value)}");`);
    }
    if (r.requestBody) {
      const mediaType = (r.requestContentType ?? 'text/plain').split(';')[0].trim();
      lines.push(`request.Content = new StringContent(@"${r.requestBody.replace(/"/g, '""')}", Encoding.UTF8, "${mediaType}");`);
    }
    lines.push('var response = await client.SendAsync(request);');
    lines.push('var body = await response.Content.ReadAsStringAsync();');
    return lines.join('\n');
  }
}

function escapeSingleQuotes(s: string): string {
  return s.replace(/'/g, "'\\''");
}

function escapeString(s: string): string {
  return s.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}
