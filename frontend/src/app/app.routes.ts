import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';

export const appRoutes: Routes = [
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent) },
  { path: 'register', canActivate: [guestGuard], loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent) },
  {
    path: '', canActivate: [authGuard], loadComponent: () => import('./layout/shell.component').then(m => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'sales' },
      { path: 'sales', loadComponent: () => import('./features/sales/sales-list.component').then(m => m.SalesListComponent) },
      { path: 'sales/new', loadComponent: () => import('./features/sales/sale-form.component').then(m => m.SaleFormComponent) },
      { path: 'sales/:id', loadComponent: () => import('./features/sales/sale-detail.component').then(m => m.SaleDetailComponent) },
      { path: 'sales/:id/edit', loadComponent: () => import('./features/sales/sale-form.component').then(m => m.SaleFormComponent) },
      { path: 'users', loadComponent: () => import('./features/users/users.component').then(m => m.UsersComponent) }
    ]
  },
  { path: '**', redirectTo: '' }
];
