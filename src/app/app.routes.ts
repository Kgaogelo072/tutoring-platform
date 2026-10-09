import { Routes } from '@angular/router';
import { Home } from './pages/home/home';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'diagnostic', loadComponent: () => import('./pages/diagnostic/diagnostic').then((m) => m.Diagnostic) },
  { path: 'privacy', loadComponent: () => import('./pages/privacy/privacy').then((m) => m.Privacy) },
  // DX-01: the learner's test, opened from the emailed link.
  { path: 'd/:token', loadComponent: () => import('./pages/test/test-page').then((m) => m.TestPage) },
  {
    path: 'admin',
    loadComponent: () => import('./admin/admin-shell').then((m) => m.AdminShell),
    children: [
      { path: '', loadComponent: () => import('./admin/leads-page').then((m) => m.LeadsPage) },
      { path: 'learners/:id', loadComponent: () => import('./admin/learner-page').then((m) => m.LearnerPage) },
      { path: 'questions', loadComponent: () => import('./admin/questions-page').then((m) => m.QuestionsPage) },
      {
        path: 'assessments/:id/preview',
        loadComponent: () => import('./admin/preview-page').then((m) => m.PreviewPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
