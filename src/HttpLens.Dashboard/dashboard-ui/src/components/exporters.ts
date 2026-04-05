import { parseDurationMs } from '../utils/formatters.js';
import type { HttpTrafficRecord } from '../types/traffic.js';

/** Handles clipboard copy and HAR export actions. */
export class Exporters {
  init(): void {
    // Event delegation on #detail-content for .copy-btn clicks
    const detail = document.getElementById('detail-content');
    detail?.addEventListener('click', (e) => {
      const btn = (e.target as Element).closest('.copy-btn') as HTMLButtonElement | null;
      if (!btn) return;
      const targetId = btn.getAttribute('data-target');
      if (!targetId) return;

      const pre = document.getElementById(targetId);
      if (!pre) return;

      const text = pre.textContent ?? '';
      const original = btn.textContent ?? '';

      navigator.clipboard.writeText(text).then(() => {
        btn.textContent = '✅ Copied!';
        setTimeout(() => { btn.textContent = original; }, 2000);
      }).catch(() => {
        btn.textContent = '❌ Failed';
        setTimeout(() => { btn.textContent = original; }, 2000);
      });
    });
  }

  /** Generates a HAR 1.2 JSON string from the given records (client-side). */
  static exportAsHar(records: HttpTrafficRecord[]): string {
    const entries = records.map(r => {
      const durationMs = parseDurationMs(r.duration);
      return {
        startedDateTime: r.timestamp,
        time: durationMs,
        request: {
          method: r.requestMethod,
          url: r.requestUri,
          httpVersion: 'HTTP/1.1',
          headers: toHarHeaders(r.requestHeaders),
          queryString: extractQueryParams(r.requestUri),
          bodySize: r.requestBodySizeBytes ?? -1,
          postData: r.requestBody ? { mimeType: r.requestContentType ?? 'text/plain', text: r.requestBody } : undefined
        },
        response: {
          status: r.responseStatusCode ?? 0,
          statusText: '',
          httpVersion: 'HTTP/1.1',
          headers: toHarHeaders(r.responseHeaders),
          content: {
            size: r.responseBodySizeBytes ?? -1,
            mimeType: r.responseContentType ?? 'text/plain',
            text: r.responseBody ?? undefined
          },
          bodySize: r.responseBodySizeBytes ?? -1
        },
        cache: {},
        timings: { send: 0, wait: durationMs, receive: 0 }
      };
    });

    return JSON.stringify({
      log: {
        version: '1.2',
        creator: { name: 'HttpLens', version: '1.0.0' },
        entries
      }
    }, null, 2);
  }

  /** Triggers a download of the supplied records as a .har file. */
  static downloadHar(records: HttpTrafficRecord[]): void {
    const json = Exporters.exportAsHar(records);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    const ts = new Date().toISOString().replace(/[:.]/g, '-');
    a.href = url;
    a.download = `httplens-${ts}.har`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }
}

function toHarHeaders(headers: Record<string, string[]>): Array<{ name: string; value: string }> {
  const list: Array<{ name: string; value: string }> = [];
  for (const [name, values] of Object.entries(headers)) {
    for (const value of values) {
      list.push({ name, value });
    }
  }
  return list;
}

function extractQueryParams(url: string): Array<{ name: string; value: string }> {
  try {
    const u = new URL(url);
    const result: Array<{ name: string; value: string }> = [];
    u.searchParams.forEach((value, name) => result.push({ name, value }));
    return result;
  } catch {
    return [];
  }
}
