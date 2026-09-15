import { Injectable } from '@angular/core';

export interface MenuItem {
  label: string;
  route: string;
  icon?: string;
}

@Injectable({
  providedIn: 'root'
})
export class SidebarMenuService {
  
  getMenuForModule(modulePrefix: string): MenuItem[] {
    if (modulePrefix === 'hr') {
      return [
        { label: 'Dashboard', route: '/hr/dashboard', icon: '📊' },
        { label: 'Employees', route: '/hr/employees', icon: '👥' },
        { label: 'Departments', route: '/hr/departments', icon: '🏢' }
      ];
    } else if (modulePrefix === 'payroll') {
      return [
        { label: 'Dashboard', route: '/payroll/dashboard', icon: '📊' },
        { label: 'Runs', route: '/payroll/runs', icon: '🔄' },
        { label: 'Elements', route: '/payroll/elements', icon: '⚡' }
      ];
    }
    return [];
  }
}
