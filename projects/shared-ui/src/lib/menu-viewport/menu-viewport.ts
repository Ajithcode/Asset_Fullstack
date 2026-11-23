/**
 * Keeps pop-up action menus (the ⋮ / ••• row menus, "More options" menus, …) inside the visible screen.
 *
 * Every module renders its own menu markup (`<div class="menu" role="menu">` next to its trigger button,
 * absolutely positioned under it). The shell installs this guard once; it watches the whole document, so it
 * covers all micro-frontends. When a menu opens and would end up below/right of the screen (or clipped by a
 * scrolling table), it is pinned with `position: fixed` next to its trigger: flipped above the trigger when
 * there is no room below, shifted left when there is no room on the right, and given a scrollbar when the
 * screen is too short for it. Menus that already fit are left untouched.
 *
 * Opt out for a specific element with the `data-menu-static` attribute.
 */

const MENU_SELECTOR = '[role="menu"], .menu';
const GAP = 8; // distance kept from the screen edges
const OFFSET = 4; // distance between trigger and menu
const MANAGED = 'menuViewportManaged';

interface Managed {
  menu: HTMLElement;
  anchor: HTMLElement;
}

export function installMenuViewportGuard(doc: Document | undefined = typeof document === 'undefined' ? undefined : document): void {
  const win = doc?.defaultView as (Window & { __a360MenuViewportGuard?: boolean }) | null | undefined;
  if (!doc || !win || win.__a360MenuViewportGuard || typeof MutationObserver === 'undefined') return;
  win.__a360MenuViewportGuard = true;

  const managed = new Map<HTMLElement, Managed>();
  const pending = new Set<HTMLElement>();
  let frame = 0;

  const schedule = (menu: HTMLElement) => {
    pending.add(menu);
    if (!frame) {
      frame = win.requestAnimationFrame(() => {
        frame = 0;
        const menus = [...pending];
        pending.clear();
        menus.forEach(check);
      });
    }
  };

  /** First look at a newly shown menu: pin it only when it doesn't fit. */
  function check(menu: HTMLElement): void {
    if (!menu.isConnected || managed.has(menu) || menu.closest('[data-menu-static]')) return;
    if (win!.getComputedStyle(menu).position !== 'absolute') return;
    const rect = menu.getBoundingClientRect();
    if (!rect.width || !rect.height) return;
    if (fitsOnScreen(rect) && !clippedByScrollParent(menu, rect)) return;
    const anchor = findAnchor(menu);
    const entry = { menu, anchor };
    managed.set(menu, entry);
    menu.dataset[MANAGED] = 'true';
    place(entry, rect);
  }

  function fitsOnScreen(r: DOMRect): boolean {
    return r.top >= GAP && r.left >= GAP && r.bottom <= win!.innerHeight - GAP && r.right <= win!.innerWidth - GAP;
  }

  function clippedByScrollParent(menu: HTMLElement, r: DOMRect): boolean {
    for (let el = menu.parentElement; el && el !== doc!.body; el = el.parentElement) {
      const s = win!.getComputedStyle(el);
      if (/(auto|scroll|hidden|clip)/.test(s.overflow + s.overflowX + s.overflowY)) {
        const p = el.getBoundingClientRect();
        if (r.bottom > p.bottom + 1 || r.right > p.right + 1 || r.top < p.top - 1 || r.left < p.left - 1) return true;
      }
    }
    return false;
  }

  /** The button that opened the menu: the trigger next to it in its wrapper, else the wrapper itself. */
  function findAnchor(menu: HTMLElement): HTMLElement {
    const parent = menu.parentElement ?? menu;
    const buttons = [...parent.querySelectorAll<HTMLElement>('button, [role="button"]')].filter((b) => !menu.contains(b));
    return buttons.find((b) => b.hasAttribute('aria-expanded')) ?? buttons[0] ?? parent;
  }

  /** Pins the menu to the screen next to its trigger. */
  function place({ menu, anchor }: Managed, rect = menu.getBoundingClientRect()): void {
    const vw = win!.innerWidth;
    const vh = win!.innerHeight;
    const a = anchor.getBoundingClientRect();
    const width = rect.width;
    const fullHeight = Math.max(rect.height, menu.scrollHeight);
    const height = Math.min(fullHeight, vh - 2 * GAP);

    const below = a.bottom + OFFSET;
    const above = a.top - OFFSET - height;
    let top: number;
    if (below + height <= vh - GAP) top = below;
    else if (above >= GAP) top = above;
    else top = Math.max(GAP, vh - GAP - height);

    // Keep the menu's own horizontal alignment when it fits, else line its right edge up with the trigger.
    let left = rect.left;
    if (left + width > vw - GAP || left < GAP) left = a.right - width;
    left = Math.min(Math.max(GAP, left), Math.max(GAP, vw - GAP - width));

    const s = menu.style;
    s.position = 'fixed';
    s.top = `${top}px`;
    s.left = `${left}px`;
    s.right = 'auto';
    s.bottom = 'auto';
    s.margin = '0';
    s.width = `${width}px`;
    s.transform = 'none';
    s.zIndex = '2000';
    if (height < fullHeight) {
      s.maxHeight = `${height}px`;
      s.overflowY = 'auto';
    }

    // An ancestor with a transform turns "fixed" into "relative to that ancestor": correct by the difference.
    const now = menu.getBoundingClientRect();
    const dx = left - now.left;
    const dy = top - now.top;
    if (Math.abs(dx) > 1 || Math.abs(dy) > 1) {
      s.top = `${top + dy}px`;
      s.left = `${left + dx}px`;
    }
  }

  const reposition = () => {
    managed.forEach((entry, menu) => {
      if (!menu.isConnected) managed.delete(menu);
      else place(entry);
    });
  };

  const collect = (node: Node) => {
    if (!(node instanceof HTMLElement)) return;
    if (node.matches(MENU_SELECTOR)) schedule(node);
    node.querySelectorAll<HTMLElement>(MENU_SELECTOR).forEach(schedule);
  };

  new MutationObserver((records) => {
    for (const record of records) {
      record.addedNodes.forEach(collect);
      record.removedNodes.forEach((node) => {
        if (node instanceof HTMLElement && managed.size) {
          managed.forEach((_, menu) => {
            if (!menu.isConnected) managed.delete(menu);
          });
        }
      });
    }
  }).observe(doc.body, { childList: true, subtree: true });

  win.addEventListener('resize', reposition, { passive: true });
  win.addEventListener('scroll', reposition, { passive: true, capture: true });
}
