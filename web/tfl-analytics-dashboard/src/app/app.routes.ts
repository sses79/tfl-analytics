import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./views/dashboard/dashboard.component').then(m => m.DashboardComponent)
  },
  {
    path: 'status',
    loadComponent: () =>
      import('./views/line-status/line-status.component').then(m => m.LineStatusComponent)
  },
  {
    path: 'arrivals',
    loadComponent: () =>
      import('./views/arrivals/arrivals.component').then(m => m.ArrivalsComponent)
  },
  {
    path: 'alerts',
    loadComponent: () =>
      import('./views/alerts/alerts.component').then(m => m.AlertsComponent)
  },
  { path: '**', redirectTo: 'dashboard' }
];
