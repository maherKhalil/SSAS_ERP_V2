import { Routes } from '@angular/router';
import { ModuleSelectorComponent } from './features/module-selector/module-selector';
import { authGuard } from './core/guards/auth-guard';

export const routes: Routes = [
  { path: 'modules', component: ModuleSelectorComponent, canActivate: [authGuard] },
  { path: '', redirectTo: 'modules', pathMatch: 'full' }
];
