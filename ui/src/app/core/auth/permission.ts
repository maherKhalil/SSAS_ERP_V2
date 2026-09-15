import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class Permission {
  private _permissions: string[] = [
    'Platform.Tenants.View',
    'Platform.Identity.View',
    'Platform.HR.View', 
    'Platform.Attendance.View',
    'Payroll.Runs.View', 
    'Platform.SelfService.View',
    'Platform.GL.View', 
    'Platform.Subscriptions.View', 
    'HIS.Registration.Manage',
    'HIS.Outpatient.View',
    'HIS.Inpatient.View',
    'HIS.Pharmacy.View',
    'HIS.Laboratory.View',
    'HIS.Radiology.View',
    'HIS.Emergency.View',
    'HIS.BloodBank.View'
  ];

  getPermissions(): string[] {
    return this._permissions;
  }

  hasPermission(permission: string): boolean {
    return this._permissions.includes(permission);
  }

  hasAnyPermission(permissions: string[]): boolean {
    if (!permissions || permissions.length === 0) return true;
    return permissions.some(p => this.hasPermission(p));
  }
}
