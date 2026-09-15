import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Permission } from '../../core/auth/permission';

interface ModuleCard {
  title: string;
  permission: string;
  route: string;
  icon: string;
  description: string;
}

@Component({
  selector: 'app-module-selector',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './module-selector.html',
  styleUrl: './module-selector.css',
})
export class ModuleSelectorComponent {
  private permissionService = inject(Permission);

  allModules: ModuleCard[] = [
    {
      title: 'HR & Employees',
      permission: 'Platform.HR.View',
      route: '/hr/dashboard',
      icon: '👥',
      description: 'Manage employees, attendance, and HR operations.'
    },
    {
      title: 'Payroll & Compensation',
      permission: 'Payroll.Runs.View',
      route: '/payroll/dashboard',
      icon: '💰',
      description: 'Process payroll, bonuses, and compensation.'
    },
    {
      title: 'General Ledger & Finance',
      permission: 'Platform.GL.View',
      route: '/gl',
      icon: '📈',
      description: 'Manage accounts, journal entries, and financial reports.'
    },
    {
      title: 'Subscription Billing',
      permission: 'Platform.Subscriptions.View',
      route: '/subscriptions',
      icon: '🔄',
      description: 'Manage recurring billing and subscriptions.'
    },
    {
      title: 'HIS',
      permission: 'Platform.HIS.View',
      route: '/his',
      icon: '🏥',
      description: 'Healthcare Information System for hospitals.'
    }
  ];

  get visibleModules(): ModuleCard[] {
    return this.allModules.filter(m => this.permissionService.hasPermission(m.permission));
  }
}
