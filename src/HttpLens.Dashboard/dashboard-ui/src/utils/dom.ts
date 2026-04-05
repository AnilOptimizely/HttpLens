export function $<T extends Element = Element>(selector: string): T | null {
  return document.querySelector<T>(selector);
}

export function $$<T extends Element = Element>(selector: string): T[] {
  return Array.from(document.querySelectorAll<T>(selector));
}

export function setHtml(selector: string, html: string): void {
  const el = $(selector);
  if (el) el.innerHTML = html;
}

export function toggleClass(el: Element, cls: string, force?: boolean): void {
  el.classList.toggle(cls, force);
}

export function createElement<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  options?: { className?: string; textContent?: string; innerHTML?: string }
): HTMLElementTagNameMap[K] {
  const el = document.createElement(tag);
  if (options?.className) el.className = options.className;
  if (options?.textContent) el.textContent = options.textContent;
  if (options?.innerHTML) el.innerHTML = options.innerHTML;
  return el;
}
