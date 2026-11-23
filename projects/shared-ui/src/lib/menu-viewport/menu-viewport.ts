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
 * Also covered: menus that are not added on open but stay in the page and are shown/moved by the component
 * (a class/style/hidden change on the menu or an ancestor, or one shared `position: fixed` row menu that
 * the component moves from row to row via [style.top.px]). Such menus are re-checked on every attribute
 * change, released (original inline style restored) when they hide, and kept inside the screen when the
 * component's own fixed position would put them off-screen. Pages using that pattern (2026-11-23):
 * assets: Asset Assignment, Assign Asset, Asset Audit, New Asset Audit (row/criterion/team menus),
 * Asset Disposal; tracking-iot: Device Management, Device Type & Model; shell: topbar account menu.
 *
 * Opt out for a specific element with the `data-menu-static` attribute.
 */

const MENU_SELECTOR = '[role="menu"], .menu';
const GAP = 8; // distance kept from the screen edges
const OFFSET = 4; // distance between trigger and menu
const MANAGED = 'menuViewportManaged';

interface Managed {
  menu: HTMLElement;
  anchor: HTMLElement | null;
  /** The menu's own inline style before it was pinned, restored when it hides. */
  original: string;
  /** What the guard last wrote: a different top/left means the component moved the menu itself. */
  top: string;
  left: string;
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

  /** Look at a shown (or moved) menu: pin it only when it doesn't fit. */
  function check(menu: HTMLElement): void {
    if (!menu.isConnected || menu.closest('[data-menu-static]')) return;
    const entry = managed.get(menu);
    if (entry) {
      if (!isShown(menu)) release(entry);
      else if (menu.style.top !== entry.top || menu.style.left !== entry.left) {
        // The component moved the menu (e.g. same menu opened on another row): start over from its position.
        release(entry, false);
        check(menu);
      }
      return;
    }
    const position = win!.getComputedStyle(menu).position;
    if (position !== 'absolute' && position !== 'fixed') return;
    const rect = menu.getBoundingClientRect();
    if (!rect.width || !rect.height) return;
    if (fitsOnScreen(rect) && (position === 'fixed' || !clippedByScrollParent(menu, rect))) return;
    const created: Managed = { menu, anchor: findAnchor(menu, position), original: menu.getAttribute('style') ?? '', top: '', left: '' };
    managed.set(menu, created);
    menu.dataset[MANAGED] = 'true';
    place(created, rect);
  }

  function isShown(menu: HTMLElement): boolean {
    const r = menu.getBoundingClientRect();
    return menu.isConnected && r.width > 0 && r.height > 0 && win!.getComputedStyle(menu).visibility !== 'hidden';
  }

  /** Un-pins a menu. `restore` puts back its own inline style (when it hides); else the component's current one is kept. */
  function release(entry: Managed, restore = true): void {
    managed.delete(entry.menu);
    delete entry.menu.dataset[MANAGED];
    if (!entry.menu.isConnected) return;
    if (restore) {
      if (entry.original) entry.menu.setAttribute('style', entry.original);
      else entry.menu.removeAttribute('style');
    } else {
      const s = entry.menu.style;
      for (const prop of ['right', 'bottom', 'margin', 'width', 'transform', 'z-index', 'max-height', 'overflow-y', 'position']) {
        if (!new RegExp(`(^|;)\\s*${prop}\\s*:`).test(entry.original)) s.removeProperty(prop);
      }
    }
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

  /**
   * The button that opened the menu: the trigger next to it in its wrapper, else the wrapper itself.
   * A fixed menu shared by all rows sits far from its trigger: use the focused / expanded trigger, else none.
   */
  function findAnchor(menu: HTMLElement, position: string): HTMLElement | null {
    if (position === 'fixed') {
      const active = doc!.activeElement as HTMLElement | null;
      if (active && active !== doc!.body && !menu.contains(active) && active.matches('button, [role="button"]')) return active;
      const expanded = [...doc!.querySelectorAll<HTMLElement>('[aria-expanded="true"][aria-haspopup]')].filter((b) => !menu.contains(b));
      return expanded.length === 1 ? expanded[0] : null;
    }
    const parent = menu.parentElement ?? menu;
    const buttons = [...parent.querySelectorAll<HTMLElement>('button, [role="button"]')].filter((b) => !menu.contains(b));
    return buttons.find((b) => b.hasAttribute('aria-expanded')) ?? buttons[0] ?? parent;
  }

  /** Pins the menu to the screen next to its trigger. */
  function place(entry: Managed, rect = entry.menu.getBoundingClientRect()): void {
    const { menu, anchor } = entry;
    const vw = win!.innerWidth;
    const vh = win!.innerHeight;
    // Without a trigger, keep the menu where it is and only pull it back inside the screen.
    const a = anchor?.getBoundingClientRect() ?? new DOMRect(rect.right, rect.top - OFFSET, 0, 0);
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
    entry.top = s.top;
    entry.left = s.left;
  }

  const reposition = () => {
    managed.forEach((entry, menu) => {
      if (!menu.isConnected) managed.delete(menu);
      else if (!isShown(menu)) release(entry);
      else place(entry);
    });
  };

  const collect = (node: Node) => {
    if (!(node instanceof HTMLElement)) return;
    if (node.matches(MENU_SELECTOR)) schedule(node);
    node.querySelectorAll<HTMLElement>(MENU_SELECTOR).forEach(schedule);
  };

  new MutationObserver((records) => {
    const seen = new Set<Node>();
    for (const record of records) {
      if (record.type === 'attributes') {
        // A menu (or a wrapper around one) was shown, hidden or moved without being added/removed.
        const target = (record.attributeName === 'aria-expanded' ? record.target.parentElement : record.target) as HTMLElement | null;
        if (target && !seen.has(target)) {
          seen.add(target);
          collect(target);
        }
        continue;
      }
      record.addedNodes.forEach(collect);
      record.removedNodes.forEach((node) => {
        if (node instanceof HTMLElement && managed.size) {
          managed.forEach((_, menu) => {
            if (!menu.isConnected) managed.delete(menu);
          });
        }
      });
    }
  }).observe(doc.body, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ['class', 'style', 'hidden', 'aria-expanded', 'open'],
  });

  win.addEventListener('resize', reposition, { passive: true });
  win.addEventListener('scroll', reposition, { passive: true, capture: true });
}
