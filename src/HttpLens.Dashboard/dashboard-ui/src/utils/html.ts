export function escapeHtml(str: string): string {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

export function prettyPrintJson(body: string | null | undefined): string {
  if (!body) return '';
  try {
    return escapeHtml(JSON.stringify(JSON.parse(body), null, 2));
  } catch {
    return escapeHtml(body);
  }
}
