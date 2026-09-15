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
    { title: 'Platform Administration', permission: 'Platform.Tenants.View', route: '/admin/dashboard', icon: '\u{1F6E1}', description: 'Manage tenants, settings, and platform configurations.' },
    { title: 'Identity & Access', permission: 'Platform.Identity.View', route: '/identity/dashboard', icon: '\u{1F512}', description: 'Manage users, roles, and permissions.' },
    { title: 'HR & Employees', permission: 'Platform.HR.View', route: '/hr/dashboard', icon: '\u{1F465}', description: 'Manage employees, positions, and departments.' },
    { title: 'Time & Attendance', permission: 'Platform.Attendance.View', route: '/attendance/dashboard', icon: '\u{1F552}', description: 'Track employee time, attendance, and leave.' },
    { title: 'Payroll & Compensation', permission: 'Payroll.Runs.View', route: '/payroll/dashboard', icon: '\u{1F4B5}', description: 'Process payroll, bonuses, and compensation.' },
    { title: 'Employee Self-Service', permission: 'Platform.SelfService.View', route: '/self-service/dashboard', icon: '\u{1F4F1}', description: 'Employee portal for requests and paystubs.' },
    { title: 'General Ledger & Finance', permission: 'Platform.GL.View', route: '/gl/dashboard', icon: '\u{1F4C8}', description: 'Manage accounts, journal entries, and reports.' },
    { title: 'Subscription Billing', permission: 'Platform.Subscriptions.View', route: '/subscriptions/dashboard', icon: '\u{1F504}', description: 'Manage recurring billing, plans, and entitlements.' },
    
    // Clinical Modules (HIS)
    { title: 'Patient Registration', permission: 'HIS.Registration.Manage', route: '/his/registration/dashboard', icon: '\u{1F4CB}', description: 'Register and manage patient demographics.' },
    { title: 'Outpatient (OPD)', permission: 'HIS.Outpatient.View', route: '/his/outpatient/dashboard', icon: '\u{1F3E5}', description: 'Manage outpatient clinics and appointments.' },
    { title: 'Inpatient (IPD)', permission: 'HIS.Inpatient.View', route: '/his/inpatient/dashboard', icon: '\u{1F6CF}', description: 'Manage ward admissions and inpatient care.' },
    { title: 'Pharmacy', permission: 'HIS.Pharmacy.View', route: '/his/pharmacy/dashboard', icon: '\u{1F48A}', description: 'Dispense medications and manage inventory.' },
    { title: 'Laboratory', permission: 'HIS.Laboratory.View', route: '/his/laboratory/dashboard', icon: '\u{1F52C}', description: 'Process lab tests and medical pathology.' },
    { title: 'Radiology', permission: 'HIS.Radiology.View', route: '/his/radiology/dashboard', icon: '\u{1F4FB}', description: 'Manage imaging, X-rays, and MRI scans.' },
    { title: 'Emergency (ER)', permission: 'HIS.Emergency.View', route: '/his/emergency/dashboard', icon: '\u{1F6A8}', description: 'Handle trauma cases and emergency triage.' },
    { title: 'Blood Bank', permission: 'HIS.BloodBank.View', route: '/his/bloodbank/dashboard', icon: '\u{1FA78}', description: 'Manage blood stock and transfusions.' }
  ];

  get visibleModules(): ModuleCard[] {
    return this.allModules.filter(m => this.permissionService.hasPermission(m.permission));
  }
}
