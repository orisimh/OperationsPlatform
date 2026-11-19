import { Component } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { ThemeService } from './services/theme.service';

interface Breadcrumb {
  label: string;
  route?: string;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  standalone: false
})
export class AppComponent {
  breadcrumbs: Breadcrumb[] = [{ label: 'Workspace', route: '/dashboard' }, { label: 'Operations Hub' }];

  constructor(public theme: ThemeService, private router: Router) {
    this.updateBreadcrumbs(this.router.url);
    this.router.events.pipe(filter(event => event instanceof NavigationEnd)).subscribe(event => {
      this.updateBreadcrumbs((event as NavigationEnd).urlAfterRedirects);
    });
  }

  toggleTheme(): void { this.theme.toggle(); }

  private updateBreadcrumbs(url: string): void {
    const path = url.split('?')[0];
    const workspace: Breadcrumb[] = [{ label: 'Workspace', route: '/dashboard' }];

    if (path.startsWith('/operations/new')) {
      this.breadcrumbs = [...workspace, { label: 'Operations', route: '/operations' }, { label: 'Create operation' }];
    } else if (/^\/operations\/[^/]+\/edit/.test(path)) {
      this.breadcrumbs = [...workspace, { label: 'Operations', route: '/operations' }, { label: 'Edit operation' }];
    } else if (path.startsWith('/operations')) {
      this.breadcrumbs = [...workspace, { label: 'Operations' }];
    } else if (/^\/execute\//.test(path)) {
      this.breadcrumbs = [...workspace, { label: 'Operations', route: '/operations' }, { label: 'Execute operation' }];
    } else if (/^\/builder\//.test(path)) {
      this.breadcrumbs = [...workspace, { label: 'Operation Builder', route: '/builder' }, { label: 'Edit flow' }];
    } else if (path.startsWith('/builder')) {
      this.breadcrumbs = [...workspace, { label: 'Operation Builder' }];
    } else if (path.startsWith('/history')) {
      this.breadcrumbs = [...workspace, { label: 'Execution history' }];
    } else if (path.startsWith('/logs')) {
      this.breadcrumbs = [...workspace, { label: 'Application logs' }];
    } else {
      this.breadcrumbs = [...workspace, { label: 'Dashboard' }];
    }
  }
}
