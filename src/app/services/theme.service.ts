import { Injectable, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

/** The user's own choice (topbar toggle); absent while the app follows the browser / OS setting. */
const STORAGE_KEY = 'a360.theme';

/**
 * Light / dark theme for the whole app (shell and every module, as `data-theme` on <html>; the dark styles are in
 * src/styles.css). It follows the browser's / OS's light-dark setting by default and keeps following it: whenever
 * that setting changes, the app switches with it. The topbar toggle sets the theme by hand until the setting
 * changes again. src/index.html applies the same choice before the app starts, so a page load never flashes white.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly media = typeof matchMedia === 'function' ? matchMedia('(prefers-color-scheme: dark)') : null;
  readonly theme = signal<Theme>(this.initial());

  constructor() {
    this.apply(this.theme());
    this.media?.addEventListener('change', (e) => {
      // the browser / OS setting changed: follow it again
      this.forget();
      this.set(e.matches ? 'dark' : 'light', false);
    });
  }

  toggle(): void {
    this.set(this.theme() === 'dark' ? 'light' : 'dark', true);
  }

  private set(theme: Theme, remember: boolean): void {
    if (remember) {
      try {
        localStorage.setItem(STORAGE_KEY, theme);
      } catch {
        /* storage blocked: the choice lasts for this page only */
      }
    }
    this.theme.set(theme);
    this.apply(theme);
  }

  private initial(): Theme {
    let saved: string | null = null;
    try {
      saved = localStorage.getItem(STORAGE_KEY);
    } catch {
      saved = null;
    }
    if (saved === 'light' || saved === 'dark') return saved;
    return this.media?.matches ? 'dark' : 'light';
  }

  private forget(): void {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* nothing stored */
    }
  }

  private apply(theme: Theme): void {
    if (typeof document !== 'undefined') document.documentElement.dataset['theme'] = theme;
  }
}
