import { Routes } from '@angular/router';
import { ModuleSelectorComponent } from './features/module-selector/module-selector';
import { authGuard } from './core/guards/auth-guard';
import { permissionGuard } from './core/guards/permission-guard';
import { Shell } from './layout/shell/shell';

export const routes: Routes = [
  { path: '', redirectTo: 'modules', pathMatch: 'full' },
  { path: 'modules', component: ModuleSelectorComponent, canActivate: [authGuard] },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      {
        path: 'hr/dashboard',
        loadComponent: () => import('./features/hr/dashboard/dashboard').then(m => m.Dashboard),
        canActivate: [permissionGuard],
        data: { permission: 'Platform.HR.View' }
      },
      {
        path: 'payroll/dashboard',
        loadComponent: () => import('./features/payroll/dashboard/dashboard').then(m => m.Dashboard),
        canActivate: [permissionGuard],
        data: { permission: 'Payroll.Runs.View' }
      }
    ]
  }
];
