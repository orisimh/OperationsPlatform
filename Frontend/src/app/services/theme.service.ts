import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private dark = false;
  constructor() {
    const stored = localStorage.getItem('orbit-theme');
    this.dark = stored ? stored === 'dark' : false;
    this.apply();
  }
  get isDark(): boolean { return this.dark; }
  toggle(): void {
    this.dark = !this.dark;
    localStorage.setItem('orbit-theme', this.dark ? 'dark' : 'light');
    this.apply();
  }
  private apply(): void {
    document.body.classList.toggle('light-theme', !this.dark);
    document.body.classList.toggle('dark-theme', this.dark);
  }
}
