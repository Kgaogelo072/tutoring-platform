import { Routes } from '@angular/router';
import { Home } from './pages/home/home';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'diagnostic', loadComponent: () => import('./pages/diagnostic/diagnostic').then((m) => m.Diagnostic) },
  { path: 'privacy', loadComponent: () => import('./pages/privacy/privacy').then((m) => m.Privacy) },
  { path: '**', redirectTo: '' },
];
