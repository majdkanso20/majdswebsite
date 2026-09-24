import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { featureGuard } from './core/guards/feature.guard';
import { permissionGuard } from './core/guards/permission.guard';

export const routes: Routes = [
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register)
  },
  {
    path: 'forgot-password',
    loadComponent: () => import('./features/auth/forgot-password/forgot-password').then((m) => m.ForgotPassword)
  },
  {
    path: 'reset-password',
    loadComponent: () => import('./features/auth/reset-password/reset-password').then((m) => m.ResetPassword)
  },
  {
    path: 'login/callback',
    loadComponent: () => import('./features/auth/login-callback/login-callback').then((m) => m.LoginCallback)
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login)
  },
  {
    path: '',
    loadComponent: () => import('./layout/shell/shell').then((m) => m.Shell),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard/dashboard').then((m) => m.Dashboard)
      },
      {
        path: 'administration/users',
        canActivate: [permissionGuard('Users.View')],
        loadComponent: () =>
          import('./features/administration/users/users-list/users-list').then((m) => m.UsersList)
      },
      {
        path: 'administration/roles',
        canActivate: [permissionGuard('Roles.View')],
        loadComponent: () =>
          import('./features/administration/roles/roles-list/roles-list').then((m) => m.RolesList)
      },
      {
        path: 'administration/settings',
        canActivate: [permissionGuard('Settings.View')],
        loadComponent: () =>
          import('./features/administration/settings/settings-page/settings-page').then((m) => m.SettingsPage)
      },
      {
        path: 'administration/jobs',
        canActivate: [permissionGuard('Jobs.View')],
        loadComponent: () => import('./features/administration/jobs/jobs-page/jobs-page').then((m) => m.JobsPage)
      },
      {
        path: 'administration/features',
        canActivate: [permissionGuard('Features.View')],
        loadComponent: () =>
          import('./features/administration/features/features-page/features-page').then((m) => m.FeaturesPage)
      },
      {
        path: 'account',
        loadComponent: () => import('./features/account/account-page/account-page').then((m) => m.AccountPage)
      },
      {
        path: 'files',
        canActivate: [featureGuard('Files')],
        loadComponent: () => import('./features/files/files-list/files-list').then((m) => m.FilesList)
      },
      {
        path: 'administration/audit-log',
        canActivate: [permissionGuard('Audit.View')],
        loadComponent: () =>
          import('./features/administration/audit-log/audit-log-list/audit-log-list').then((m) => m.AuditLogList)
      },
      {
        path: 'administration/plugins',
        canActivate: [permissionGuard('Plugins.View')],
        loadComponent: () =>
          import('./features/administration/plugins/plugins-list/plugins-list').then((m) => m.PluginsList)
      }
    ]
  }
];
