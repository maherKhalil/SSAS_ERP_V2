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
      title: 'Platform Administration',
      permission: 'Platform.Tenants.View',
      route: '/admin/dashboard',
      icon: '\u{1F6E1}',
      description: 'Manage tenants, settings, and platform configurations.'
    },
    {
      title: 'Identity & Access',
      permission: 'Platform.Identity.View',
      route: '/identity/dashboard',
      icon: '\u{1F512}',
      description: 'Manage users, roles, and permissions.'
    },
    {
      title: 'HR & Employees',
      permission: 'Platform.HR.View',
      route: '/hr/dashboard',
      icon: '\u{1F465}',
      description: 'Manage employees, positions, and departments.'
    },
    {
      title: 'Time & Attendance',
      permission: 'Platform.Attendance.View',
      route: '/attendance/dashboard',
      icon: '\u{1F552}',
      description: 'Track employee time, attendance, and leave.'
    },
    {
      title: 'Payroll & Compensation',
      permission: 'Payroll.Runs.View',
      route: '/payroll/dashboard',
      icon: '\u{1F4B5}',
      description: 'Process payroll, bonuses, and compensation.'
    },
    {
      title: 'Employee Self-Service',
      permission: 'Platform.SelfService.View',
      route: '/self-service/dashboard',
      icon: '\u{1F4F1}',
      description: 'Employee portal for requests and paystubs.'
    },
    {
      title: 'General Ledger & Finance',
      permission: 'Platform.GL.View',
      route: '/gl/dashboard',
      icon: '\u{1F4C8}',
      description: 'Manage accounts, journal entries, and reports.'
    },
    {
      title: 'Subscription Billing',
      permission: 'Platform.Subscriptions.View',
      route: '/subscriptions/dashboard',
      icon: '\u{1F504}',
      description: 'Manage recurring billing, plans, and entitlements.'
    },
    {
      title: 'Health Information System',
      permission: 'HIS.Registration.Manage',
      route: '/his/dashboard',
      icon: '\u{1F3E5}',
      description: 'Clinical operations, patient registration, and management.'
    }
  ];

  get visibleModules(): ModuleCard[] {
    return this.allModules.filter(m => this.permissionService.hasPermission(m.permission));
  }
}
