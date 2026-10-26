/**
 * Scroll reveal for every page rendered inside the layout (including the federated remotes, which render into the
 * same router outlet). Card-like elements fade up one after another when a page opens, and the ones below the fold
 * do the same as they scroll into view. Cards re-rendered in place later (filters, live updates) are left alone so
 * the page doesn't flicker while you work.
 */

/** Card-like blocks on the pages. Nested matches inside an already-animated card are skipped. */
const CARD_SELECTOR = [
  '.card', '.panel', '.kpi', '.stat-card', '.chart-card', '.side-card', '.kpi-card',
  '.insp-panel', '.wip-panel', '.panel-box', '.widget', '.dash-section > article',
].join(', ');

/** Never animate inside popups, menus or dialogs. */
const EXCLUDE_SELECTOR = '.overlay, .modal, [role="dialog"], [role="menu"], .menu, .pop, .tip, .toast, .cdk-overlay-container, .cdk-drag-preview';

/** How long after opening a page newly rendered cards still animate even when already in view. */
const PAGE_WINDOW_MS = 1500;
const STAGGER_MS = 70;
const MAX_STAGGER = 10;

export class ScrollReveal {
  private io?: IntersectionObserver;
  private mo?: MutationObserver;
  private seen = new WeakSet<Element>();
  private pageOpenedAt = performance.now();
  private queued = new Set<Element>();
  private frame = 0;
  private readonly enabled: boolean;

  constructor(private root: HTMLElement) {
    this.enabled = typeof IntersectionObserver !== 'undefined' && !matchMedia('(prefers-reduced-motion: reduce)').matches;
  }

  start(): void {
    if (!this.enabled) return;
    this.io = new IntersectionObserver((entries) => this.onIntersect(entries), { rootMargin: '0px 0px -6% 0px', threshold: 0.06 });
    this.mo = new MutationObserver((records) => {
      for (const r of records) r.addedNodes.forEach((n) => n instanceof Element && this.queued.add(n));
      this.schedule();
    });
    this.mo.observe(this.root, { childList: true, subtree: true });
    this.queued.add(this.root);
    this.schedule();
  }

  /** Call on navigation: cards of the new page animate even if they are already in view. */
  pageOpened(): void {
    this.pageOpenedAt = performance.now();
  }

  stop(): void {
    this.io?.disconnect();
    this.mo?.disconnect();
    cancelAnimationFrame(this.frame);
  }

  private schedule(): void {
    if (this.frame) return;
    this.frame = requestAnimationFrame(() => {
      this.frame = 0;
      const nodes = [...this.queued];
      this.queued.clear();
      const fresh = performance.now() - this.pageOpenedAt < PAGE_WINDOW_MS;
      for (const node of nodes) {
        if (!node.isConnected) continue;
        const found = node.matches(CARD_SELECTOR) ? [node, ...node.querySelectorAll(CARD_SELECTOR)] : [...node.querySelectorAll(CARD_SELECTOR)];
        for (const el of found) this.consider(el as HTMLElement, fresh);
      }
    });
  }

  private consider(el: HTMLElement, fresh: boolean): void {
    if (this.seen.has(el)) return;
    this.seen.add(el);
    if (el.closest(EXCLUDE_SELECTOR)) return;
    // only the outermost card animates; its children come with it
    const parentCard = el.parentElement?.closest(CARD_SELECTOR);
    if (parentCard && this.root.contains(parentCard)) return;
    const style = getComputedStyle(el);
    if (style.position === 'fixed' || style.display === 'none' || style.visibility === 'hidden') return;
    if (!fresh) {
      // re-rendered later: animate only if it is still below the fold, otherwise leave it as is
      const r = el.getBoundingClientRect();
      if (r.top < innerHeight) return;
    }
    el.classList.add('piq-reveal-pending');
    this.io!.observe(el);
  }

  private onIntersect(entries: IntersectionObserverEntry[]): void {
    const visible = entries
      .filter((e) => e.isIntersecting)
      .map((e) => e.target as HTMLElement)
      .sort((a, b) => {
        const ra = a.getBoundingClientRect();
        const rb = b.getBoundingClientRect();
        // reading order: rows top to bottom, then left to right
        return Math.abs(ra.top - rb.top) > 24 ? ra.top - rb.top : ra.left - rb.left;
      });
    visible.forEach((el, i) => {
      this.io!.unobserve(el);
      el.style.setProperty('--piq-reveal-delay', `${Math.min(i, MAX_STAGGER) * STAGGER_MS}ms`);
      el.classList.add('piq-reveal-play');
      el.classList.remove('piq-reveal-pending');
      const done = (ev: AnimationEvent) => {
        if (ev.target !== el || ev.animationName !== 'piq-reveal-up') return;
        el.classList.remove('piq-reveal-play');
        el.style.removeProperty('--piq-reveal-delay');
        el.removeEventListener('animationend', done);
      };
      el.addEventListener('animationend', done);
    });
  }
}
