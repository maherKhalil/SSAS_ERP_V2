import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { Permission } from '../auth/permission';

export const permissionGuard: CanActivateFn = (route, state) => {
  const permissionService = inject(Permission);
  const router = inject(Router);

  const requiredPermissions = route.data['permissions'] as string[];

  if (!requiredPermissions || requiredPermissions.length === 0) {
    return true; // No specific permissions required
  }

  if (permissionService.hasAnyPermission(requiredPermissions)) {
    return true;
  }

  // Not authorized
  return router.parseUrl('/unauthorized');
};
