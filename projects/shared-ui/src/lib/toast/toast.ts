/**
 * The one toast (bottom-right notification) for the whole app. Every page calls pushToast(); <app-toast-host>
 * (mounted once in the shell layout) shows them. Durations live here and nowhere else.
 *
 * The store sits on globalThis because the shell and each micro-frontend bundle their own copy of this file.
 */
export type ToastKind = 'success' | 'info' | 'warning' | 'error';

export const TOAST_CONFIG = {
  /** How long a toast stays (it never closes while the mouse is over it). */
  durationMs: { success: 6000, info: 6000, warning: 10000, error: 10000 } as Record<ToastKind, number>,
  /** Older toasts are dropped when more than this are open. */
  maxVisible: 4,
};

export interface ToastItem {
  id: number;
  message: string;
  kind: ToastKind;
}

class ToastStore {
  items: ToastItem[] = [];
  private nextId = 1;
  private readonly listeners = new Set<() => void>();

  subscribe(fn: () => void): () => void {
    this.listeners.add(fn);
    return () => this.listeners.delete(fn);
  }

  push(message: string, kind: ToastKind): void {
    // The same message again refreshes the open toast instead of stacking
    this.items = this.items.filter((t) => !(t.message === message && t.kind === kind));
    this.items = [...this.items, { id: this.nextId++, message, kind }].slice(-TOAST_CONFIG.maxVisible);
    this.emit();
  }

  dismiss(id: number): void {
    this.items = this.items.filter((t) => t.id !== id);
    this.emit();
  }

  private emit(): void {
    this.listeners.forEach((fn) => fn());
  }
}

const KEY = '__a360ToastStore__';

export function toastStore(): ToastStore {
  const g = globalThis as unknown as Record<string, ToastStore | undefined>;
  return (g[KEY] ??= new ToastStore());
}

const ERROR_WORDS = /\b(fail(ed|ure|s)?|error|could not|couldn'?t|can'?t|cannot|unable|invalid|denied|not allowed|not reachable|not found|rejected|forbidden|unauthori[sz]ed|timed out|went wrong)\b/i;
const WARNING_WORDS = /\b(warning|already|no current location|skipped|nothing to|first|must|needs? to|not (configured|set up)|required|limit|at least|select|choose|expired|retired)\b/i;

/** Pages that only pass text get a kind from its wording: failures = error, cautions = warning, the rest = success. */
export function inferToastKind(message: string): ToastKind {
  if (ERROR_WORDS.test(message)) return 'error';
  if (WARNING_WORDS.test(message)) return 'warning';
  return 'success';
}

/** Shows a toast. `kind` may be a ToastKind, `true` (= error) or left out (guessed from the text). */
export function pushToast(message: string | null | undefined, kind?: ToastKind | boolean | null): void {
  const text = (message ?? '').trim();
  if (!text) return;
  const k: ToastKind = kind === true ? 'error' : typeof kind === 'string' ? kind : inferToastKind(text);
  toastStore().push(text, k);
}
