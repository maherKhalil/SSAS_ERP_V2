import { Service } from '@angular/core';

@Service()
export class Permission {
  private _permissions: string[] = ['Platform.HR.View', 'Payroll.Runs.View', 'Platform.GL.View', 'Platform.Subscriptions.View', 'HIS.Registration.Manage']; // Mock state

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
