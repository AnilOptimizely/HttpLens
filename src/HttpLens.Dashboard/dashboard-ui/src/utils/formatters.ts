import type { StatusClass } from '../types/traffic.js';

export function getStatusClass(code: number | null | undefined): StatusClass {
  if (code === null || code === undefined) return 'error';
  if (code >= 200 && code < 300) return 'success';
  if (code >= 300 && code < 400) return 'redirect';
  if (code >= 400 && code < 500) return 'client-error';
  if (code >= 500) return 'server-error';
  return 'error';
}

export function formatTime(isoString: string): string {
  const d = new Date(isoString);
  const h = String(d.getHours()).padStart(2, '0');
  const m = String(d.getMinutes()).padStart(2, '0');
  const s = String(d.getSeconds()).padStart(2, '0');
  const ms = String(d.getMilliseconds()).padStart(3, '0');
  return `${h}:${m}:${s}.${ms}`;
}

/** Parses a .NET TimeSpan string (e.g. "00:00:01.2345678") into milliseconds. */
export function parseDurationMs(timespan: string): number {
  if (!timespan) return 0;
  // Format: [d.]hh:mm:ss[.fffffff]
  const parts = timespan.split(':');
  if (parts.length < 3) return 0;
  const hours = parseFloat(parts[0]);
  const minutes = parseFloat(parts[1]);
  const seconds = parseFloat(parts[2]);
  return (hours * 3600 + minutes * 60 + seconds) * 1000;
}

export function formatDuration(timespan: string): string {
  const ms = parseDurationMs(timespan);
  if (ms < 1000) return `${ms.toFixed(0)}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
}

export function formatSize(bytes: number | null | undefined): string {
  if (bytes === null || bytes === undefined) return '-';
  if (bytes < 1024) return `${bytes}B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)}K`;
  return `${(bytes / (1024 * 1024)).toFixed(1)}MB`;
}

export function truncateUrl(url: string): string {
  try {
    const u = new URL(url);
    return u.pathname + u.search;
  } catch {
    return url;
  }
}

export function extractHost(url: string): string {
  try {
    return new URL(url).host;
  } catch {
    return url;
  }
}
