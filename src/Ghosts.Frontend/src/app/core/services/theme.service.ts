import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

/** Where the person's choice is kept. index.html reads the same key before the app boots, so the first paint is right. */
export const THEME_KEY = 'ghosts-theme';

/**
 * Light or dark. Follows the operating system until the person picks one, then keeps their pick. The theme is the
 * data-theme attribute on <html>; styles.scss swaps the colour tokens on it.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly root = inject(DOCUMENT).documentElement;
  private readonly media = globalThis.matchMedia?.('(prefers-color-scheme: dark)');
  readonly theme = signal<Theme>(this.initial());

  constructor() {
    this.apply(this.theme());
    this.media?.addEventListener?.('change', (e) => {
      if (!this.stored()) this.set(e.matches ? 'dark' : 'light', false);
    });
  }

  toggle(): void {
    this.set(this.theme() === 'dark' ? 'light' : 'dark');
  }

  set(theme: Theme, remember = true): void {
    this.theme.set(theme);
    this.apply(theme);
    if (remember) {
      try { localStorage.setItem(THEME_KEY, theme); } catch { /* private mode: the choice lasts this page only */ }
    }
  }

  private initial(): Theme {
    return this.stored() ?? (this.media?.matches ? 'dark' : 'light');
  }

  private stored(): Theme | null {
    try {
      const value = localStorage.getItem(THEME_KEY);
      return value === 'light' || value === 'dark' ? value : null;
    } catch {
      return null;
    }
  }

  private apply(theme: Theme): void {
    this.root.dataset['theme'] = theme;
  }
}
