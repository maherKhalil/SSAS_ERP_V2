import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { SidebarMenuService, MenuItem } from '../sidebar-menu';
import { Auth } from '../../core/auth/auth';

@Component({
  imports: [CommonModule, RouterModule],
  selector: 'app-shell',
  styleUrl: './shell.css',
  templateUrl: './shell.html',
  standalone: true
})
export class Shell {
  private router = inject(Router);
  private sidebarMenuService = inject(SidebarMenuService);
  private authService = inject(Auth);

  menuItems: MenuItem[] = [];
  moduleName = '';
  userName = '';

  constructor() {
    this.userName = this.authService.getCurrentUser()?.username || 'User';
    this.updateMenu(this.router.url);

    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe((event: any) => {
      this.updateMenu(event.urlAfterRedirects);
    });
  }

  private updateMenu(url: string) {
    if (url.startsWith('/hr')) {
      this.moduleName = 'HR Module';
      this.menuItems = this.sidebarMenuService.getMenuForModule('hr');
    } else if (url.startsWith('/payroll')) {
      this.moduleName = 'Payroll Module';
      this.menuItems = this.sidebarMenuService.getMenuForModule('payroll');
    } else {
      this.moduleName = 'Dashboard';
      this.menuItems = [];
    }
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
